using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using UnityEngine;

/// <summary>
/// Utility class for post-processing raw face detection outputs from the UltraFace ONNX model.
/// 
/// PIPELINE:
///   1. Filter boxes by confidence threshold per class
///   2. Apply Non-Maximum Suppression (NMS) to remove overlapping detections
///   3. Scale boxes back to original image dimensions
/// 
/// The UltraFace model outputs:
///   - confidences: [1, numBoxes, numClasses]  (class probabilities)
///   - boxes:     [1, numBoxes, 4]            (normalized bounding box coordinates)
/// </summary>
public static class faceDetectUtils
{
    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Main prediction function. Converts raw model outputs to filtered bounding boxes.
    /// </summary>
    /// <param name="width">Original image width (for scaling).</param>
    /// <param name="height">Original image height (for scaling).</param>
    /// <param name="confidences">Raw confidence tensor [1, numBoxes, numClasses].</param>
    /// <param name="boxes">Raw box tensor [1, numBoxes, 4].</param>
    /// <param name="probThreshold">Minimum confidence to keep a detection.</param>
    /// <param name="iouThreshold">IOU threshold for NMS (higher = more aggressive suppression).</param>
    /// <param name="topK">Maximum number of detections to return (-1 = unlimited).</param>
    /// <returns>Tuple of (boxes [N,4], labels [N], probabilities [N]).</returns>
    public static (int[,] Boxes, int[] Labels, float[] Probs) Predict(
        int width, 
        int height, 
        Tensor<float> confidences, 
        Tensor<float> boxes, 
        float probThreshold, 
        float iouThreshold = 0.5f, 
        int topK = -1)
    {
        // Tensors are [1, numBoxes, numClasses] and [1, numBoxes, 4]
        int numBoxes = confidences.Dimensions[1];
        int numClasses = confidences.Dimensions[2];

        List<float[,]> pickedBoxProbs = new List<float[,]>();
        List<int> pickedLabels = new List<int>();

        // Iterate classes (skip background class 0)
        for (int classIndex = 1; classIndex < numClasses; classIndex++)
        {
            // Collect candidates for this class
            List<int> maskIndices = new List<int>();
            List<float> maskedProbs = new List<float>();

            for (int i = 0; i < numBoxes; i++)
            {
                float prob = confidences[0, i, classIndex];
                if (prob > probThreshold)
                {
                    maskIndices.Add(i);
                    maskedProbs.Add(prob);
                }
            }

            if (maskIndices.Count == 0)
            {
                continue;
            }

            // Build subset boxes [N, 4]
            float[,] subsetBoxes = new float[maskIndices.Count, 4];
            for (int i = 0; i < maskIndices.Count; i++)
            {
                int idx = maskIndices[i];
                subsetBoxes[i, 0] = boxes[0, idx, 0];
                subsetBoxes[i, 1] = boxes[0, idx, 1];
                subsetBoxes[i, 2] = boxes[0, idx, 2];
                subsetBoxes[i, 3] = boxes[0, idx, 3];
            }

            // Concatenate boxes and probs [N, 5]
            float[,] boxProbs = new float[maskIndices.Count, 5];
            for (int i = 0; i < maskIndices.Count; i++)
            {
                boxProbs[i, 0] = subsetBoxes[i, 0];
                boxProbs[i, 1] = subsetBoxes[i, 1];
                boxProbs[i, 2] = subsetBoxes[i, 2];
                boxProbs[i, 3] = subsetBoxes[i, 3];
                boxProbs[i, 4] = maskedProbs[i];
            }

            // Apply Non-Maximum Suppression
            float[,] nmsResult = HardNms(boxProbs, iouThreshold, topK);

            pickedBoxProbs.Add(nmsResult);
            for (int i = 0; i < nmsResult.GetLength(0); i++)
            {
                pickedLabels.Add(classIndex);
            }
        }

        if (pickedBoxProbs.Count == 0)
        {
            return (new int[0, 4], new int[0], new float[0]);
        }

        // Concatenate all results from all classes
        int totalRows = pickedBoxProbs.Sum(x => x.GetLength(0));
        float[,] concatenated = new float[totalRows, 5];
        int row = 0;
        foreach (var bp in pickedBoxProbs)
        {
            int r = bp.GetLength(0);
            for (int i = 0; i < r; i++)
            {
                concatenated[row + i, 0] = bp[i, 0];
                concatenated[row + i, 1] = bp[i, 1];
                concatenated[row + i, 2] = bp[i, 2];
                concatenated[row + i, 3] = bp[i, 3];
                concatenated[row + i, 4] = bp[i, 4];
            }
            row += r;
        }

        // Scale by width/height and convert to int
        int[,] finalBoxes = new int[concatenated.GetLength(0), 4];
        float[] finalProbs = new float[concatenated.GetLength(0)];

        for (int i = 0; i < concatenated.GetLength(0); i++)
        {
            finalBoxes[i, 0] = (int)(concatenated[i, 0] * width);
            finalBoxes[i, 1] = (int)(concatenated[i, 1] * height);
            finalBoxes[i, 2] = (int)(concatenated[i, 2] * width);
            finalBoxes[i, 3] = (int)(concatenated[i, 3] * height);
            finalProbs[i] = concatenated[i, 4];
        }

        return (finalBoxes, pickedLabels.ToArray(), finalProbs);
    }

    // =========================================================================
    // NMS HELPERS
    // =========================================================================

    /// <summary>
    /// Computes areas of rectangles given top-left and bottom-right corners.
    /// </summary>
    private static float[] AreaOf(float[,] leftTop, float[,] rightBottom)
    {
        int n = leftTop.GetLength(0);
        float[] area = new float[n];

        for (int i = 0; i < n; i++)
        {
            float w = Math.Max(rightBottom[i, 0] - leftTop[i, 0], 0.0f);
            float h = Math.Max(rightBottom[i, 1] - leftTop[i, 1], 0.0f);
            area[i] = w * h;
        }
        return area;
    }

    /// <summary>
    /// Computes Intersection-over-Union (Jaccard index) between boxes.
    /// </summary>
    private static float[] IouOf(float[,] boxes0, float[,] boxes1, float eps = 1e-5f)
    {
        int n = boxes0.GetLength(0);
        float[] iou = new float[n];

        for (int i = 0; i < n; i++)
        {
            float overlapLeft = Math.Max(boxes0[i, 0], boxes1[0, 0]);
            float overlapTop = Math.Max(boxes0[i, 1], boxes1[0, 1]);
            float overlapRight = Math.Min(boxes0[i, 2], boxes1[0, 2]);
            float overlapBottom = Math.Min(boxes0[i, 3], boxes1[0, 3]);

            float overlapW = Math.Max(overlapRight - overlapLeft, 0.0f);
            float overlapH = Math.Max(overlapBottom - overlapTop, 0.0f);
            float overlapArea = overlapW * overlapH;

            float w0 = Math.Max(boxes0[i, 2] - boxes0[i, 0], 0.0f);
            float h0 = Math.Max(boxes0[i, 3] - boxes0[i, 1], 0.0f);
            float area0 = w0 * h0;

            float w1 = Math.Max(boxes1[0, 2] - boxes1[0, 0], 0.0f);
            float h1 = Math.Max(boxes1[0, 3] - boxes1[0, 1], 0.0f);
            float area1 = w1 * h1;

            iou[i] = overlapArea / (area0 + area1 - overlapArea + eps);
        }
        return iou;
    }

    /// <summary>
    /// Performs hard Non-Maximum Suppression.
    /// Keeps only the highest-scoring box among overlapping detections.
    /// </summary>
    private static float[,] HardNms(float[,] boxScores, float iouThreshold, int topK = -1, int candidateSize = 200)
    {
        int n = boxScores.GetLength(0);
        float[] scores = new float[n];
        float[,] boxes = new float[n, 4];

        for (int i = 0; i < n; i++)
        {
            scores[i] = boxScores[i, 4];
            boxes[i, 0] = boxScores[i, 0];
            boxes[i, 1] = boxScores[i, 1];
            boxes[i, 2] = boxScores[i, 2];
            boxes[i, 3] = boxScores[i, 3];
        }

        // Get sorted indexes (ascending order)
        int[] indexes = scores
            .Select((score, index) => new { Score = score, Index = index })
            .OrderBy(x => x.Score)
            .Select(x => x.Index)
            .ToArray();

        // Take last candidateSize elements (highest scores)
        if (indexes.Length > candidateSize)
        {
            indexes = indexes.Skip(indexes.Length - candidateSize).ToArray();
        }

        List<int> picked = new List<int>();

        while (indexes.Length > 0)
        {
            int current = indexes[indexes.Length - 1];
            picked.Add(current);

            if (topK > 0 && picked.Count == topK || indexes.Length == 1)
            {
                break;
            }

            float[,] currentBox = new float[1, 4];
            currentBox[0, 0] = boxes[current, 0];
            currentBox[0, 1] = boxes[current, 1];
            currentBox[0, 2] = boxes[current, 2];
            currentBox[0, 3] = boxes[current, 3];

            Array.Resize(ref indexes, indexes.Length - 1);

            if (indexes.Length == 0) break;

            float[,] restBoxes = new float[indexes.Length, 4];
            for (int i = 0; i < indexes.Length; i++)
            {
                restBoxes[i, 0] = boxes[indexes[i], 0];
                restBoxes[i, 1] = boxes[indexes[i], 1];
                restBoxes[i, 2] = boxes[indexes[i], 2];
                restBoxes[i, 3] = boxes[indexes[i], 3];
            }

            float[] iou = IouOf(restBoxes, currentBox);

            List<int> newIndexes = new List<int>();
            for (int i = 0; i < iou.Length; i++)
            {
                if (iou[i] <= iouThreshold)
                {
                    newIndexes.Add(indexes[i]);
                }
            }
            indexes = newIndexes.ToArray();
        }

        float[,] result = new float[picked.Count, 5];
        for (int i = 0; i < picked.Count; i++)
        {
            result[i, 0] = boxScores[picked[i], 0];
            result[i, 1] = boxScores[picked[i], 1];
            result[i, 2] = boxScores[picked[i], 2];
            result[i, 3] = boxScores[picked[i], 3];
            result[i, 4] = boxScores[picked[i], 4];
        }

        return result;
    }
}

/// <summary>
/// Fast conversion from OpenCV Mat to Unity Texture2D.
/// Handles format conversion (BGR->RGB) and vertical flip.
/// </summary>
public static class MatToTextureConverter
{
    /// <summary>
    /// Converts an OpenCV Mat to a Unity Texture2D.
    /// 
    /// PROCESS:
    ///   1. Flip vertically (OpenCV origin is top-left, Unity is bottom-left)
    ///   2. Convert color format (OpenCV uses BGR, Unity uses RGB)
    ///   3. Copy raw bytes to texture
    /// </summary>
    /// <param name="mat">Source OpenCV Mat.</param>
    /// <param name="texture">Target Unity Texture2D (must be same size).</param>
    public static void MatToTexture(Mat mat, Texture2D texture)
    {
        if (mat.Empty()) return;

        Cv2.Flip(mat, mat, FlipMode.X); // Flip around X axis (vertical flip)

        // Ensure Mat is RGB format (Unity Texture2D expects RGB/RGBA)
        Mat rgbMat = new Mat();
        if (mat.Channels() == 3)
        {
            // BGR to RGB (OpenCV default is BGR)
            Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGR2RGB);
        }
        else if (mat.Channels() == 4)
        {
            Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.BGRA2RGBA);
        }
        else if (mat.Channels() == 1)
        {
            Cv2.CvtColor(mat, rgbMat, ColorConversionCodes.GRAY2RGB);
        }
        else
        {
            rgbMat = mat.Clone();
        }

        // Get raw data pointer and copy to texture
        int width = rgbMat.Width;
        int height = rgbMat.Height;

        byte[] data = new byte[width * height * 3];
        System.Runtime.InteropServices.Marshal.Copy(rgbMat.Data, data, 0, data.Length);

        texture.LoadRawTextureData(data);
        texture.Apply(false); // false = no mipmaps update for speed

        rgbMat.Dispose();
    }
}