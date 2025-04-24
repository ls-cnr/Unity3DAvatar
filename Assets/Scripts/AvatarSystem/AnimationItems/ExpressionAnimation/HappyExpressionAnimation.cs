using System.Collections.Generic;
using UnityEngine;

/// Animazione per l'espressione felice.
/// Implementata come animazione transitoria che torna a neutrale dopo un certo tempo.
public class HappyExpressionAnimation : TransitoryAnimation {

    public override int GetPriority() {
        return 5;
    }
    public override ExpressionState GetEntryExpressionState() {
        return new HappyState();
    }
    public override float HoldTime() {
        return 5f;
    }
    public override ExpressionState GetExitExpressionState() {
        return new ResetHappyState();
    }

}