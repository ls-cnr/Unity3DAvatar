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
        
        yield return InterpolationCoroutine(currentBlendShapeValues,targetBlendShapeValues,animationDuration,blendShapeHelper);

        terminated = true;
    }

}