using UnityEngine;

[System.Serializable]
public class EngagementWeights
{
    public float wv = 0.2f, wa = 0.2f, wt = 0.1f, wG = 0.1f, wC = 0.1f, wp = 0.1f, wR = 0.1f, wu = 0.1f;
}

public interface IEngagementPolicy
{
    string PolicyName { get; }
    EngagementWeights GetWeights();

    // --- Behavior Tree Conditions ---
    bool CheckUntrackCondition(ParticipantContext current);
    bool CheckChangeCondition(ParticipantContext candidate);
    bool CheckEngagementCondition(ParticipantContext candidate);

    // --- Formula Variables ---
    
    /// <summary>
    /// R_i: Role Relevance. 
    /// Returns a value [0, 1] indicating how important this participant is for this specific policy.
    /// </summary>
    float GetRoleRelevance(ParticipantContext context);

    /// <summary>
    /// P_i: Perceptual Penalty.
    /// (Optional) If different roles calculate "penalty" differently, put it here. 
    /// Otherwise, keep it in the unified VariableCalculator.
    /// </summary>
    float CalculatePerceptualPenalty(Faces.TrackedFace face); 
}