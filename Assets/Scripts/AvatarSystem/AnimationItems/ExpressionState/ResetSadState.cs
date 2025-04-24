using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per la fase di attivazione dell'espressione arrabbiata.
/// </summary>
public class ResetSadState : ExpressionState {
    /// <summary>
    /// L'intensità dell'espressione.
    /// </summary>
    private float intensity = 0.0f;
    
    /// <summary>
    /// I valori delle blend shapes per questa espressione.
    /// </summary>
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
    {
        { "browInnerUp", 0f },
        { "mouthFrownLeft", 0f },
        { "mouthFrownRight", 0f },
        { "mouthShrugLower", 0f },
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