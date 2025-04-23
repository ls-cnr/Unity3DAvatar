using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Animazione per l'espressione neutrale.
/// </summary>
public class NeutralExpressionAnimation : PersistentAnimation {

    public override int GetPriority() {
        return 10;
    }

    public override ExpressionState GetExpressionState() {
        return new NeutralState();
    }

}