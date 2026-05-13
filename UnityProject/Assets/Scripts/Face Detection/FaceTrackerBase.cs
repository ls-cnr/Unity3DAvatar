using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using UnityEngine;
using Rect = OpenCvSharp.Rect;

/// <summary>
/// Shared tracked face data structure used by both IOU and SORT trackers.
/// This is the TOP-LEVEL class. For backward compatibility, Faces.cs contains
/// a nested alias 'public class TrackedFace : global::TrackedFace'.
/// 
/// STORES:
///   - Bounding boxes (original frame + resized model input coordinates)
///   - Face center position
///   - Head pose angles (pitch, yaw, roll)
///   - Frame history (circular buffer of 112x112 grayscale crops for ASD)
///   - Tracking metadata (missing frames, speaking score, etc.)
/// </summary>
public class TrackedFace
{
    // =========================================================================
    // CORE PROPERTIES
    // =========================================================================

    /// <summary>Unique track ID assigned by the tracker.</summary>
    public int Id;

    /// <summary>Bounding box in original frame coordinates (for drawing).</summary>
    public Rect OriginalRect;

    /// <summary>Bounding box in resFrame coordinates (640x480 padded) for head pose input.</summary>
    public Rect ResFrameRect;

    /// <summary>Face center X in original frame coordinates.</summary>
    public float CenterX;

    /// <summary>Face center Y in original frame coordinates.</summary>
    public float CenterY;

    /// <summary>Head pose: pitch angle in degrees (up/down).</summary>
    public float Pitch;

    /// <summary>Head pose: yaw angle in degrees (left/right).</summary>
    public float Yaw;

    /// <summary>Head pose: roll angle in degrees (tilt).</summary>
    public float Roll;

    /// <summary>Circular buffer for 25 frames of 112x112 grayscale (for ASD).</summary>
    public Queue<Mat> FrameHistory;

    // =========================================================================
    // TRACKING METADATA
    // =========================================================================

    /// <summary>Number of consecutive frames this face was not detected.</summary>
    public int FramesMissing;

    /// <summary>Last time this face was updated.</summary>
    public DateTime LastUpdated;

    /// <summary>Active speaker probability (-1 = not computed).</summary>
    public float SpeakingScore = -1f;

    /// <summary>Consecutive detection hits (SORT-specific).</summary>
    public int HitCount = 0;

    /// <summary>Frames since last update (SORT-specific).</summary>
    public int TimeSinceUpdate = 0;

    /// <summary>Whether track is confirmed (SORT manages this; always true for IOU).</summary>
    public bool IsConfirmed = true;

    // =========================================================================
    // CONSTRUCTORS
    // =========================================================================

    /// <summary>Creates a new tracked face.</summary>
    public TrackedFace(int id, Rect originalRect, Rect resRect, float centerX, float centerY)
    {
        Id = id;
        OriginalRect = originalRect;
        ResFrameRect = resRect;
        CenterX = centerX; 
        CenterY = centerY;
        FrameHistory = new Queue<Mat>(25);
        FramesMissing = 0;
        LastUpdated = DateTime.Now;
        Pitch = Yaw = Roll = 0;
    }

    /// <summary>Copy constructor for wrapper classes.</summary>
    public TrackedFace(TrackedFace other)
    {
        Id = other.Id;
        OriginalRect = other.OriginalRect;
        ResFrameRect = other.ResFrameRect;
        CenterX = other.CenterX;
        CenterY = other.CenterY;
        Pitch = other.Pitch;
        Yaw = other.Yaw;
        Roll = other.Roll;
        FramesMissing = other.FramesMissing;
        LastUpdated = other.LastUpdated;
        SpeakingScore = other.SpeakingScore;
        HitCount = other.HitCount;
        TimeSinceUpdate = other.TimeSinceUpdate;
        IsConfirmed = other.IsConfirmed;
        // Note: FrameHistory is NOT deep-copied (shallow reference)
        // The wrapper should not dispose this - ownership stays with tracker
        FrameHistory = other.FrameHistory;
    }

    // =========================================================================
    // PUBLIC METHODS
    // =========================================================================

    /// <summary>Updates face position and resets missing counter.</summary>
    public void UpdatePosition(Rect originalRect, Rect resRect, float centerX, float centerY)
    {
        OriginalRect = originalRect;
        ResFrameRect = resRect;
        CenterX = centerX;
        CenterY = centerY;
        FramesMissing = 0;
        LastUpdated = DateTime.Now;
    }

    /// <summary>Adds a new grayscale frame to the circular buffer.</summary>
    public void AddFrame(Mat grey112)
    {
        // Maintain circular buffer of 25 frames
        if (FrameHistory.Count >= 25)
        {
            Mat old = FrameHistory.Dequeue();
            old?.Dispose();
        }
        // Clone to own the memory (source mats are reused)
        FrameHistory.Enqueue(grey112.Clone());
    }

    /// <summary>Draws bounding box, pose info, and speaking score on the frame.</summary>
    public void Draw(Mat frame, float textScale, int textThickness, float resolutionScale)
    {
        // Draw bounding box
        Cv2.Rectangle(frame, OriginalRect, new Scalar(255, 128, 0), 2);

        // Draw Speaking Probability Bar on the left side
        if (SpeakingScore >= 0f)
        {
            int barWidth = Mathf.Max(4, (int)(8 * resolutionScale));
            int barHeight = OriginalRect.Height;
            int barX = Mathf.Max(0, OriginalRect.X - barWidth - 2);
            int barY = OriginalRect.Y;

            // Background bar (gray)
            Cv2.Rectangle(frame, 
                new OpenCvSharp.Rect(barX, barY, barWidth, barHeight),
                new Scalar(50, 50, 50), -1);

            // Filled portion (green to red gradient based on score)
            int fillHeight = (int)(barHeight * SpeakingScore);
            int fillY = barY + (barHeight - fillHeight); // Fill from bottom

            // Color: Red (0) -> Yellow (0.5) -> Green (1.0)
            Scalar barColor;
            if (SpeakingScore < 0.5f)
            {
                // Interpolate red to yellow
                byte green = (byte)(255 * (SpeakingScore * 2));
                barColor = new Scalar(0, green, 255);
            }
            else
            {
                // Interpolate yellow to green
                byte red = (byte)(255 * ((1 - SpeakingScore) * 2));
                barColor = new Scalar(0, 255, red);
            }

            Cv2.Rectangle(frame,
                new OpenCvSharp.Rect(barX, fillY, barWidth, fillHeight),
                barColor, -1);

            string pctText = $"{SpeakingScore:P0}";
            Cv2.PutText(frame, pctText,
                new OpenCvSharp.Point(barX - 25, barY + 15),
                HersheyFonts.HersheySimplex, textScale * 0.6f, new Scalar(255, 0, 255), textThickness);
        }

        // Draw head pose angles
        if (Math.Abs(Yaw) > 0.01f || Math.Abs(Pitch) > 0.01f || Math.Abs(Roll) > 0.01f)
        {
            string poseText = $"Y:{Yaw:F0} P:{Pitch:F0} R:{Roll:F0}";
            Cv2.PutText(frame, poseText,
                new OpenCvSharp.Point(OriginalRect.X, Mathf.Max(0, OriginalRect.Y - (int)(20 * resolutionScale))),
                HersheyFonts.HersheySimplex, textScale, new Scalar(255, 128, 255), textThickness);
        }

        // Draw speaking status text
        if (SpeakingScore >= 0f)
        {
            string statusText = SpeakingScore > 0.5f ? "SPEAKING" : "NOT SPEAKING";
            Scalar statusColor = SpeakingScore > 0.5f ? new Scalar(0, 255, 0) : new Scalar(0, 0, 255);

            int textY = OriginalRect.Bottom + (int)(25 * resolutionScale);
            int textX = OriginalRect.X;

            // Draw background for readability
            int textWidth = SpeakingScore > 0.5f ? 90 : 120; // Approximate widths
            int textHeight = (int)(20 * resolutionScale);
            Cv2.Rectangle(frame,
                new OpenCvSharp.Rect(textX, textY - textHeight, textWidth, textHeight),
                new Scalar(0, 0, 0), -1);

            Cv2.PutText(frame, statusText,
                new OpenCvSharp.Point(textX, textY),
                HersheyFonts.HersheySimplex, textScale, statusColor, textThickness);
        }
    }

    /// <summary>Disposes all frames in the history buffer.</summary>
    public void DisposeHistory()
    {
        while (FrameHistory.Count > 0)
        {
            Mat mat = FrameHistory.Dequeue();
            mat?.Dispose();
        }
    }
}

/// <summary>
/// Interface for face tracking algorithms.
/// Both IOU and SORT implement this interface for drop-in replacement.
/// </summary>
public interface IFaceTracker
{
    /// <summary>Update tracking with new detections. Call every frame.</summary>
    void Update(
        List<(int x_min, int y_min, int x_max, int y_max,
              int bbox_width, int bbox_height, float centerX, float centerY)> detections,
        Mat resFrame,
        float modelScaleX, float modelScaleY, int padX, int padY,
        int frameWidth, int frameHeight);

    /// <summary>Get currently active (visible) tracks.</summary>
    List<TrackedFace> GetActiveTracks();

    /// <summary>Get all tracks including those temporarily missing.</summary>
    List<TrackedFace> GetAllTracks();

    /// <summary>Get face data formatted for HeadPoseEstimation.Inference().</summary>
    List<(int x_min, int y_min, int x_max, int y_max,
          int bbox_width, int bbox_height, float centerX, float centerY)> GetFaceDataForHeadPose();

    /// <summary>Update head pose angles for visible faces.</summary>
    void SetHeadPoseData(List<(float pitch, float yaw, float roll)> poses);

    /// <summary>Update speaking scores for visible faces.</summary>
    void SetSpeakingScores(List<float> scores);

    /// <summary>Draw all visible faces on the frame.</summary>
    void Draw(Mat frame, float textScale, int textThickness, float resolutionScale);

    /// <summary>Get frame histories for active speaker detection.</summary>
    List<Queue<Mat>> GetFrameHistories();

    /// <summary>Clear all tracks and free memory.</summary>
    void Clear();

    /// <summary>Number of currently visible faces.</summary>
    int VisibleCount { get; }
}

/// <summary>
/// ORIGINAL IOU-based tracker extracted from Faces.cs.
/// Simple intersection-over-union matching with frame history.
/// 
/// ALGORITHM:
///   1. For each existing face, find the best-matching detection by IOU
///   2. Update matched faces with new positions
///   3. Create new faces for unmatched detections
///   4. Remove faces that haven't been seen for MAX_MISSING_FRAMES
/// </summary>
public class IouFaceTracker : IFaceTracker
{
    // =========================================================================
    // CONFIGURATION
    // =========================================================================

    private List<TrackedFace> _activeFaces;
    private int _nextId;
    private const int MAX_HISTORY = 25;
    private const int CROP_SIZE = 112;
    private const float IOU_THRESHOLD = 0.5f;
    private const int MAX_MISSING_FRAMES = 5;

    public int VisibleCount => _activeFaces.Count(f => f.FramesMissing == 0);

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    public IouFaceTracker()
    {
        _activeFaces = new List<TrackedFace>();
        _nextId = 0;
    }

    // =========================================================================
    // CORE TRACKING LOGIC
    // =========================================================================

    public void Update(
        List<(int x_min, int y_min, int x_max, int y_max,
              int bbox_width, int bbox_height, float centerX, float centerY)> detections,
        Mat resFrame,
        float modelScaleX, float modelScaleY, int padX, int padY,
        int frameWidth, int frameHeight)
    {
        // Increment missing counter for all faces
        foreach (var face in _activeFaces)
        {
            face.FramesMissing++;
        }

        bool[] detectionMatched = new bool[detections.Count];
        var sortedFaces = _activeFaces.OrderByDescending(f => f.LastUpdated).ToList();

        // Match existing faces to new detections using IOU
        foreach (var face in sortedFaces)
        {
            float bestIoU = IOU_THRESHOLD;
            int bestMatchIdx = -1;

            for (int i = 0; i < detections.Count; i++)
            {
                if (detectionMatched[i]) continue;

                var det = detections[i];
                Rect detRect = new Rect(det.x_min, det.y_min, det.bbox_width, det.bbox_height);

                float iou = CalculateIoU(face.ResFrameRect, detRect);
                if (iou > bestIoU)
                {
                    bestIoU = iou;
                    bestMatchIdx = i;
                }
            }

            if (bestMatchIdx >= 0)
            {
                var match = detections[bestMatchIdx];
                Rect newResRect = new Rect(match.x_min, match.y_min, match.bbox_width, match.bbox_height);
                Rect newOriginalRect = ResFrameToOriginal(match.x_min, match.y_min, match.bbox_width, match.bbox_height,
                                                           modelScaleX, modelScaleY, padX, padY, frameWidth, frameHeight);

                face.UpdatePosition(newOriginalRect, newResRect, match.centerX, match.centerY);

                using (Mat crop = ExtractCenterCrop(resFrame, newResRect))
                {
                    face.AddFrame(crop);
                }

                detectionMatched[bestMatchIdx] = true;
            }
        }

        // Create new faces for unmatched detections
        for (int i = 0; i < detections.Count; i++)
        {
            if (!detectionMatched[i])
            {
                var det = detections[i];
                Rect resRect = new Rect(det.x_min, det.y_min, det.bbox_width, det.bbox_height);
                Rect originalRect = ResFrameToOriginal(det.x_min, det.y_min, det.bbox_width, det.bbox_height,
                                                          modelScaleX, modelScaleY, padX, padY, frameWidth, frameHeight);

                var newFace = new TrackedFace(_nextId++, originalRect, resRect, det.centerX, det.centerY);

                using (Mat crop = ExtractCenterCrop(resFrame, resRect))
                {
                    newFace.AddFrame(crop);
                }

                _activeFaces.Add(newFace);
            }
        }

        // Remove stale faces
        _activeFaces.RemoveAll(f => 
        {
            if (f.FramesMissing > MAX_MISSING_FRAMES)
            {
                f.DisposeHistory();
                return true;
            }
            return false;
        });
    }

    // =========================================================================
    // HELPER METHODS
    // =========================================================================

    private float CalculateIoU(Rect boxA, Rect boxB)
    {
        int xLeft = Math.Max(boxA.X, boxB.X);
        int yTop = Math.Max(boxA.Y, boxB.Y);
        int xRight = Math.Min(boxA.Right, boxB.Right);
        int yBottom = Math.Min(boxA.Bottom, boxB.Bottom);

        if (xRight < xLeft || yBottom < yTop)
            return 0.0f;

        int intersectionArea = (xRight - xLeft) * (yBottom - yTop);
        int boxAArea = boxA.Width * boxA.Height;
        int boxBArea = boxB.Width * boxB.Height;

        return (float)intersectionArea / (boxAArea + boxBArea - intersectionArea + 1e-5f);
    }

    private Rect ResFrameToOriginal(int x_min, int y_min, int width, int height,
                                     float modelScaleX, float modelScaleY, int padX, int padY,
                                     int frameWidth, int frameHeight)
    {
        int disp_x_min = (int)((x_min - padX) / modelScaleX);
        int disp_y_min = (int)((y_min - padY) / modelScaleY);
        int disp_x_max = (int)((x_min + width - padX) / modelScaleX);
        int disp_y_max = (int)((y_min + height - padY) / modelScaleY);

        disp_x_min = Mathf.Max(0, disp_x_min);
        disp_y_min = Mathf.Max(0, disp_y_min);
        disp_x_max = Mathf.Min(frameWidth, disp_x_max);
        disp_y_max = Mathf.Min(frameHeight, disp_y_max);

        return new Rect(disp_x_min, disp_y_min, disp_x_max - disp_x_min, disp_y_max - disp_y_min);
    }

    private Mat ExtractCenterCrop(Mat resFrame, Rect resFrameRect)
    {
        int centerX = resFrameRect.X + resFrameRect.Width / 2;
        int centerY = resFrameRect.Y + resFrameRect.Height / 2;

        int cropX = centerX - CROP_SIZE / 2;
        int cropY = centerY - CROP_SIZE / 2;

        cropX = Math.Max(0, Math.Min(cropX, resFrame.Width - CROP_SIZE));
        cropY = Math.Max(0, Math.Min(cropY, resFrame.Height - CROP_SIZE));

        Rect cropRect = new Rect(cropX, cropY, CROP_SIZE, CROP_SIZE);

        using (Mat cropped = new Mat(resFrame, cropRect))
        {
            Mat grey = new Mat();

            if (cropped.Channels() == 3)
            {
                Cv2.CvtColor(cropped, grey, ColorConversionCodes.BGR2GRAY);
            }
            else if (cropped.Channels() == 4)
            {
                Cv2.CvtColor(cropped, grey, ColorConversionCodes.BGRA2GRAY);
            }
            else if (cropped.Channels() == 1)
            {
                grey = cropped.Clone();
            }
            else
            {
                grey = cropped.Clone();
            }

            return grey;
        }
    }

    // =========================================================================
    // IFaceTracker IMPLEMENTATION
    // =========================================================================

    public List<TrackedFace> GetActiveTracks()
    {
        return _activeFaces.Where(f => f.FramesMissing == 0).ToList();
    }

    public List<TrackedFace> GetAllTracks()
    {
        return new List<TrackedFace>(_activeFaces);
    }

    public List<(int x_min, int y_min, int x_max, int y_max,
                 int bbox_width, int bbox_height, float centerX, float centerY)> GetFaceDataForHeadPose()
    {
        var result = new List<(int, int, int, int, int, int, float, float)>();

        foreach (var face in _activeFaces.Where(f => f.FramesMissing == 0))
        {
            result.Add((
                face.ResFrameRect.X,
                face.ResFrameRect.Y,
                face.ResFrameRect.Right,
                face.ResFrameRect.Bottom,
                face.ResFrameRect.Width,
                face.ResFrameRect.Height,
                face.CenterX,
                face.CenterY
            ));
        }

        return result; 
    }

    public void SetHeadPoseData(List<(float pitch, float yaw, float roll)> poses)
    {
        var visibleFaces = _activeFaces.Where(f => f.FramesMissing == 0).ToList();

        for (int i = 0; i < poses.Count && i < visibleFaces.Count; i++)
        {
            visibleFaces[i].Pitch = poses[i].pitch;
            visibleFaces[i].Yaw = poses[i].yaw;
            visibleFaces[i].Roll = poses[i].roll;
        }
    }

    public void Draw(Mat frame, float textScale, int textThickness, float resolutionScale)
    {
        foreach (var face in _activeFaces)
        {
            if (face.FramesMissing == 0)
            {
                face.Draw(frame, textScale, textThickness, resolutionScale);
            }
        }
    }

    public List<Queue<Mat>> GetFrameHistories()
    {
        return _activeFaces
            .Where(f => f.FramesMissing == 0 && f.FrameHistory.Count > 0)
            .Select(f => f.FrameHistory)
            .ToList();
    }

    public void SetSpeakingScores(List<float> scores)
    {
        var visibleFaces = _activeFaces.Where(f => f.FramesMissing == 0).ToList();

        for (int i = 0; i < scores.Count && i < visibleFaces.Count; i++)
        {
            visibleFaces[i].SpeakingScore = scores[i];
        }
    }

    public void Clear()
    {
        foreach (var face in _activeFaces)
        {
            face.DisposeHistory();
        }
        _activeFaces.Clear();
        _nextId = 0;
    }
}