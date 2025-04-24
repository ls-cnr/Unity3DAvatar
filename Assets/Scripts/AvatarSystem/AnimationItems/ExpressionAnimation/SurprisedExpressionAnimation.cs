using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per l'espressione sorpresa.
/// È un'animazione transitoria che ritorna allo stato neutrale dopo un breve tempo.
/// </summary>
public class SurprisedExpressionAnimation : TransitoryAnimation {

    public override int GetPriority() {
        return 5;
    }
    public override ExpressionState GetEntryExpressionState() {
        return new SurprisedState();
    }
    public override float HoldTime() {
        return 3f;
    }
    public override ExpressionState GetExitExpressionState() {
        return new ResetSurprisedState();
    }

}