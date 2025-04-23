using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Classe per animazioni base o statiche.
/// Estende FaceAnimationItem con funzionalità per gestire valori di blend shapes.
public abstract class ExpressionState
{
    /// Ottiene i valori correnti delle blend shapes.
    /// <returns>Un dizionario con i nomi delle blend shapes e i loro valori (0-100).</returns>
    public abstract Dictionary<string, float> GetBlendShapeValues();

/// Determina se questa espressione è compatibile con un'altra espressione.
/// Due espressioni sono compatibili quando non condividono blend shapes in comune.
/// <param name="with">L'altra espressione da confrontare</param>
/// <returns>True se le espressioni sono compatibili (nessuna blend shape in comune), altrimenti False</returns>
    public bool IsCompatible(ExpressionState with) {
        if (with == null)
            return true; // Nulla è sempre compatibile
            
        // Ottieni gli insiemi di blend shapes influenzate da entrambe le espressioni
        HashSet<string> thisBlendShapes = GetAffectedBlendShapes();
        HashSet<string> otherBlendShapes = with.GetAffectedBlendShapes();
        
        // Crea un nuovo HashSet che contiene l'intersezione dei due insiemi
        HashSet<string> intersection = new HashSet<string>(thisBlendShapes);
        intersection.IntersectWith(otherBlendShapes);
        
        // Le espressioni sono compatibili se l'intersezione è vuota
        return intersection.Count == 0;
    }

    /// Ottiene l'insieme delle blend shapes influenzate da questa animazione.
    /// <returns>Un HashSet di stringhe con i nomi delle blend shapes.</returns>
    public HashSet<string> GetAffectedBlendShapes() {
        Dictionary<string, float> dic = GetBlendShapeValues();
        return dic.Keys.ToHashSet();
    }

    public enum ExpressionType
    {
        Neutral,
        Happy,
        Sad,
        Angry,
        Surprised,
        Fearful,
        Disgusted
    }

    public static ExpressionState GetExpressionState(ExpressionType expr_type) {
        switch(expr_type) {
            case ExpressionType.Neutral:
                return new NeutralState();
            case ExpressionType.Happy:
                return new HappyState();
            case ExpressionType.Sad:
                return new SadState();
            case ExpressionType.Angry:
                return new AngryState();
            case ExpressionType.Surprised:
                return new SurprisedState();
            case ExpressionType.Fearful:
                return new FearfulState();
            case ExpressionType.Disgusted:
                return new DisgustedState();

            default:
                return new NeutralState();
        }    
    }

}