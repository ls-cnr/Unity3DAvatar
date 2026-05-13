using UnityEngine;

/// <summary>
/// Controls an avatar's head orientation using Unity's Animator IK system.
/// 
/// The avatar looks at the detected face position in the camera feed.
/// Supports mirror mode (for webcam) and camera offset calibration.
/// 
/// SETUP:
///   1. Attach to the avatar GameObject with an Animator component
///   2. Configure camera calibration settings in Inspector
///   3. Call LookAtFace() with normalized face coordinates (0-1)
/// 
/// CALIBRATION:
///   - invertMirrorX: Toggle if avatar looks opposite direction
///   - cameraOffset: Adjust if avatar doesn't look at eyes level
/// </summary>
[RequireComponent(typeof(Animator))]
public class AvatarHeadController : MonoBehaviour
{
    // =========================================================================
    // CAMERA CALIBRATION
    // =========================================================================

    [Header("1. Camera Calibration")]

    [Tooltip("If the avatar looks left when you go right, enable this.")]
    public bool invertMirrorX = true; 

    [Tooltip("Vertical offset to compensate for camera position. Negative values (e.g., -0.15) if camera is above monitor.")]
    public Vector2 cameraOffset = new Vector2(0f, -0.15f);

    // =========================================================================
    // VIRTUAL SPACE MAPPING
    // =========================================================================

    [Header("2. Virtual Space Mapping")]

    [Tooltip("Virtual distance of the screen from the avatar (meters).")]
    public float targetDistance = 2.0f;

    [Tooltip("How far the avatar can turn left/right (meters).")]
    public float horizontalSpread = 2.0f;

    [Tooltip("How far the avatar can look up/down (meters).")]
    public float verticalSpread = 1.2f;

    // =========================================================================
    // ANIMATOR IK WEIGHTS
    // =========================================================================

    [Header("3. Animator IK Weights")]

    [Range(0, 1)] 
    [Tooltip("Overall IK influence (0 = no IK, 1 = full IK).")]
    public float globalWeight = 1f;

    [Range(0, 1)]
    [Tooltip("How much the body rotates to follow the target.")]
    public float bodyWeight = 0.15f; 

    [Range(0, 1)]
    [Tooltip("How much the head rotates to follow the target.")]
    public float headWeight = 1f;

    [Range(0, 1)]
    [Tooltip("How much the eyes rotate to follow the target.")]
    public float eyesWeight = 1f;

    [Range(0, 1)]
    [Tooltip("Clamping to prevent unnatural neck twisting.")]
    public float clampWeight = 0.6f;

    // =========================================================================
    // MOVEMENT SETTINGS
    // =========================================================================

    [Tooltip("Speed of head movement (higher = more responsive).")]
    public float followSpeed = 6f;

    // =========================================================================
    // INTERNAL STATE
    // =========================================================================

    /// <summary>Reference to the Animator component.</summary>
    private Animator animator;

    /// <summary>Current smoothed focus position (0-1 normalized).</summary>
    private Vector2 currentFocusPos = new Vector2(0.5f, 0.5f);

    /// <summary>Target focus position from face detection (0-1 normalized).</summary>
    private Vector2 targetFocusPos = new Vector2(0.5f, 0.5f);

    /// <summary>3D world position the avatar is looking at.</summary>
    private Vector3 lookAtPoint3D;

    // =========================================================================
    // UNITY LIFECYCLE
    // =========================================================================

    /// <summary>Gets the Animator component.</summary>
    void Start()
    {
        animator = GetComponent<Animator>();
    }

    /// <summary>Smoothly interpolates toward the target focus position.</summary>
    void Update()
    {
        currentFocusPos = Vector2.Lerp(currentFocusPos, targetFocusPos, Time.deltaTime * followSpeed);
    }

    /// <summary>
    /// Unity Animator IK callback.
    /// Computes the 3D look-at point from normalized screen coordinates.
    /// </summary>
    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) return;

        Transform headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
        if (headTransform == null) return;

        // Convert normalized coordinates (0-1) to centered coordinates (-1 to 1)
        float offsetX = (currentFocusPos.x - 0.5f) * 2f; 

        // Invert Y because webcam Y is top-down, but 3D space Y is bottom-up
        float offsetY = (0.5f - currentFocusPos.y) * 2f; 

        // Apply physical camera offset (compensates for monitor/webcam position)
        offsetX += cameraOffset.x;
        offsetY += cameraOffset.y;

        // Build 3D target point in front of the avatar
        Vector3 target3D = headTransform.position + 
                           (transform.forward * targetDistance) + 
                           (transform.right * offsetX * horizontalSpread) + 
                           (transform.up * offsetY * verticalSpread); 

        // Smooth the 3D target
        lookAtPoint3D = Vector3.Lerp(lookAtPoint3D, target3D, Time.deltaTime * followSpeed);

        // Apply IK weights and set look-at position
        animator.SetLookAtWeight(globalWeight, bodyWeight, headWeight, eyesWeight, clampWeight);
        animator.SetLookAtPosition(lookAtPoint3D);
    }

    // =========================================================================
    // PUBLIC API
    // =========================================================================

    /// <summary>
    /// Tells the avatar to look at a face position.
    /// Call this when the neural network finds a face.
    /// 
    /// Parameters must be normalized (0 to 1):
    ///   - (0, 0) = top-left of camera view
    ///   - (1, 1) = bottom-right of camera view
    ///   - (0.5, 0.5) = center
    /// </summary>
    public void LookAtFace(float normalizedX, float normalizedY)
    {
        // Apply mirror calibration
        float finalX = invertMirrorX ? (1f - normalizedX) : normalizedX;

        targetFocusPos = new Vector2(finalX, normalizedY);
    }

    /// <summary>
    /// Returns the avatar to looking at the center (idle position).
    /// Call this when no face is detected.
    /// </summary>
    public void ReturnToCenter()
    {
        targetFocusPos = new Vector2(0.5f, 0.5f);
    }
}