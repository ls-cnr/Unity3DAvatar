using System.Collections.Generic;
using UnityEngine;

/// Animazione per la fase di attivazione dell'espressione felice.
public class ResetHappyState : ExpressionState {
    /// L'intensità dell'espressione.
    private float intensity = 0f;

    /// I valori delle blend shapes per questa espressione.
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
                {
                    { "mouthSmileLeft", 0.0f },
                    { "mouthSmileRight", 0.0f },
                    { "cheekSquintLeft", 0.0f },
                    { "cheekSquintRight", 0.0f },
                    { "eyeSquintLeft", 0.0f },
                    { "eyeSquintRight", 0.0f },
                    { "browOuterUpLeft", 0.0f },
                    { "browOuterUpRight", 0.0f }
                };


    /// Ottiene i valori correnti delle blend shapes.
    public override Dictionary<string, float> GetBlendShapeValues() {
        Dictionary<string, float> result = new Dictionary<string, float>();

        foreach (var pair in expressionValues) {
            result[pair.Key] = pair.Value * intensity * 100f;
        }

        return result;
    }

}
