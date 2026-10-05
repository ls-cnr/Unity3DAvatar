using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public static class VariableCalculator
{
    /// <summary>
    /// Updates all objective variables for a participant based on raw sensor data.
    /// </summary>
    public static void UpdateVariables(ParticipantContext p, float deltaTime, List<ParticipantContext> allParticipants, int cameraWidth, int cameraHeight)
    {
        var face = p.FaceData;

        // 1. V_i (Visual Speaking Activity)
        p.Vi = Mathf.Max(0f, 1.0f - (Mathf.Abs(face.Yaw) + Mathf.Abs(face.Pitch)) / 90.0f);

        // 2. A_i (Audio Speaking Activity)
        p.Ai = Mathf.Max(0f, face.SpeakingScore); 

        // 3. T_i (Accumulated Speaking Time)
        if (p.Ai > 0.5f) 
        {
            p.Ti += deltaTime * p.Ai; 
        }

        // 4. C_i (Spatial Variable) - FIXED: Uses true camera resolution, NOT Screen.width
        float normX = face.CenterX / cameraWidth;
        float normY = face.CenterY / cameraHeight;
        
        // Distance from center (0.5, 0.5)
        float distFromCenter = Mathf.Sqrt(Mathf.Pow(normX - 0.5f, 2) + Mathf.Pow(normY - 0.5f, 2));
        p.Ci = Mathf.Max(0f, 1.0f - (distFromCenter * 2f)); 

        // 5. P_i (Perceptual Penalty) - Handled by Policy, so we leave it 0 here.
        p.Pi = 0f; 

        // 6. U_i (Under-participation)
        if (allParticipants.Count > 0)
        {
            float avgT = allParticipants.Average(part => part.Ti);
            p.Ui = Mathf.Max(0f, avgT - p.Ti); 
        }
        else
        {
            p.Ui = 0f;
        }

        // 7. G_i (Unsuccessful Attempts)
        p.Gi = 0f; 
    }
}