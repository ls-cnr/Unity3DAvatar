/// Classe specializzata per l'animazione del battito delle palpebre.
/// Estende TransitoryAnimation con impostazioni specifiche per il battito degli occhi.
public class BlinkAnimation : TransitoryAnimation {

    public override int GetPriority() {
        return 10;
    }
    public override float GetEntryAnimationDuration() { 
        return 0.01f; 
    }
    public override float GetExitAnimationDuration() { 
        return 0.01f; 
    }
    public override ExpressionState GetEntryExpressionState() {
        return new ClosedEyeState();
    }
    public override float HoldTime() {
        return 0.05f;
    }
    public override ExpressionState GetExitExpressionState() {
        return new OpenEyeState();
    }
}