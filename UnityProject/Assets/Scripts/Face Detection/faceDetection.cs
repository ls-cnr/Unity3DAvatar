using UnityEngine;
using OpenCvSharp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

/// <summary>
/// Face detection using the UltraFace ONNX model.
/// 
/// MODEL INPUT:  640x480 RGB image, normalized to [-1, 1]
/// MODEL OUTPUT: confidences [1, numBoxes, numClasses], boxes [1, numBoxes, 4]
/// 
/// PIPELINE:
///   1. Resize and normalize input frame
///   2. Run ONNX inference
///   3. Post-process with NMS (handled by faceDetectUtils.Predict)
///   4. Convert coordinates back to original frame space
/// </summary>
public class FaceDetection : IDisposable
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// <summary>Maximum number of faces to detect per frame.</summary>
    private const int maxbatch = 10;

    // =========================================================================
    // ONNX SESSION
    // =========================================================================

    /// <summary>ONNX Runtime inference session for face detection.</summary>
    private InferenceSession faceDetector;

    // =========================================================================
    // INPUT BUFFERS (pinned for zero-copy with OpenCV)
    // =========================================================================

    /// <summary>Pinned buffer for model input [1, 3, 480, 640].</summary>
    private PinnedBuffer<float> _pinnedInputBuffer;

    /// <summary>Managed array view of the pinned buffer.</summary>
    private float[] faceDetectionInputBuffer;

    /// <summary>ONNX tensor wrapping the input buffer.</summary>
    private DenseTensor<float> faceDetectionTensor;

    // =========================================================================
    // OPENCV WRAPPERS (point into pinned buffer for zero-copy)
    // =========================================================================

    /// <summary>Blue channel wrapper (offset 0).</summary>
    private Mat bufferBlueWrapper;

    /// <summary>Green channel wrapper (offset 640*480).</summary>
    private Mat bufferGreenWrapper;

    /// <summary>Red channel wrapper (offset 640*480*2).</summary>
    private Mat bufferRedWrapper;

    // =========================================================================
    // REUSABLE MATS
    // =========================================================================

    /// <summary>Reusable mat for normalized input.</summary>
    private Mat reusableNewFrame = new Mat();

    /// <summary>Reusable channel split buffers.</summary>
    private Mat[] reusableInputChannels = new Mat[3];

    // =========================================================================
    // COORDINATE CONVERSION PARAMETERS
    // =========================================================================

    private float modelScaleX;
    private float modelScaleY;
    private int padX;
    private int padY;
    private int frameWidth;
    private int frameHeight;
    private int boxThickness;

    // =========================================================================
    // OUTPUT BUFFER
    // =========================================================================

    /// <summary>Reusable list for detection results.</summary>
    private List<(int x_min, int y_min, int x_max, int y_max,
                        int bbox_width, int bbox_height, float centerX, float centerY)> faceData;

    // =========================================================================
    // DISPOSAL
    // =========================================================================

    private bool _disposed = false;

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    /// <summary>
    /// Initializes the face detector with ONNX model and coordinate parameters.
    /// </summary>
    public FaceDetection(SessionOptions options, float xscale, float yscale, int xpad, int ypad,
                         string modelsPath, int width, int height, int bThick)
    {
        // Store coordinate conversion parameters
        padX = xpad;
        padY = ypad;
        modelScaleX = xscale;
        modelScaleY = yscale;
        frameWidth = width;
        frameHeight = height;
        boxThickness = bThick;

        faceData = new List<(int x_min, int y_min, int x_max, int y_max,
                        int bbox_width, int bbox_height, float centerX, float centerY)>();

        // Load ONNX model
        string faceDetectorPath = modelsPath + "ultraface.onnx";
        faceDetector = new InferenceSession(faceDetectorPath, options);
        UnityEngine.Debug.Log("Face Detector ✓");

        // Allocate pinned input buffer for zero-copy tensor preparation
        _pinnedInputBuffer = new PinnedBuffer<float>(1 * 3 * 480 * 640);
        faceDetectionInputBuffer = _pinnedInputBuffer.Array;

        faceDetectionTensor = new DenseTensor<float>(faceDetectionInputBuffer, new[] { 1, 3, 480, 640 });

        // Create OpenCV Mat wrappers pointing into the pinned buffer
        // Layout: [batch=1, channels=3, height=480, width=640]
        // Channel order in buffer: B, G, R (matching OpenCV's default)
        bufferBlueWrapper = Mat.FromPixelData(
            480, 640, MatType.CV_32FC1,
            _pinnedInputBuffer.Pointer,
            640 * sizeof(float));

        bufferGreenWrapper = Mat.FromPixelData(
            480, 640, MatType.CV_32FC1,
            _pinnedInputBuffer.ElementOffset(640 * 480),
            640 * sizeof(float));

        bufferRedWrapper = Mat.FromPixelData(
            480, 640, MatType.CV_32FC1,
            _pinnedInputBuffer.ElementOffset(640 * 480 * 2),
            640 * sizeof(float));

        reusableInputChannels[0] = new Mat();
        reusableInputChannels[1] = new Mat();
        reusableInputChannels[2] = new Mat();
    }

    // =========================================================================
    // INFERENCE
    // =========================================================================

    /// <summary>
    /// Runs face detection on the current frame.
    /// </summary>
    /// <param name="frame">Original camera frame (for reference, not directly used).</param>
    /// <param name="resFrame">Resized and padded frame (640x480) for model input.</param>
    /// <returns>List of detected faces with coordinates in original frame space.</returns>
    public List<(int x_min, int y_min, int x_max, int y_max,
                        int bbox_width, int bbox_height, float centerX, float centerY)> Inference(Mat frame, Mat resFrame)
    {
        // Normalize input: convert to float32 and scale to [-1, 1]
        // Formula: pixel / 128.0 - 127.0 / 128.0
        resFrame.ConvertTo(reusableNewFrame, MatType.CV_32FC3, 1.0f / 128.0f, -127.0f / 128.0f);

        // Split into BGR channels
        Cv2.Split(reusableNewFrame, out reusableInputChannels);

        // Copy channels into pinned buffer (zero-copy via Mat wrappers)
        reusableInputChannels[0].CopyTo(bufferBlueWrapper);
        reusableInputChannels[1].CopyTo(bufferGreenWrapper);
        reusableInputChannels[2].CopyTo(bufferRedWrapper);

        // Create ONNX input
        var inputs = new NamedOnnxValue[]
        {
            NamedOnnxValue.CreateFromTensor("input", faceDetectionTensor)
        };

        // Run inference
        int[,] processedBoxes;
        int[] labels;
        float[] probs;

        using (var results = faceDetector.Run(inputs))
        {
            var confidences = results[0].AsTensor<float>();
            var boxes = results[1].AsTensor<float>();

            // Post-process: NMS and coordinate scaling
            (processedBoxes, labels, probs) = faceDetectUtils.Predict(
                640, 480, confidences, boxes, 0.7f
            );
        }

        // Limit to maxbatch faces
        int numFaces = processedBoxes.GetLength(0);
        numFaces = Math.Min(numFaces, maxbatch);

        faceData.Clear();

        // Convert detections back to original frame coordinates
        for (int i = 0; i < numFaces; i++)
        {
            int x_min = processedBoxes[i, 0];
            int y_min = processedBoxes[i, 1];
            int x_max = processedBoxes[i, 2];
            int y_max = processedBoxes[i, 3];

            int bbox_width = Mathf.Abs(x_max - x_min);
            int bbox_height = Mathf.Abs(y_max - y_min);

            // Add margin around face for better head pose estimation
            int marginX = (int)(0.2f * bbox_width);
            int marginY = (int)(0.2f * bbox_height);

            int rawX = x_min - marginX;
            int rawY = y_min - marginY;
            int rawRight = x_max + marginX;
            int rawBottom = y_max + marginY;

            // Clamp to model input bounds
            int clampedX = Mathf.Max(0, rawX);
            int clampedY = Mathf.Max(0, rawY);
            int clampedRight = Mathf.Min(640, rawRight);
            int clampedBottom = Mathf.Min(480, rawBottom);

            int finalWidth = Mathf.Max(1, clampedRight - clampedX);
            int finalHeight = Mathf.Max(1, clampedBottom - clampedY);

            // Convert back to original frame coordinates
            int disp_x_min = (int)((clampedX - padX) / modelScaleX);
            int disp_y_min = (int)((clampedY - padY) / modelScaleY);
            int disp_x_max = (int)((clampedRight - padX) / modelScaleX);
            int disp_y_max = (int)((clampedBottom - padY) / modelScaleY);

            disp_x_min = Mathf.Max(0, disp_x_min);
            disp_y_min = Mathf.Max(0, disp_y_min);
            disp_x_max = Mathf.Min(frameWidth, disp_x_max);
            disp_y_max = Mathf.Min(frameHeight, disp_y_max);

            float centerX = (disp_x_min + disp_x_max) / 2f;
            float centerY = (disp_y_min + disp_y_max) / 2f;

            faceData.Add((clampedX, clampedY, clampedRight, clampedBottom, finalWidth, finalHeight, centerX, centerY));
        }
        return faceData;
    }

    // =========================================================================
    // DISPOSAL
    // =========================================================================

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                faceDetector?.Dispose();
                faceDetector = null;
                faceDetectionTensor = null;

                bufferBlueWrapper?.Dispose();
                bufferGreenWrapper?.Dispose();
                bufferRedWrapper?.Dispose();
                bufferBlueWrapper = null;
                bufferGreenWrapper = null;
                bufferRedWrapper = null;

                _pinnedInputBuffer?.Dispose();
                _pinnedInputBuffer = null;
                faceDetectionInputBuffer = null;

                reusableNewFrame?.Dispose();
                reusableNewFrame = null;

                if (reusableInputChannels != null)
                {
                    foreach (var mat in reusableInputChannels)
                    {
                        mat?.Dispose();
                    }
                    reusableInputChannels = null;
                }
            }
            _disposed = true;
        }
    }
}