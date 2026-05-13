using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using UnityEngine;
using Rect = OpenCvSharp.Rect;

/// <summary>
/// Unified face tracking manager.
/// Can switch between IOU-based tracking (original) and SORT tracking (Kalman filter + Hungarian algorithm).
/// 
/// USAGE:
///   // Use original IOU tracker (default, same as before)
///   faces = new Faces(modelScaleX, modelScaleY, padX, padY, frameWidth, frameHeight);
///   
///   // Use SORT tracker
///   faces = new Faces(modelScaleX, modelScaleY, padX, padY, frameWidth, frameHeight, useSort: true);
/// </summary>
public class Faces : IDisposable
{
    // =========================================================================
    // BACKWARD COMPATIBILITY: Nested type alias
    // =========================================================================
    /// <summary>
    /// The original code used Faces.TrackedFace everywhere. Since TrackedFace is
    /// now a top-level class (shared between IOU and SORT trackers), this alias
    /// ensures existing code like 'Faces.TrackedFace' still compiles.
    /// </summary>
    public class TrackedFace : global::TrackedFace
    {
        public TrackedFace(int id, Rect originalRect, Rect resRect, float centerX, float centerY) 
            : base(id, originalRect, resRect, centerX, centerY) { }

        public TrackedFace(global::TrackedFace other) : base(other) { }
    }

    // =========================================================================
    // FIELDS
    // =========================================================================

    /// <summary>The active tracker implementation (IOU or SORT).</summary>
    private IFaceTracker _tracker;

    // Model scaling parameters (passed to tracker each frame)
    private float _modelScaleX;
    private float _modelScaleY;
    private int _padX;
    private int _padY;
    private int _frameWidth;
    private int _frameHeight;

    // =========================================================================
    // CONSTRUCTOR
    // =========================================================================

    /// <summary>
    /// Creates a face tracker.
    /// </summary>
    /// <param name="modelScaleX">Horizontal scaling factor from original to model input.</param>
    /// <param name="modelScaleY">Vertical scaling factor from original to model input.</param>
    /// <param name="padX">Horizontal padding added to model input.</param>
    /// <param name="padY">Vertical padding added to model input.</param>
    /// <param name="frameWidth">Original frame width.</param>
    /// <param name="frameHeight">Original frame height.</param>
    /// <param name="useSort">True to use SORT tracker, false for IOU tracker.</param>
    public Faces(float modelScaleX, float modelScaleY, int padX, int padY, int frameWidth, int frameHeight, bool useSort = false)
    {
        _modelScaleX = modelScaleX;
        _modelScaleY = modelScaleY;
        _padX = padX;
        _padY = padY;
        _frameWidth = frameWidth;
        _frameHeight = frameHeight;

        _tracker = useSort ? new SortFaceTracker() : (IFaceTracker)new IouFaceTracker();

        UnityEngine.Debug.Log($"[Faces] Using {(useSort ? "SORT" : "IOU")} tracker");
    }

    // =========================================================================
    // TRACKER SWITCHING
    // =========================================================================

    /// <summary>
    /// Switches tracker at runtime (clears existing tracks).
    /// </summary>
    /// <param name="useSort">True for SORT, false for IOU.</param>
    public void SetTracker(bool useSort)
    {
        _tracker?.Clear();
        _tracker = useSort ? new SortFaceTracker() : (IFaceTracker)new IouFaceTracker();
        UnityEngine.Debug.Log($"[Faces] Switched to {(useSort ? "SORT" : "IOU")} tracker");
    }

    // =========================================================================
    // PUBLIC PROPERTIES
    // =========================================================================

    /// <summary>Number of currently visible faces.</summary>
    public int Count => _tracker.VisibleCount;

    // =========================================================================
    // PUBLIC METHODS
    // =========================================================================

    /// <summary>
    /// Updates face tracking with new detections. Call this every frame.
    /// </summary>
    /// <param name="detections">List of detected face bounding boxes.</param>
    /// <param name="resFrame">Resized and padded frame for crop extraction.</param>
    public void Update(List<(int x_min, int y_min, int x_max, int y_max,
                            int bbox_width, int bbox_height, float centerX, float centerY)> detections,
                      Mat resFrame)
    {
        _tracker.Update(detections, resFrame, _modelScaleX, _modelScaleY, _padX, _padY, _frameWidth, _frameHeight);
    }

    /// <summary>
    /// Gets face data in format required by HeadPoseEstimation.Inference().
    /// Only returns faces that are currently detected (not missing).
    /// </summary>
    public List<(int x_min, int y_min, int x_max, int y_max,
                 int bbox_width, int bbox_height, float centerX, float centerY)> GetFaceDataForHeadPose()
    {
        return _tracker.GetFaceDataForHeadPose();
    }

    /// <summary>
    /// Updates head pose angles for faces. 
    /// Call this after HeadPoseEstimation.Inference() with the results.
    /// Results must be in same order as GetFaceDataForHeadPose() returned.
    /// </summary>
    public void SetHeadPoseData(List<(float pitch, float yaw, float roll)> poses)
    {
        _tracker.SetHeadPoseData(poses);
    }

    /// <summary>Draws all faces (bounding boxes and pose text) on the frame.</summary>
    public void Draw(Mat frame, float textScale, int textThickness, float resolutionScale)
    {
        _tracker.Draw(frame, textScale, textThickness, resolutionScale);
    }

    /// <summary>
    /// Gets list of frame histories for active speaker detection or other processing.
    /// </summary>
    public List<Queue<Mat>> GetFrameHistories()
    {
        return _tracker.GetFrameHistories();
    }

    /// <summary>Updates speaking scores for visible faces.</summary>
    public void SetSpeakingScores(List<float> scores)
    {
        _tracker.SetSpeakingScores(scores);
    }

    /// <summary>
    /// Returns list of currently tracked faces for Behavior Tree logic.
    /// </summary>
    public List<TrackedFace> GetActiveTracks()
    {
        // Cast from base TrackedFace to Faces.TrackedFace for compatibility
        return _tracker.GetActiveTracks()
            .Select(f => new TrackedFace(f))
            .ToList();
    }

    /// <summary>
    /// Gets all tracks including those temporarily missing (SORT only).
    /// </summary>
    public List<TrackedFace> GetAllTracks()
    {
        return _tracker.GetAllTracks()
            .Select(f => new TrackedFace(f))
            .ToList();
    }

    /// <summary>Clears all tracked faces and frees memory.</summary>
    public void Clear()
    {
        _tracker.Clear();
    }

    /// <summary>Disposes the tracker and frees all resources.</summary>
    public void Dispose()
    {
        Clear();
    }
}