
/// Animazione per l'espressione impaurita.
/// È un'animazione transitoria che ritorna allo stato neutrale dopo un breve tempo.
public class FearfulExpressionAnimation : TransitoryAnimation {

    public override int GetPriority() {
        return 5;
    }
    public override ExpressionState GetEntryExpressionState() {
        return new FearfulState();
    }
    public override float HoldTime() {
        return 3f;
    }
    public override ExpressionState GetExitExpressionState() {
        return new NeutralState();
    }

}


