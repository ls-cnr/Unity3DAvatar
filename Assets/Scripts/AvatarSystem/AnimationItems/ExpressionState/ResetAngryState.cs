using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per la fase di attivazione dell'espressione arrabbiata.
/// </summary>
public class ResetAngryState : ExpressionState {
    /// <summary>
    /// L'intensità dell'espressione.
    /// </summary>
    private float intensity = 0f;
    
    /// <summary>
    /// I valori delle blend shapes per questa espressione.
    /// </summary>
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
    {
        { "browDownLeft", 0f },
        { "browDownRight", 0f },
        { "noseSneerLeft", 0f },
        { "noseSneerRight", 0f },
        { "mouthFrownLeft", 0f },
        { "mouthFrownRight", 0f },
        { "eyeSquintLeft", 0f },
        { "eyeSquintRight", 0f }
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