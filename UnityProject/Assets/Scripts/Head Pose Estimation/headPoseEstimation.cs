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
using System.Runtime.InteropServices;

/// <summary>
/// Head pose estimation using the 6DRepNet ONNX model.
/// 
/// MODEL INPUT:  Batch of 224x224 RGB face crops, normalized with ImageNet stats
/// MODEL OUTPUT: 6D rotation representation [batch, 3, 3] (converted to Euler angles)
/// 
/// PIPELINE:
///   1. Extract face crops from resized frame
///   2. Resize to 224x224 and normalize
///   3. Run ONNX inference
///   4. Convert 6D representation to rotation matrix
///   5. Extract Euler angles (pitch, yaw, roll)
/// </summary>
public class HeadPoseEstimation : IDisposable
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// <summary>Maximum batch size for head pose inference.</summary>
    private const int maxbatch = 10;

    /// <summary>Input image size for 6DRepNet.</summary>
    private const int InputSize = 224;

    // =========================================================================
    // ONNX SESSION
    // =========================================================================

    /// <summary>ONNX Runtime inference session for head pose estimation.</summary>
    private InferenceSession headPoseEstimator;

    // =========================================================================
    // INPUT BUFFERS
    // =========================================================================

    /// <summary>Pinned buffer for batched model input [maxbatch, 3, 224, 224].</summary>
    private PinnedBuffer<float> _pinnedInputBuffer;

    /// <summary>Managed array view of pinned buffer.</summary>
    private float[] headPoseEstimationInputBuffer;

    /// <summary>ONNX tensor wrapping the input buffer.</summary>
    private DenseTensor<float> headPoseEstimationTensor;

    // =========================================================================
    // REUSABLE MATS
    // =========================================================================

    /// <summary>Channel split buffers for normalization.</summary>
    private Mat[] reusableFaceChannels = new Mat[3];

    /// <summary>Per-batch-item channel wrappers pointing into pinned buffer.</summary>
    private Mat[][] headPoseWrappers = new Mat[maxbatch][];

    /// <summary>Reusable mats for preprocessing pipeline.</summary>
    private Mat reusableFaceCrop = new Mat();
    private OpenCvSharp.Rect reusableROI = new OpenCvSharp.Rect();
    private Mat reusableResizedFaceCrop = new Mat();
    private Mat reusableNormalizedFaceCrop = new Mat();

    // =========================================================================
    // COORDINATE & DISPLAY PARAMETERS
    // =========================================================================

    private float modelScaleX;
    private float modelScaleY;
    private int padX;
    private int padY;
    private float textScale;
    private int textThickness;
    private float resolutionScale;

    // =========================================================================
    // DISPOSAL
    // =========================================================================

    private bool _disposed = false;

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    /// <summary>
    /// Initializes the head pose estimator.
    /// </summary>
    public HeadPoseEstimation(SessionOptions options, string modelsPath, float xscale, float yscale,
                              int xpad, int ypad, float txtscale, int txtThick, float resscale)
    {
        // Store parameters
        modelScaleX = xscale;
        modelScaleY = yscale;
        padX = xpad;
        padY = ypad;
        textScale = txtscale;
        textThickness = txtThick;
        resolutionScale = resscale;

        // Load ONNX model
        string headPoseEstimatorPath = modelsPath + "6drepnet_dynamic.onnx";
        headPoseEstimator = new InferenceSession(headPoseEstimatorPath, options);
        UnityEngine.Debug.Log("Head Pose Estimator ✓");

        // Allocate pinned input buffer for zero-copy
        _pinnedInputBuffer = new PinnedBuffer<float>(maxbatch * 3 * InputSize * InputSize);
        headPoseEstimationInputBuffer = _pinnedInputBuffer.Array;

        // Initialize per-batch-item channel wrappers
        for (int i = 0; i < maxbatch; i++)
        {
            headPoseWrappers[i] = new Mat[3];
        }

        // Create OpenCV Mat wrappers pointing into pinned buffer
        // Layout: [batch, channels=3, height=224, width=224]
        // Channel order: R, G, B (matching PyTorch's expected input)
        for (int count = 0; count < maxbatch; count++)
        {
            int baseOffset = count * 3 * InputSize * InputSize;

            // Red channel (index 0 in PyTorch, stored last in buffer)
            headPoseWrappers[count][0] = Mat.FromPixelData(
                InputSize, InputSize, MatType.CV_32FC1,
                _pinnedInputBuffer.ElementOffset(baseOffset + (InputSize * InputSize * 2)),
                InputSize * sizeof(float));

            // Green channel (index 1)
            headPoseWrappers[count][1] = Mat.FromPixelData(
                InputSize, InputSize, MatType.CV_32FC1,
                _pinnedInputBuffer.ElementOffset(baseOffset + (InputSize * InputSize)),
                InputSize * sizeof(float));

            // Blue channel (index 2, stored first in buffer)
            headPoseWrappers[count][2] = Mat.FromPixelData(
                InputSize, InputSize, MatType.CV_32FC1,
                _pinnedInputBuffer.ElementOffset(baseOffset),
                InputSize * sizeof(float));
        }

        reusableFaceChannels[0] = new Mat();
        reusableFaceChannels[1] = new Mat();
        reusableFaceChannels[2] = new Mat();
    }

    // =========================================================================
    // INFERENCE
    // =========================================================================

    /// <summary>
    /// Estimates head pose for all detected faces.
    /// </summary>
    /// <param name="faceData">Face bounding boxes from FaceDetection.</param>
    /// <param name="numFaces">Number of faces to process.</param>
    /// <param name="frame">Original frame (for reference).</param>
    /// <param name="resFrame">Resized frame containing face regions.</param>
    /// <returns>List of (pitch, yaw, roll) in degrees for each face.</returns>
    public List<(float p_pred_deg, float y_pred_deg, float r_pred_deg)> Inference(
        List<(int x_min, int y_min, int x_max, int y_max,
              int bbox_width, int bbox_height, float centerX, float centerY)> faceData,
        int numFaces, Mat frame, Mat resFrame)
    {
        // Create tensor view over the pinned buffer
        var memory = new Memory<float>(
            headPoseEstimationInputBuffer, 0, (numFaces * 3 * InputSize * InputSize));
        headPoseEstimationTensor = new DenseTensor<float>(memory, new[] { numFaces, 3, InputSize, InputSize });

        // Preprocess each face crop
        for (int i = 0; i < numFaces; i++)
        {
            var (x_min, y_min, x_max, y_max, bbox_width, bbox_height, centerX, centerY) = faceData[i];

            // Extract face region from resized frame
            reusableROI = new OpenCvSharp.Rect(x_min, y_min, bbox_width, bbox_height);
            reusableFaceCrop = resFrame[reusableROI];

            // Resize to model input size
            Cv2.Resize(reusableFaceCrop, reusableResizedFaceCrop, new Size(InputSize, InputSize));

            // Normalize to [0, 1]
            reusableResizedFaceCrop.ConvertTo(reusableNormalizedFaceCrop, MatType.CV_32FC3, 1.0f / 255.0f);

            // Split into channels
            Cv2.Split(reusableNormalizedFaceCrop, out reusableFaceChannels);

            // Normalize with ImageNet mean/std and copy to pinned buffer
            // BGR -> RGB conversion happens via channel reordering in the wrappers
            // mean=[0.485, 0.456, 0.406], std=[0.229, 0.224, 0.225]
            reusableFaceChannels[0].ConvertTo(
                headPoseWrappers[i][2], MatType.CV_32FC1, 1.0f / 0.225f, -0.406f / 0.225f);  // B -> R
            reusableFaceChannels[1].ConvertTo(
                headPoseWrappers[i][1], MatType.CV_32FC1, 1.0f / 0.224f, -0.456f / 0.224f);  // G -> G
            reusableFaceChannels[2].ConvertTo(
                headPoseWrappers[i][0], MatType.CV_32FC1, 1.0f / 0.229f, -0.485f / 0.229f);  // R -> B
        }

        // Run ONNX inference
        var headPoseData = new List<(float p_pred_deg, float y_pred_deg, float r_pred_deg)>();
        var headPoseInputs = new NamedOnnxValue[]
        {
            NamedOnnxValue.CreateFromTensor("input", headPoseEstimationTensor)
        };

        using (var poseResults = headPoseEstimator.Run(headPoseInputs))
        {
            var outputTensor = poseResults[0].AsTensor<float>();

            // Convert 6D representation to Euler angles for each face
            for (int i = 0; i < numFaces; i++)
            {
                // Build rotation matrix from model output
                Matrix4x4 R_matrix = Matrix4x4.identity;
                R_matrix[0, 0] = outputTensor[i, 0, 0];
                R_matrix[0, 1] = outputTensor[i, 0, 1];
                R_matrix[0, 2] = outputTensor[i, 0, 2];
                R_matrix[1, 0] = outputTensor[i, 1, 0];
                R_matrix[1, 1] = outputTensor[i, 1, 1];
                R_matrix[1, 2] = outputTensor[i, 1, 2];
                R_matrix[2, 0] = outputTensor[i, 2, 0];
                R_matrix[2, 1] = outputTensor[i, 2, 1];
                R_matrix[2, 2] = outputTensor[i, 2, 2];

                // Extract Euler angles
                Vector3 euler = headPoseEstimationUtils.ComputeEulerAnglesFromRotationMatrices(R_matrix);
                float p_pred_deg = euler.x * Mathf.Rad2Deg;
                float y_pred_deg = euler.y * Mathf.Rad2Deg;
                float r_pred_deg = euler.z * Mathf.Rad2Deg;

                headPoseData.Add((p_pred_deg, y_pred_deg, r_pred_deg));
            }
            return headPoseData;
        }
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
                headPoseEstimator?.Dispose();
                headPoseEstimator = null;
                headPoseEstimationTensor = null;

                if (headPoseWrappers != null)
                {
                    foreach (var channelArray in headPoseWrappers)
                    {
                        if (channelArray != null)
                        {
                            foreach (var mat in channelArray)
                            {
                                mat?.Dispose();
                            }
                        }
                    }
                    headPoseWrappers = null;
                }

                if (reusableFaceChannels != null)
                {
                    foreach (var mat in reusableFaceChannels)
                    {
                        mat?.Dispose();
                    }
                    reusableFaceChannels = null;
                }

                reusableFaceCrop?.Dispose();
                reusableFaceCrop = null;

                reusableResizedFaceCrop?.Dispose();
                reusableResizedFaceCrop = null;

                reusableNormalizedFaceCrop?.Dispose();
                reusableNormalizedFaceCrop = null;

                _pinnedInputBuffer?.Dispose();
                _pinnedInputBuffer = null;
                headPoseEstimationInputBuffer = null;
            }
            _disposed = true;
        }
    }
}