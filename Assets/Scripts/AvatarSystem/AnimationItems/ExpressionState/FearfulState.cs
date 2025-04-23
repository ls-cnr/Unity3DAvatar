using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per la fase di attivazione dell'espressione impaurita.
/// </summary>
public class FearfulState : ExpressionState {
    /// <summary>
    /// L'intensità dell'espressione.
    /// </summary>
    private float intensity = 0.005f;
    
    /// <summary>
    /// I valori delle blend shapes per questa espressione.
    /// </summary>
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
    {
        { "eyeWideLeft", 0.5f },
        { "eyeWideRight", 0.5f },
        { "browInnerUp", 0.3f },
        { "browOuterUpLeft", 0.15f },
        { "browOuterUpRight", 0.15f },
        { "mouthStretchLeft", 0.3f },
        { "mouthStretchRight", 0.3f }
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