using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per la fase di attivazione dell'espressione disgustata.
/// </summary>
public class ResetDisgustedState : ExpressionState {
    /// <summary>
    /// L'intensità dell'espressione.
    /// </summary>
    private float intensity = 0f;
    
    /// <summary>
    /// I valori delle blend shapes per questa espressione.
    /// </summary>
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
    {
        { "noseSneerLeft", 0f },
        { "noseSneerRight", 0f },
        { "mouthLeft", 0f },
        { "cheekSquintLeft", 0f },
        { "cheekSquintRight", 0f },
        { "browDownLeft", 0f },
        { "browDownRight", 0f }
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