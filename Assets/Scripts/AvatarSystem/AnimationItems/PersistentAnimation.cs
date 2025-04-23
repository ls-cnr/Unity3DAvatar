using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PersistentAnimation : FaceAnimationItem {
    private bool started = false;
    private bool terminated = false;

    public float GetAnimationDuration() { 
        return 0.5f; 
    }
    public override bool IsComplete() {
        return terminated;
    }

    public abstract ExpressionState GetExpressionState();

    public override IEnumerator AnimationCoroutine(Dictionary<string, float> currentBlendShapeValues, BlendShapeHelper blendShapeHelper) {
        started = true;

        // Ottieni lo stato di espressione target e i valori delle blend shapes
        ExpressionState targetState = GetExpressionState();
        Dictionary<string, float> targetBlendShapeValues = targetState.GetBlendShapeValues();
        
        // Durata dell'animazione (in secondi)
        float animationDuration = GetAnimationDuration();
        
        // Tempo trascorso
        float elapsedTime = 0f;
        
        // Esegui l'interpolazione finché non raggiungi la durata dell'animazione
        while (elapsedTime < animationDuration)
        {
            // Calcola la posizione normalizzata (0.0 - 1.0)
            float normalizedPosition = elapsedTime / animationDuration;
            
            // Interpola tra i valori attuali e quelli target
            Dictionary<string, float> interpolatedValues = Interpolate(currentBlendShapeValues, targetBlendShapeValues, normalizedPosition);
            
            // Applica i valori interpolati al modello 3D
            ApplyBlendShapeValues(interpolatedValues, blendShapeHelper);
            
            // Incrementa il tempo trascorso
            elapsedTime += Time.deltaTime;
            
            // Attendi il prossimo frame
            yield return null;
        }
        
        // Imposta esattamente i valori target alla fine dell'animazione
        ApplyBlendShapeValues(targetBlendShapeValues, blendShapeHelper);
        terminated = true;
    }

}