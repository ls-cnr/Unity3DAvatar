using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per la fase di attivazione dell'espressione arrabbiata.
/// </summary>
public class AngryState : ExpressionState {
    /// <summary>
    /// L'intensità dell'espressione.
    /// </summary>
    private float intensity = 0.005f;
    
    /// <summary>
    /// I valori delle blend shapes per questa espressione.
    /// </summary>
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
    {
        { "browDownLeft", 0.6f },
        { "browDownRight", 0.6f },
        { "noseSneerLeft", 0.4f },
        { "noseSneerRight", 0.4f },
        { "mouthFrownLeft", 0.3f },
        { "mouthFrownRight", 0.3f },
        { "eyeSquintLeft", 0.3f },
        { "eyeSquintRight", 0.3f }
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