using UnityEngine;

[CreateAssetMenu(fileName = "GenericPolicy", menuName = "Engagement/GenericPolicy")]
public class GenericPolicy : ScriptableObject, IEngagementPolicy
{
    public string PolicyName => "Generic (Equal Weights)";

    [Header("Formula Weights (Equation 1)")]
    [Tooltip("These weights will be visible and editable in the Unity Inspector.")]
    public EngagementWeights weights;

    [Header("Generic Thresholds")]
    [Tooltip("If the engaged user's probability drops below this, untrack them.")]
    public float MinEngagementProbability = 0.1f;

    // =========================================================================
    // FORMULA CONFIGURATION
    // =========================================================================
    
    /// <summary>
    /// Returns the serialized weights from the Inspector.
    /// </summary>
    public EngagementWeights GetWeights()
    {
        return weights;
    }

    public float GetRoleRelevance(ParticipantContext context)
    {
        // Generic policy: No one has special role relevance.
        return 0f; 
    }

    public float CalculatePerceptualPenalty(Faces.TrackedFace face)
    {
        // Objective penalty based on bounding box area (proxy for distance from camera)
        float bboxArea = face.OriginalRect.Width * face.OriginalRect.Height;
        float maxExpectedArea = 1920f * 1080f * 0.2f; 
        
        float penalty = 1.0f - (bboxArea / maxExpectedArea); 
        
        return Mathf.Clamp01(penalty);
    }

    // =========================================================================
    // BEHAVIOR TREE CONDITIONS
    // =========================================================================

    public bool CheckUntrackCondition(ParticipantContext current)
    {
        return current.Probability < MinEngagementProbability;
    }

    public bool CheckChangeCondition(ParticipantContext candidate)
    {
        return true; 
    }

    public bool CheckEngagementCondition(ParticipantContext candidate)
    {
        return candidate.Probability > 0f;
    }
}