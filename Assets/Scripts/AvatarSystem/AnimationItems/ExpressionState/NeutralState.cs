using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per l'espressione neutra.
/// </summary>
public class NeutralState : ExpressionState
{
    /// L'intensità dell'espressione.
    private float intensity = 0.01f;

    /// I valori delle blend shapes per questa espressione.
    private Dictionary<string, float> expressionValues = new Dictionary<string, float>()
                {
                    // Espressione neutra: nessun valore, tutto a 0
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

    public new bool IsCompatible(ExpressionState with) {
        return true;
    }
}