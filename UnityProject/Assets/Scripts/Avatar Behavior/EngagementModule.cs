using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// Singleton module that manages user engagement state.
/// 
/// ENGAGEMENT POLICY:
///   A user becomes ENGAGED when they are:
///   1. Looking at the robot (head pose within thresholds)
///   2. Speaking (speaking score above threshold)
///   
///   A user becomes DISENGAGED when:
///   1. They leave the frame (no face detected for timeout seconds)
///   2. They stop looking AND speaking for timeout seconds
/// 
/// USAGE:
///   Access from anywhere via: EngagementModule.Instance
///   Subscribe to events: OnUserEngaged, OnUserDisengaged
/// </summary>
public class EngagementModule : MonoBehaviour
{
    // =========================================================================
    // SINGLETON PATTERN
    // =========================================================================

    /// <summary>Global access point to the engagement module.</summary>
    public static EngagementModule Instance { get; private set; }

    // =========================================================================
    // BLACKBOARD STATE (Public Read-Only)
    // =========================================================================

    /// <summary>ID of the currently engaged user, or null if no one is engaged.</summary>
    public int? EngagedTrackId { get; private set; } = null;

    /// <summary>True if any user is currently engaged.</summary>
    public bool IsEngaged => EngagedTrackId.HasValue;

    /// <summary>Timer counting seconds since engagement conditions were last met.</summary>
    public float DisengagementTimer { get; private set; } = 0f;

    // =========================================================================
    // CONFIGURATION PARAMETERS
    // =========================================================================

    [Header("Head Pose Thresholds (Degrees)")]
    [Tooltip("Maximum yaw angle to consider 'looking at robot'.")]
    public float MaxYawThreshold = 30f;

    [Tooltip("Maximum pitch angle to consider 'looking at robot'.")]
    public float MaxPitchThreshold = 30f;

    [Header("Active Speaker Threshold")]
    [Tooltip("Minimum speaking score to consider 'speaking'.")]
    public float SpeakingScoreThreshold = 0.5f;

    [Header("Timing (Seconds)")]
    [Tooltip("Seconds of non-engagement before disengaging.")]
    public float DisengagementTimeout = 3.0f;

    // =========================================================================
    // EVENTS
    // =========================================================================

    /// <summary>Triggered when a new user becomes engaged. Parameter: track ID.</summary>
    public event Action<int> OnUserEngaged;

    /// <summary>Triggered when the engaged user disengages.</summary>
    public event Action OnUserDisengaged;

    // =========================================================================
    // CACHED DATA
    // =========================================================================

    /// <summary>Full face data of the currently engaged user (for external access).</summary>
    private Faces.TrackedFace _cachedEngagedFace;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    /// <summary>
    /// Initializes the singleton instance.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject); // Optional: persists across scenes
    }

    // =========================================================================
    // MAIN UPDATE LOOP
    // =========================================================================

    /// <summary>
    /// Main tick function. Call this from demo.cs Update() every frame.
    /// 
    /// LOGIC:
    ///   If engaged:
    ///     - Check if engaged user still meets criteria
    ///     - If not, increment disengagement timer
    ///     - If timer exceeds timeout, disengage
    ///   
    ///   If not engaged:
    ///     - Check all faces for engagement criteria
    ///     - Engage the first face that meets criteria
    /// </summary>
    public void UpdateEngagement(List<Faces.TrackedFace> activeFaces, float deltaTime)
    {
        if (activeFaces == null || activeFaces.Count == 0)
        {
            // No faces visible - check timeout
            if (EngagedTrackId.HasValue)
            {
                DisengagementTimer += deltaTime;
                if (DisengagementTimer >= DisengagementTimeout)
                {
                    Disengage();
                }
            }
            _cachedEngagedFace = null;
            return;
        }

        // --- BRANCH 1: Disengagement Policy ---
        if (EngagedTrackId.HasValue)
        {
            var engagedFace = activeFaces.Find(f => f.Id == EngagedTrackId.Value);

            if (engagedFace == null)
            {
                // Engaged user disappeared
                Disengage();
            }
            else
            {
                // Check if still engaged
                bool isLooking = IsLookingAtRobot(engagedFace);
                bool isSpeaking = IsSpeaking(engagedFace);

                if (isLooking && isSpeaking)
                {
                    // Still engaged - reset timer
                    DisengagementTimer = 0f;
                }
                else
                {
                    // Losing engagement - increment timer
                    DisengagementTimer += deltaTime;
                    if (DisengagementTimer >= DisengagementTimeout)
                    {
                        Disengage();
                    }
                }

                // Cache for external access
                _cachedEngagedFace = engagedFace;
            }
        }

        // --- BRANCH 2: Engagement Policy ---
        if (!EngagedTrackId.HasValue)
        {
            foreach (var face in activeFaces)
            {
                if (IsLookingAtRobot(face) && IsSpeaking(face))
                {
                    Engage(face.Id);
                    _cachedEngagedFace = face;
                    break;
                }
            }
        }
    }

    // =========================================================================
    // ENGAGEMENT CRITERIA
    // =========================================================================

    /// <summary>
    /// Checks if the user is looking at the robot based on head pose.
    /// </summary>
    private bool IsLookingAtRobot(Faces.TrackedFace face)
    {
        return Mathf.Abs(face.Yaw) < MaxYawThreshold && 
               Mathf.Abs(face.Pitch) < MaxPitchThreshold;
    }

    /// <summary>
    /// Checks if the user is speaking based on active speaker detection score.
    /// </summary>
    private bool IsSpeaking(Faces.TrackedFace face)
    {
        return face.SpeakingScore >= SpeakingScoreThreshold;
    }

    // =========================================================================
    // STATE TRANSITIONS
    // =========================================================================

    /// <summary>Transitions to engaged state.</summary>
    private void Engage(int trackId)
    {
        EngagedTrackId = trackId;
        DisengagementTimer = 0f;
        Debug.Log($"[EngagementModule] ENGAGED User ID: {trackId}");
        OnUserEngaged?.Invoke(trackId);
    }

    /// <summary>Transitions to disengaged state.</summary>
    private void Disengage()
    {
        int? oldId = EngagedTrackId;
        EngagedTrackId = null;
        DisengagementTimer = 0f;
        _cachedEngagedFace = null;
        Debug.Log($"[EngagementModule] DISENGAGED User ID: {oldId}");
        OnUserDisengaged?.Invoke();
    }

    // =========================================================================
    // PUBLIC API FOR OTHER MODULES
    // =========================================================================

    /// <summary>
    /// Gets the full tracked face data of the engaged user.
    /// Returns null if no one is engaged.
    /// </summary>
    public Faces.TrackedFace GetEngagedFace()
    {
        return _cachedEngagedFace;
    }

    /// <summary>
    /// Gets the screen position (CenterX, CenterY) of the engaged user.
    /// Useful for robot head turning or UI highlighting.
    /// Returns (0,0) if no one is engaged.
    /// </summary>
    public (float x, float y) GetEngagedUserScreenPosition()
    {
        if (_cachedEngagedFace != null)
        {
            return (_cachedEngagedFace.CenterX, _cachedEngagedFace.CenterY);
        }
        return (0f, 0f);
    }

    /// <summary>
    /// Checks if a specific track ID is currently engaged.
    /// </summary>
    public bool IsUserEngaged(int trackId)
    {
        return EngagedTrackId.HasValue && EngagedTrackId.Value == trackId;
    }

    /// <summary>
    /// Force disengagement (e.g., triggered by external emergency stop).
    /// </summary>
    public void ForceDisengage()
    {
        Disengage();
    }
}