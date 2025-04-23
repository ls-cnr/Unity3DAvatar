using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per l'espressione arrabbiata.
/// Implementata come animazione transitoria che torna a neutrale dopo un certo tempo.
/// </summary>
public class AngryExpressionAnimation : TransitoryAnimation {
    public override int GetPriority() {
        return 5;
    }
    public override ExpressionState GetEntryExpressionState() {
        return new AngryState();
    }
    public override float HoldTime() {
        return 3f;
    }
    public override ExpressionState GetExitExpressionState() {
        return new NeutralState();
    }
}