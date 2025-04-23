using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per la fase di chiusura degli occhi.
/// </summary>
public class ClosedEyeState : ExpressionState {
    /// <summary>
    /// L'intensità dell'espressione.
    /// </summary>
    private float intensity = 0.010f;
    
    /// <summary>
    /// I valori delle blend shapes per questa espressione.
    /// </summary>
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
    {
        { "eyeBlinkLeft", 1.0f },
        { "eyeBlinkRight", 1.0f }
    };

    /// <summary>
    /// Ottiene i valori correnti delle blend shapes.
    /// </summary>
    public override Dictionary<string, float> GetBlendShapeValues()
    {
        Dictionary<string, float> result = new Dictionary<string, float>();
        
        foreach (var pair in expressionValues) {
            result[pair.Key] = pair.Value * intensity * 100f;
        }
        
        return result;
    }
}