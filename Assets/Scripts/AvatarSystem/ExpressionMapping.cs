using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Classe che definisce e gestisce le mappature tra espressioni facciali e valori di blend shapes.
/// </summary>
public class ExpressionMapping {
    /// <summary>
    /// Tipi di espressioni facciali supportate.
    /// </summary>
    public enum ExpressionType {
        Neutral,
        Happy,
        Sad,
        Angry,
        Surprised,
        Fearful,
        Disgusted
    }

    // Dizionario che mappa ciascuna espressione ai suoi valori di blend shape
    private readonly Dictionary<ExpressionType, Dictionary<string, float>> facialExpressions;

    /// <summary>
    /// Costruttore che inizializza le mappature delle espressioni facciali con valori predefiniti.
    /// </summary>
    public ExpressionMapping() {
        facialExpressions = new Dictionary<ExpressionType, Dictionary<string, float>>()
        {
            {
                ExpressionType.Neutral, new Dictionary<string, float>()
                {
                    // Espressione neutra: nessun valore, tutto a 0
                }
            },
            {
                ExpressionType.Happy, new Dictionary<string, float>()
                {
                    { "mouthSmileLeft", 0.7f },
                    { "mouthSmileRight", 0.7f },
                    { "cheekSquintLeft", 0.4f },
                    { "cheekSquintRight", 0.4f },
                    { "eyeSquintLeft", 0.2f },
                    { "eyeSquintRight", 0.2f },
                    { "browOuterUpLeft", 0.2f },
                    { "browOuterUpRight", 0.2f }
                }
            },
            {
                ExpressionType.Sad, new Dictionary<string, float>()
                {
                    { "browInnerUp", 0.6f },
                    { "mouthFrownLeft", 0.4f },
                    { "mouthFrownRight", 0.4f },
                    { "mouthShrugLower", 0.3f },
                    { "browDownLeft", 0.2f },
                    { "browDownRight", 0.2f }
                }
            },
            {
                ExpressionType.Angry, new Dictionary<string, float>()
                {
                    { "browDownLeft", 0.6f },
                    { "browDownRight", 0.6f },
                    { "noseSneerLeft", 0.4f },
                    { "noseSneerRight", 0.4f },
                    { "mouthFrownLeft", 0.3f },
                    { "mouthFrownRight", 0.3f },
                    { "eyeSquintLeft", 0.3f },
                    { "eyeSquintRight", 0.3f }
                }
            },
            {
                ExpressionType.Surprised, new Dictionary<string, float>()
                {
                    { "eyeWideLeft", 0.6f },
                    { "eyeWideRight", 0.6f },
                    { "browInnerUp", 0.5f },
                    { "browOuterUpLeft", 0.5f },
                    { "browOuterUpRight", 0.5f },
                    { "jawOpen", 0.4f },
                    { "mouthOpen", 0.4f }
                }
            },
            {
                ExpressionType.Fearful, new Dictionary<string, float>()
                {
                    { "eyeWideLeft", 0.5f },
                    { "eyeWideRight", 0.5f },
                    { "browInnerUp", 0.3f },        // Ridotto per bilanciare l'espressione
                    { "browOuterUpLeft", 0.15f },   // Ridotto per bilanciare l'espressione
                    { "browOuterUpRight", 0.15f },  // Ridotto per bilanciare l'espressione
                    { "mouthStretchLeft", 0.3f },
                    { "mouthStretchRight", 0.3f }
                }
            },
            {
                ExpressionType.Disgusted, new Dictionary<string, float>()
                {
                    { "noseSneerLeft", 0.6f },
                    { "noseSneerRight", 0.6f },
                    { "mouthLeft", 0.4f },
                    { "cheekSquintLeft", 0.4f },
                    { "cheekSquintRight", 0.4f },
                    { "browDownLeft", 0.4f },
                    { "browDownRight", 0.4f }
                }
            }
        };
    }

    /// <summary>
    /// Ottiene i valori delle blend shapes per una specifica espressione.
    /// </summary>
    /// <param name="expressionType">Il tipo di espressione.</param>
    /// <returns>Un dizionario di nomi di blend shapes e relativi valori per l'espressione specificata.</returns>
    public Dictionary<string, float> GetExpressionValues(ExpressionType expressionType) {
        if (facialExpressions.TryGetValue(expressionType, out var values)) {
            // Ritorna una copia per evitare modifiche non intenzionali al dizionario originale
            return new Dictionary<string, float>(values);
        }

        // Se l'espressione non esiste, ritorna un dizionario vuoto
        return new Dictionary<string, float>();
    }

    /// <summary>
    /// Verifica se una specifica espressione è definita.
    /// </summary>
    /// <param name="expressionType">Il tipo di espressione da verificare.</param>
    /// <returns>True se l'espressione è definita, false altrimenti.</returns>
    public bool HasExpression(ExpressionType expressionType) {
        return facialExpressions.ContainsKey(expressionType);
    }

    /// <summary>
    /// Ottiene tutti i tipi di espressione disponibili.
    /// </summary>
    /// <returns>Un array di tutti i tipi di espressione definiti.</returns>
    public ExpressionType[] GetAvailableExpressions() {
        ExpressionType[] expressions = new ExpressionType[facialExpressions.Count];
        facialExpressions.Keys.CopyTo(expressions, 0);
        return expressions;
    }
}