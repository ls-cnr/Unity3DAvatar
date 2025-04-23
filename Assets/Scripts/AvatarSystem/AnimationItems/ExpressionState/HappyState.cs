using System.Collections.Generic;
using UnityEngine;

/// Animazione per la fase di attivazione dell'espressione felice.
public class HappyState : ExpressionState {
    /// L'intensità dell'espressione.
    private float intensity = 0.005f;
    
    /// I valori delle blend shapes per questa espressione.
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
                {
                    { "mouthSmileLeft", 0.7f },
                    { "mouthSmileRight", 0.7f },
                    { "cheekSquintLeft", 0.4f },
                    { "cheekSquintRight", 0.4f },
                    { "eyeSquintLeft", 0.2f },
                    { "eyeSquintRight", 0.2f },
                    { "browOuterUpLeft", 0.2f },
                    { "browOuterUpRight", 0.2f }
                };

    
    /// Ottiene i valori correnti delle blend shapes.
    public override Dictionary<string, float> GetBlendShapeValues()
    {
        Dictionary<string, float> result = new Dictionary<string, float>();
        
        foreach (var pair in expressionValues) {
            result[pair.Key] = pair.Value * intensity * 100f;
        }
        
        return result;
    }

}
