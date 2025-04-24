using UnityEngine;

/// <summary>
/// Gestisce gli input da tastiera per controllare le espressioni facciali dell'avatar.
/// </summary>
public class KeyboardExpressionController : MonoBehaviour
{
    [Header("Key Mappings")]
    [Tooltip("Tasto per attivare l'espressione neutrale")]
    [SerializeField] private KeyCode neutralKey = KeyCode.N;
    
    [Tooltip("Tasto per attivare l'espressione felice")]
    [SerializeField] private KeyCode happyKey = KeyCode.A;
    
    [Tooltip("Tasto per attivare l'espressione triste")]
    [SerializeField] private KeyCode sadKey = KeyCode.S;
    
    [Tooltip("Tasto per attivare l'espressione arrabbiata")]
    [SerializeField] private KeyCode angryKey = KeyCode.D;
    
    [Tooltip("Tasto per attivare l'espressione sorpresa")]
    [SerializeField] private KeyCode surprisedKey = KeyCode.F;
    
    [Tooltip("Tasto per attivare l'espressione impaurita")]
    [SerializeField] private KeyCode fearfulKey = KeyCode.G;
    
    [Tooltip("Tasto per attivare l'espressione disgustata")]
    [SerializeField] private KeyCode disgustedKey = KeyCode.H;


    private ExpressionController expressionController;
    /// <summary>
    /// Inizializza il componente trovando l'ExpressionController se non è stato assegnato.
    /// </summary>
    private void Start()
    {
        // Se l'ExpressionController non è stato assegnato nell'Inspector, prova a trovarlo
        expressionController = GetComponent<ExpressionController>();
        if (expressionController == null)
            Debug.LogError("KeyboardExpressionController: Impossibile trovare ExpressionController nella scena.");
        
    }

    /// <summary>
    /// Controlla gli input della tastiera ad ogni frame.
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(happyKey))
        {
            expressionController.SetCurrentExpression(ExpressionState.ExpressionType.Happy);
            Debug.Log("Espressione impostata: Happy");
        }
        else if (Input.GetKeyDown(sadKey))
        {
            expressionController.SetCurrentExpression(ExpressionState.ExpressionType.Sad);
            Debug.Log("Espressione impostata: Sad");
        }
        else if (Input.GetKeyDown(angryKey))
        {
            expressionController.SetCurrentExpression(ExpressionState.ExpressionType.Angry);
            Debug.Log("Espressione impostata: Angry");
        }
        else if (Input.GetKeyDown(surprisedKey))
        {
            expressionController.SetCurrentExpression(ExpressionState.ExpressionType.Surprised);
            Debug.Log("Espressione impostata: Surprised");
        }
        else if (Input.GetKeyDown(fearfulKey))
        {
            expressionController.SetCurrentExpression(ExpressionState.ExpressionType.Fearful);
            Debug.Log("Espressione impostata: Fearful");
        }
        else if (Input.GetKeyDown(disgustedKey))
        {
            expressionController.SetCurrentExpression(ExpressionState.ExpressionType.Disgusted);
            Debug.Log("Espressione impostata: Disgusted");
        }
    }
}