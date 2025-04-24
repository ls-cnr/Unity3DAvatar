using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Classe per animazioni che hanno uno stato di entrata, mantenimento e uscita.
/// Utile per espressioni temporanee e battito delle palpebre.
public abstract class TransitoryAnimation : FaceAnimationItem {
   
    private bool started = false;
    private bool terminated = false;

    public virtual float GetEntryAnimationDuration() { 
        return 0.5f; 
    }
    public virtual float GetExitAnimationDuration() { 
        return 0.5f; 
    }
    public override bool IsComplete() {
        return terminated;
    }

    /// Ottiene l'animazione nello stato iniziale.
    /// <returns>L'animazione di entrata come BasicAnimationItem.</returns>
    public abstract ExpressionState GetEntryExpressionState();
    
    /// Ottiene il tempo di mantenimento dello stato iniziale prima di passare allo stato finale.
    /// <returns>Il tempo di mantenimento in secondi.</returns>
    public abstract float HoldTime();
    
    /// Ottiene l'animazione nello stato finale.
    /// <returns>L'animazione di uscita come BasicAnimationItem.</returns>
    public abstract ExpressionState GetExitExpressionState();

    public override IEnumerator AnimationCoroutine(Dictionary<string, float> currentBlendShapeValues, BlendShapeHelper blendShapeHelper) {
        started = true;

        //Debug.Log("animazione iniziata");
        ExpressionState entryTargetState = GetEntryExpressionState();
        Dictionary<string, float> entryTargetBlendShapeValues = entryTargetState.GetBlendShapeValues();
        float entryAnimationDuration = GetEntryAnimationDuration();
        
        yield return InterpolationCoroutine(currentBlendShapeValues,entryTargetBlendShapeValues,entryAnimationDuration,blendShapeHelper);
        //Debug.Log("fase 1 finita");

        yield return new WaitForSeconds(HoldTime());

        //Debug.Log("fase 2 inziata");

        ExpressionState exitTargetState = GetExitExpressionState();
        Dictionary<string, float> exitTargetBlendShapeValues = exitTargetState.GetBlendShapeValues();
        float exitAnimationDuration = GetExitAnimationDuration();
        
        yield return InterpolationCoroutine(entryTargetBlendShapeValues,exitTargetBlendShapeValues,exitAnimationDuration,blendShapeHelper);

        terminated = true;
        //Debug.Log("animazione conclusa");
    }

}

