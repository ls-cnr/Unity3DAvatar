using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Classe base astratta per tutte le animazioni facciali.
/// Definisce l'interfaccia minima comune a tutte le animazioni.
public abstract class FaceAnimationItem
{   
    /// Ottiene la priorità dell'animazione. Valori più alti hanno maggiore priorità.
    public abstract int GetPriority();

    /// <summary>
    /// invocato da che ExpressionController per avviare l'animazione
    /// </summary>
    /// <returns>una coroutine per generare l'animazione</returns>
    public abstract IEnumerator AnimationCoroutine(Dictionary<string, float> currentBlendShapeValues, BlendShapeHelper blendShapeHelper);

    public abstract bool IsComplete();
    
    protected Dictionary<string, float> Interpolate(Dictionary<string, float> start, Dictionary<string, float> target, float pos) {
        Dictionary<string, float> output = new Dictionary<string, float>();
        
        // 1) Calcola l'unione delle Keys di start e target
        HashSet<string> allKeys = new HashSet<string>(start.Keys);
        allKeys.UnionWith(target.Keys);
        
        // 2) Cicla su tutte le chiavi uniche
        foreach(var key in allKeys) {
            if (start.ContainsKey(key) && target.ContainsKey(key)) {
                // Blend shape presente in entrambi i dizionari: interpola
                output[key] = Mathf.Lerp(start[key], target[key], pos);
            }
            else if (start.ContainsKey(key)) {
                // Blend shape presente solo in start: riporta il valore di start
                output[key] = start[key];
            }
            else {
                // Blend shape presente solo in target: riporta il valore di target
                output[key] = target[key];
            }
        }
        
        return output;
    }

    // Applica i valori delle blend shapes al modello
    protected void ApplyBlendShapeValues(Dictionary<string, float> blendShapeValues, BlendShapeHelper blendShapeHelper)
    {
        foreach (var pair in blendShapeValues) {
            // Applica ogni valore di blend shape al renderer
            blendShapeHelper.SetBlendShapeWeight(pair.Key, pair.Value);
        }
    }

    protected IEnumerator InterpolationCoroutine(
        Dictionary<string, float> startBlendShapeValues,
        Dictionary<string, float> targetBlendShapeValues, 
        float duration,
        BlendShapeHelper blendShapeHelper
        ) 
    {
        float entryElapsedTime = 0f;
        
        if (duration > 0.01f) {
            // Esegui l'interpolazione finché non raggiungi la durata dell'animazione
            while (entryElapsedTime < duration) {
                float normalizedPosition = entryElapsedTime / duration;
                Dictionary<string, float> interpolatedValues = Interpolate(startBlendShapeValues, targetBlendShapeValues, normalizedPosition);
                ApplyBlendShapeValues(interpolatedValues, blendShapeHelper);
                entryElapsedTime += Time.deltaTime;
                yield return null;
            }
        }

        // Imposta esattamente i valori target alla fine dell'animazione
        ApplyBlendShapeValues(targetBlendShapeValues, blendShapeHelper); 
    }


    public static FaceAnimationItem GetTransitoryTo(ExpressionState.ExpressionType expr_type) {
        switch(expr_type) {
            case ExpressionState.ExpressionType.Neutral:
                return new NeutralExpressionAnimation();
            case ExpressionState.ExpressionType.Happy:
                return new HappyExpressionAnimation();
            case ExpressionState.ExpressionType.Sad:
                return new SadExpressionAnimation();
            case ExpressionState.ExpressionType.Angry:
                return new AngryExpressionAnimation();
            case ExpressionState.ExpressionType.Surprised:
                return new SurprisedExpressionAnimation();
            case ExpressionState.ExpressionType.Fearful:
                return new FearfulExpressionAnimation();
            case ExpressionState.ExpressionType.Disgusted:
                return new DisgustedExpressionAnimation();

            default:
                return new NeutralExpressionAnimation();
        }          
    }
}
