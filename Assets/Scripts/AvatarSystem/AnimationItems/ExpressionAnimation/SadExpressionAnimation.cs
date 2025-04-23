using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per l'espressione triste.
/// Implementata come animazione transitoria che torna a neutrale dopo un certo tempo.
/// </summary>
public class SadExpressionAnimation : TransitoryAnimation {
    public override int GetPriority() {
        return 5;
    }
    public override ExpressionState GetEntryExpressionState() {
        return new SadState();
    }
    public override float HoldTime() {
        return 1f;
    }
    public override ExpressionState GetExitExpressionState() {
        return new NeutralState();
    }
}