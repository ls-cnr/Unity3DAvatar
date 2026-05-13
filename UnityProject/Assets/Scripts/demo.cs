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

/// <summary>
/// Main application controller that orchestrates all ML pipelines:
///   1. Face Detection (UltraFace)
///   2. Face Tracking (SORT or IOU)
///   3. Head Pose Estimation (6DRepNet)
///   4. Active Speaker Detection (LRASD)
///   5. Engagement Logic
///   6. Avatar Control
/// 
/// PIPELINE PER FRAME:
///   Capture -> Detect -> Track -> Estimate Pose -> Detect Speaker -> 
///   Update Engagement -> Control Avatar -> Render
/// </summary>
public class demoMain : MonoBehaviour
{
    // =========================================================================
    // INSPECTOR REFERENCES
    // =========================================================================

    [Header("Avatar Controller")]
    [Tooltip("Avatar that will look at the engaged user.")]
    public AvatarHeadController avatarController;

    [Header("Camera")]
    [Tooltip("Which camera device to use (0 = default).")]
    public int cameraIndex = 0;

    [Tooltip("UI RawImage to display the camera feed.")]
    public RawImage displayImage;

    [Header("Toggles")]
    [Tooltip("Enable head pose estimation (6DRepNet).")]
    public bool enableHeadPoseEstimation = true;

    [Tooltip("Enable active speaker detection (LRASD).")]
    public bool enableActiveSpeakerDetection = true;

    [Header("AudioCapture Component")]
    [Tooltip("Reference to AudioCapture component (auto-detected if null).")]
    public AudioCapture audioCapture;

    // =========================================================================
    // VIDEO CAPTURE
    // =========================================================================

    /// <summary>OpenCV video capture from webcam.</summary>
    private VideoCapture capture;

    /// <summary>Unity texture for displaying the camera feed.</summary>
    private Texture2D cameraTexture;

    /// <summary>True when the processing loop is running.</summary>
    private bool isRunning = false;

    // =========================================================================
    // REUSABLE MATS (avoid per-frame allocation)
    // =========================================================================

    private Mat reusableCaptureFrame = new Mat();
    private Mat reusableResizedCapture = new Mat();
    private Mat reusableScaledMat = new Mat();

    // =========================================================================
    // DISPLAY PARAMETERS
    // =========================================================================

    private float textScale;
    private int boxThickness;
    private int textThickness;
    private float resolutionScale;
    private int statsHeight;
    private int statsWidth;
    private int fontHeight;
    private Vector2Int statsPosition = new Vector2Int(10, 30);

    // =========================================================================
    // MODEL SCALING (camera -> model input coordinates)
    // =========================================================================

    private float displayScaleX;
    private float displayScaleY;
    private float modelScaleX;
    private float modelScaleY;
    private int modelInputWidth = 640;
    private int modelInputHeight = 480;
    private int padX;
    private int padY;

    // =========================================================================
    // PERFORMANCE TRACKING
    // =========================================================================

    private Stopwatch frameTimer = new Stopwatch();
    private float[] frameTimeHistory = new float[60];
    private int frameTimeIndex = 0;
    private string performanceString = "  ";

    // =========================================================================
    // ML MODULE REFERENCES
    // =========================================================================

    /// <summary>Face detection (UltraFace ONNX).</summary>
    private FaceDetection faceDetector;

    /// <summary>Face tracking (SORT or IOU).</summary>
    private Faces faces;

    /// <summary>Head pose estimation (6DRepNet ONNX).</summary>
    private HeadPoseEstimation headPoseEstimator;

    /// <summary>Active speaker detection (LRASD ONNX).</summary>
    private ActiveSpeakerDetection activeSpeakerDetector;

    // =========================================================================
    // ENGAGEMENT MODULE
    // =========================================================================

    /// <summary>User engagement state manager.</summary>
    private EngagementModule engagementModule;

    // =========================================================================
    // UNITY LIFECYCLE: START
    // =========================================================================

    /// <summary>
    /// Initializes camera, ML models, and engagement system.
    /// </summary>
    void Start()
    {
        // Initialize camera
        capture = new VideoCapture(cameraIndex);
        if (!capture.IsOpened())
        {
            UnityEngine.Debug.LogError($"Camera {cameraIndex} initialization failed.");
            return;
        }

        // Calculate scaling from camera resolution to model input (640x480)
        // Uses uniform scaling with letterboxing (black bars)
        float scaleX = modelInputWidth / (float)capture.FrameWidth;
        float scaleY = modelInputHeight / (float)capture.FrameHeight;
        float uniformScale = Math.Min(scaleX, scaleY);

        modelScaleX = uniformScale;
        modelScaleY = uniformScale;

        int scaledWidth = (int)(capture.FrameWidth * uniformScale);
        int scaledHeight = (int)(capture.FrameHeight * uniformScale);
        padX = (modelInputWidth - scaledWidth) / 2;
        padY = (modelInputHeight - scaledHeight) / 2;

        UnityEngine.Debug.Log($"Camera: {capture.FrameWidth}x{capture.FrameHeight},  " +
                            $"Model scale: {modelScaleX:F3},  " +
                            $"Scaled: {scaledWidth}x{scaledHeight},  " +
                            $"Pad: {padX},{padY}");

        // Calculate display scaling based on 1080p reference
        resolutionScale = Mathf.Max(capture.FrameWidth, capture.FrameHeight) / 1080f;
        textScale = Mathf.Max(0.5f, 0.6f * resolutionScale);
        boxThickness = Mathf.Max(1, (int)(2 * resolutionScale));
        textThickness = Mathf.Max(1, (int)(2 * resolutionScale));
        statsHeight = (int)(25 * resolutionScale);
        statsWidth = (int)(280 * resolutionScale); 
        fontHeight = (int)(20 * resolutionScale);

        // Setup display texture
        cameraTexture = new Texture2D(capture.FrameWidth, capture.FrameHeight, TextureFormat.RGB24, false);
        displayImage.texture = cameraTexture;

        // Load ML models
        UnityEngine.Debug.Log("Loading models...");
        var options = new SessionOptions();
        options.GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL;
        options.LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_WARNING;

        // Optimize threading for the CPU
        int cores = SystemInfo.processorCount;
        options.IntraOpNumThreads = Mathf.Max(1, cores / 2);
        options.InterOpNumThreads = 1;
        options.ExecutionMode = ExecutionMode.ORT_SEQUENTIAL;

        string modelsPath = Application.streamingAssetsPath + "/Models/";

        // Try CUDA first, fall back to CPU
        if (OnnxRuntimeInitializer.CudaDeviceAvailable)
        {
            try
            {
                options.AppendExecutionProvider_CUDA(0);
                UnityEngine.Debug.Log("✓ CUDA Execution Provider initialized");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning(
                    $"CUDA provider failed to initialize: {ex.Message}.  " +
                     "Falling back to CPU...");
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }
        else
        {
            UnityEngine.Debug.Log("No CUDA device detected. Using CPU Execution Provider.");
            options.AppendExecutionProvider_CPU();
        }

        // Initialize face detection and tracking
        faceDetector = new FaceDetection(options, modelScaleX, modelScaleY, padX, padY, 
                                          modelsPath, capture.FrameWidth, capture.FrameHeight, boxThickness);
        faces = new Faces(modelScaleX, modelScaleY, padX, padY, capture.FrameWidth, capture.FrameHeight, useSort: true);

        // Initialize optional modules
        if (enableHeadPoseEstimation)
        {
            headPoseEstimator = new HeadPoseEstimation(options, modelsPath, modelScaleX, modelScaleY, 
                                                        padX, padY, textScale, textThickness, resolutionScale);
        }
        else
        {
            UnityEngine.Debug.Log("Head Pose Estimation: Not Activated");
        }

        if (enableActiveSpeakerDetection)
        {
            activeSpeakerDetector = new ActiveSpeakerDetection(options, modelsPath);
            if (audioCapture == null)
            {
                audioCapture = GetComponent<AudioCapture>();
                if (audioCapture == null)
                {
                    UnityEngine.Debug.LogError("AudioCapture component not found! Disabling ASD.");
                    enableActiveSpeakerDetection = false;
                }
            }
        }
        else
        {
            UnityEngine.Debug.Log("Active Speaker Detection: Not Activated");
        }

        // Initialize Engagement Module
        engagementModule = FindFirstObjectByType<EngagementModule>();
        if (engagementModule == null)
        {
            GameObject moduleObj = new GameObject("EngagementModule");
            engagementModule = moduleObj.AddComponent<EngagementModule>();
            UnityEngine.Debug.Log("[Demo] Created EngagementModule");
        }

        // Subscribe to engagement events for robot actions
        engagementModule.OnUserEngaged += (id) => { 
            UnityEngine.Debug.Log($"[Robot] Turning attention to User {id}"); 
        };

        engagementModule.OnUserDisengaged += () => { 
            UnityEngine.Debug.Log("[Robot] No user engaged. Idle behavior."); 
        };

        isRunning = true;
        frameTimer.Start();
    }

    // =========================================================================
    // UNITY LIFECYCLE: UPDATE (MAIN LOOP)
    // =========================================================================

    /// <summary>
    /// Main processing loop executed every frame.
    /// </summary>
    void Update()
    {
        if (!isRunning)
        {
            UnityEngine.Debug.Log("Demo not running. Please close and restart the demo.");
            return;
        }

        frameTimer.Restart();

        // --- STEP 1: Capture frame from camera ---
        capture.Grab();
        capture.Retrieve(reusableCaptureFrame);

        if (reusableCaptureFrame.Empty())
        {
            UnityEngine.Debug.Log("Was not able to get a frame from camera.");
            return;
        }

        // --- STEP 2: Resize and pad for model input ---
        Cv2.Resize(reusableCaptureFrame, reusableScaledMat, new Size(), modelScaleX, modelScaleY);
        Cv2.CopyMakeBorder(reusableScaledMat, reusableResizedCapture,
                        padY, padY, padX, padX,
                        BorderTypes.Constant, new Scalar(0, 0, 0));

        // --- STEP 3: Detect faces ---
        var detections = faceDetector.Inference(reusableCaptureFrame, reusableResizedCapture);
        faces.Update(detections, reusableResizedCapture);

        // --- STEP 4: Get face data for downstream processing ---
        var faceData = faces.GetFaceDataForHeadPose();
        int activeFaceCount = faces.Count;

        // --- STEP 5: Estimate head pose ---
        if (activeFaceCount > 0 && enableHeadPoseEstimation)
        {
            faces.SetHeadPoseData(headPoseEstimator.Inference(faceData, activeFaceCount, 
                                                reusableCaptureFrame, reusableResizedCapture));
        }

        // --- STEP 6: Detect active speakers ---
        if (activeFaceCount > 0 && enableActiveSpeakerDetection && audioCapture != null && audioCapture.IsBufferFull())
        {
            float[] audioBuffer = audioCapture.getBuffer();
            var frameHistories = faces.GetFrameHistories();

            if (frameHistories.Count > 0)
            {
                faces.SetSpeakingScores(activeSpeakerDetector.DetectSpeakers(frameHistories, audioBuffer));
            }
        }

        // --- STEP 7: Update engagement state ---
        if (engagementModule != null)
        {
            var activeTracks = faces.GetActiveTracks();
            engagementModule.UpdateEngagement(activeTracks, Time.deltaTime);

            // Visualize engagement state and control avatar
            if (engagementModule.IsEngaged)
            {
                // Draw engagement indicator
                Cv2.PutText(reusableCaptureFrame, $"ENGAGED: ID {engagementModule.EngagedTrackId}", 
                    new OpenCvSharp.Point(10, 100), 
                    HersheyFonts.HersheySimplex, textScale, new Scalar(0, 255, 0), textThickness);

                // Highlight engaged user with green box
                var engagedFace = engagementModule.GetEngagedFace();
                if(engagedFace != null)
                {
                    Cv2.Rectangle(reusableCaptureFrame, engagedFace.OriginalRect, new Scalar(0, 255, 0), 3);

                    // Send face position to avatar
                    if (avatarController != null)
                    {
                        float normX = engagedFace.CenterX / capture.FrameWidth;
                        float normY = engagedFace.CenterY / capture.FrameHeight;
                        avatarController.LookAtFace(normX, normY);
                    }
                }
            }
            else
            {
                // No one engaged - return avatar to idle
                if (avatarController != null) avatarController.ReturnToCenter();
            }
        }

        // --- STEP 8: Draw all faces and UI ---
        faces.Draw(reusableCaptureFrame, textScale, textThickness, resolutionScale);
        UpdatePerformanceStats(faces.Count);
        DrawStatsOnFrame(reusableCaptureFrame);

        // --- STEP 9: Display on screen ---
        MatToTextureConverter.MatToTexture(reusableCaptureFrame, cameraTexture);
        displayImage.texture = cameraTexture;
    }

    // =========================================================================
    // PERFORMANCE UI
    // =========================================================================

    /// <summary>
    /// Updates the rolling average frame time and FPS.
    /// </summary>
    private void UpdatePerformanceStats(int numFaces)
    {
        float frameTimeMs = (float)frameTimer.Elapsed.TotalMilliseconds;
        frameTimeHistory[frameTimeIndex] = frameTimeMs;
        frameTimeIndex = (frameTimeIndex + 1) % frameTimeHistory.Length;

        float sum = 0f;
        int count = 0;
        for (int i = 0; i < frameTimeHistory.Length; i++)
        {
            if (frameTimeHistory[i] > 0)
            {
                sum += frameTimeHistory[i];
                count++;
            }
        }

        float avgFrameTime = count > 0 ? sum / count : frameTimeMs;
        float currentFPS = avgFrameTime > 0 ? 1000f / avgFrameTime : 0f;
        performanceString = $"{frameTimeMs:F1}ms | {currentFPS:F0} FPS | {numFaces} faces";
    }

    /// <summary>
    /// Draws performance stats with shadow for readability.
    /// </summary>
    private void DrawStatsOnFrame(Mat frame)
    {
        // Shadow
        Cv2.PutText(frame, performanceString,
            new OpenCvSharp.Point(statsPosition.x + 1, statsPosition.y + 1),
            HersheyFonts.HersheySimplex, textScale, new Scalar(0, 0, 0), textThickness);

        // Text
        Cv2.PutText(frame, performanceString,
            new OpenCvSharp.Point(statsPosition.x, statsPosition.y),
            HersheyFonts.HersheySimplex, textScale, new Scalar(0, 255, 255), textThickness);
    }

    // =========================================================================
    // UNITY LIFECYCLE: ONDESTROY
    // =========================================================================

    /// <summary>
    /// Cleans up all resources when the application exits.
    /// </summary>
    void OnDestroy()
    {
        // Stop processing
        isRunning = false;

        // Dispose ML modules
        faces?.Dispose();
        activeSpeakerDetector?.Dispose();
        activeSpeakerDetector = null;
        headPoseEstimator?.Dispose();
        headPoseEstimator = null;
        faceDetector?.Dispose();
        faceDetector = null;

        // Dispose OpenCV mats
        reusableCaptureFrame?.Dispose();
        reusableScaledMat?.Dispose();
        reusableResizedCapture?.Dispose();

        // Dispose Unity textures
        if (cameraTexture != null)
        {
            Destroy(cameraTexture);
            cameraTexture = null;
        }
        if (displayImage != null)
        {
            displayImage.texture = null;
        }

        // Release camera
        if (capture != null)
        {
            capture.Release();
            capture.Dispose();
            capture = null;
        }

        Cv2.DestroyAllWindows();

        // Force garbage collection
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }
}