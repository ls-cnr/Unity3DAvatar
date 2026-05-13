using UnityEngine;
using OpenCvSharp;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// Active Speaker Detection using the LRASD (Lip-Reading Active Speaker Detection) ONNX model.
/// 
/// MODEL INPUTS:
///   - visualFeature: [1, 25, 112, 112] - 25 frames of 112x112 grayscale face crops
///   - audioFeature:  [1, 100, 13]       - 100 time steps of 13 MFCC coefficients
/// 
/// MODEL OUTPUT: [25] - Speaking probability for each of 25 frames
/// 
/// PIPELINE:
///   1. Extract MFCC features from 1-second audio buffer
///   2. Stack 25 most recent face crops from track history
///   3. Run ONNX inference
///   4. Average output probabilities across all 25 frames
/// </summary>
public class ActiveSpeakerDetection : System.IDisposable
{
    // =========================================================================
    // CONSTANTS
    // =========================================================================

    /// <summary>Audio time steps expected by model (100 frames at 10ms step = 1 second).</summary>
    private const int AUDIO_TIME_STEPS = 100;

    /// <summary>Number of MFCC coefficients.</summary>
    private const int AUDIO_FEATURES = 13;

    /// <summary>Number of visual frames expected by model (1 second at 25fps).</summary>
    private const int VISUAL_FRAMES = 25;

    /// <summary>Visual frame size (square crops).</summary>
    private const int VISUAL_SIZE = 112;

    // =========================================================================
    // ONNX SESSION
    // =========================================================================

    /// <summary>ONNX Runtime inference session for active speaker detection.</summary>
    private InferenceSession activespeakerdetector;

    /// <summary>MFCC feature extraction utility.</summary>
    private ActiveSpeakerDetectUtils mfccUtils;

    private bool _disposed = false;

    // =========================================================================
    // INPUT BUFFERS
    // =========================================================================

    /// <summary>Audio feature buffer [1, 100, 13].</summary>
    private float[] audioInputBuffer = new float[1 * AUDIO_TIME_STEPS * AUDIO_FEATURES];

    /// <summary>Visual feature buffer [1, 25, 112, 112].</summary>
    private float[] visualInputBuffer = new float[1 * VISUAL_FRAMES * VISUAL_SIZE * VISUAL_SIZE];

    /// <summary>Audio tensor wrapper.</summary>
    private DenseTensor<float> audioFeatureTensor;

    /// <summary>Visual tensor wrapper.</summary>
    private DenseTensor<float> visualFeatureTensor;

    // =========================================================================
    // OUTPUT BUFFER
    // =========================================================================

    /// <summary>Reusable output buffer.</summary>
    private float[] outputBuffer = new float[25];

    // =========================================================================
    // SAFE MAT EXTRACTION BUFFERS
    // =========================================================================

    /// <summary>Buffer for continuous Mat data.</summary>
    private byte[] _visualFrameBuffer = new byte[VISUAL_SIZE * VISUAL_SIZE];

    /// <summary>Buffer for non-continuous Mat row data.</summary>
    private byte[] _visualRowBuffer = new byte[VISUAL_SIZE];

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    /// <summary>
    /// Initializes the active speaker detector.
    /// </summary>
    public ActiveSpeakerDetection(SessionOptions options, string modelsPath)
    {
        string activeSpeakerDetectorPath = modelsPath + "lrasd.onnx";
        activespeakerdetector = new InferenceSession(activeSpeakerDetectorPath, options);
        mfccUtils = new ActiveSpeakerDetectUtils(16000);

        audioFeatureTensor = new DenseTensor<float>(audioInputBuffer,
            new[] { 1, AUDIO_TIME_STEPS, AUDIO_FEATURES });
        visualFeatureTensor = new DenseTensor<float>(visualInputBuffer,
            new[] { 1, VISUAL_FRAMES, VISUAL_SIZE, VISUAL_SIZE });

        UnityEngine.Debug.Log("Active Speaker Detector ✓");
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Detects active speakers among all tracked faces.
    /// </summary>
    /// <param name="frameHistories">List of frame history queues (one per face).</param>
    /// <param name="audioBuffer">1-second audio buffer (16kHz mono).</param>
    /// <returns>Speaking scores for each face (-1 if insufficient data).</returns>
    public List<float> DetectSpeakers(List<Queue<Mat>> frameHistories, float[] audioBuffer)
    {
        var scores = new List<float>();
        if (frameHistories.Count == 0 || audioBuffer == null || audioBuffer.Length < 16000)
        {
            return Enumerable.Repeat(-1f, frameHistories.Count).ToList();
        }

        // Extract MFCC features from audio
        float[,] mfcc = mfccUtils.MFCC(audioBuffer);
        FillAudioTensor(mfcc);

        // Process each face's visual history
        foreach (var history in frameHistories)
        {
            if (history.Count < VISUAL_FRAMES)
            {
                scores.Add(-1f);
                continue;
            }

            float score = ProcessSingleFace(history);
            scores.Add(score);
        }

        return scores;
    }

    // =========================================================================
    // AUDIO PREPROCESSING
    // =========================================================================

    /// <summary>
    /// Fills the audio input tensor from MFCC features.
    /// Pads with last value if fewer than AUDIO_TIME_STEPS frames.
    /// </summary>
    private void FillAudioTensor(float[,] mfcc)
    {
        int actualFrames = mfcc.GetLength(0);
        int bufferIdx = 0;

        // Copy available frames
        for (int t = 0; t < Mathf.Min(actualFrames, AUDIO_TIME_STEPS); t++)
        {
            for (int c = 0; c < AUDIO_FEATURES; c++)
            {
                audioInputBuffer[bufferIdx++] = mfcc[t, c];
            }
        }

        // Pad remaining with last value (or 0 if empty)
        if (actualFrames < AUDIO_TIME_STEPS)
        {
            float lastValue = actualFrames > 0 ? mfcc[actualFrames - 1, 0] : 0f;
            while (bufferIdx < audioInputBuffer.Length)
            {
                audioInputBuffer[bufferIdx++] = lastValue;
            }
        }
    }

    // =========================================================================
    // VISUAL PREPROCESSING & INFERENCE
    // =========================================================================

    /// <summary>
    /// Processes a single face's frame history through the ONNX model.
    /// </summary>
    private float ProcessSingleFace(Queue<Mat> frameHistory)
    {
        Array.Clear(visualInputBuffer, 0, visualInputBuffer.Length);

        int frameIdx = 0;
        foreach (var frame in frameHistory)
        {
            if (frame.Width != VISUAL_SIZE || frame.Height != VISUAL_SIZE)
            {
                UnityEngine.Debug.LogWarning($"Frame size mismatch: expected {VISUAL_SIZE}x{VISUAL_SIZE}");
                continue;
            }

            int bufferOffset = frameIdx * VISUAL_SIZE * VISUAL_SIZE;

            // Fast path: continuous memory
            if (frame.IsContinuous())
            {
                Marshal.Copy(frame.Data, _visualFrameBuffer, 0, VISUAL_SIZE * VISUAL_SIZE);

                for (int i = 0; i < VISUAL_SIZE * VISUAL_SIZE; i++)
                {
                    visualInputBuffer[bufferOffset + i] = _visualFrameBuffer[i];
                }
            }
            // Slow path: non-continuous memory (copy row by row)
            else
            {
                for (int y = 0; y < VISUAL_SIZE; y++)
                {
                    using (var row = frame.Row(y))
                    {
                        Marshal.Copy(row.Data, _visualRowBuffer, 0, VISUAL_SIZE);
                        int rowOffset = y * VISUAL_SIZE;
                        for (int x = 0; x < VISUAL_SIZE; x++)
                        {
                            visualInputBuffer[bufferOffset + rowOffset + x] = _visualRowBuffer[x];
                        }
                    }
                }
            }

            frameIdx++;
            if (frameIdx >= VISUAL_FRAMES) break;
        }

        // Create ONNX inputs
        var inputs = new NamedOnnxValue[]
        {
            NamedOnnxValue.CreateFromTensor("visualFeature", visualFeatureTensor),
            NamedOnnxValue.CreateFromTensor("audioFeature", audioFeatureTensor)
        };

        // Run inference
        try
        {
            using (var results = activespeakerdetector.Run(inputs))
            {
                var output = results[0].AsTensor<float>();

                // Average probabilities across all 25 output frames
                float sum = 0f;
                int count = (int)output.Length;

                for (int i = 0; i < count; i++)
                {
                    sum += output[i];
                }

                return count > 0 ? sum / count : 0f;
            }
        }
        catch (System.Exception ex)
        {
            UnityEngine.Debug.LogError($"Inference error: {ex.Message}");
            return 0f;
        }
    }

    // =========================================================================
    // DISPOSAL
    // =========================================================================

    public void Dispose()
    {
        Dispose(true);
        System.GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                activespeakerdetector?.Dispose();
                activespeakerdetector = null;
                mfccUtils = null;
            }
            _disposed = true;
        }
    }
}