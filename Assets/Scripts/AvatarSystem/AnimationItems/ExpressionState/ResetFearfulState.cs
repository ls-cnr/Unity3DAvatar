using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per la fase di attivazione dell'espressione impaurita.
/// </summary>
public class ResetFearfulState : ExpressionState {
    /// <summary>
    /// L'intensità dell'espressione.
    /// </summary>

    private float intensity = 0f;
    
    /// <summary>
    /// I valori delle blend shapes per questa espressione.
    /// </summary>
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
    {
        { "eyeWideLeft", 0f },
        { "eyeWideRight", 0f },
        { "browInnerUp", 0f },
        { "browOuterUpLeft", 0f },
        { "browOuterUpRight", 0f },
        { "jawOpen", 0f },
        { "mouthOpen", 0f }
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