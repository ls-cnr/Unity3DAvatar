using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per l'espressione disgustata.
/// È un'animazione transitoria che ritorna allo stato neutrale dopo un breve tempo.
/// </summary>
public class DisgustedExpressionAnimation : TransitoryAnimation {

    public override int GetPriority() {
        return 5;
    }
    public override ExpressionState GetEntryExpressionState() {
        return new DisgustedState();
    }
    public override float HoldTime() {
        return 3f;
    }
    public override ExpressionState GetExitExpressionState() {
        return new ResetDisgustedState();
    }

}