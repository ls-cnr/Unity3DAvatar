[System.Serializable]
public class ParticipantContext
{
    public Faces.TrackedFace FaceData;
    
    // Formula Variables (Eq 1) - Now populated by VariableCalculator
    public float Vi; // Visual Activity
    public float Ai; // Audio Activity
    public float Ti; // Speaking Time
    public float Gi; // Unsuccessful Attempts
    public float Ci; // Spatial
    public float Pi; // Perceptual Penalty
    public float Ri; // Role Relevance (Set by Policy)
    public float Ui; // Under-participation
    
    public float EngagementScore; 
    public float Probability;     

    public ParticipantContext(Faces.TrackedFace face)
    {
        FaceData = face;
    }
}