using System.Collections.Generic;
using UnityEngine;

public class EngagementCalculator
{
    private IEngagementPolicy _policy;

    public EngagementCalculator(IEngagementPolicy policy) { _policy = policy; }

    public void Calculate(List<ParticipantContext> participants)
    {
        var w = _policy.GetWeights();
        float sumExp = 0f;

        foreach (var p in participants)
        {
            // 1. Get R_i (Role Relevance) from the Policy
            p.Ri = _policy.GetRoleRelevance(p);

            // 2. Get P_i (Perceptual Penalty) from the Policy
            p.Pi = _policy.CalculatePerceptualPenalty(p.FaceData);

            // 3. Equation 1: S_i = wv*Vi + wa*Ai + wt*Ti + wG*Gi + wC*Ci - wp*Pi + wR*Ri + wu*Ui
            float Si = (w.wv * p.Vi) + (w.wa * p.Ai) + (w.wt * p.Ti) 
                       + (w.wG * p.Gi) + (w.wC * p.Ci) 
                       - (w.wp * p.Pi) 
                       + (w.wR * p.Ri) + (w.wu * p.Ui);
            
            p.EngagementScore = Si;
            sumExp += Mathf.Exp(Si);
        }

        // 4. Equation 2: Softmax
        foreach (var p in participants)
        {
            p.Probability = Mathf.Exp(p.EngagementScore) / sumExp;
        }
    }
}