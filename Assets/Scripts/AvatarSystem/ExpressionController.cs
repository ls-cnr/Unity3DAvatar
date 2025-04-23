using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// Controllore principale per le espressioni facciali che utilizza un sistema di schedulazione delle animazioni.
/// Coordina l'intero sistema di animazioni facciali dell'avatar.
public class ExpressionController : MonoBehaviour
{
   
    [Header("Expression Settings")]
    [Tooltip("Espressione corrente dell'avatar")]
    [SerializeField] private ExpressionState.ExpressionType currentExpression = ExpressionState.ExpressionType.Neutral;

    [Header("Blink Settings")]
    [Tooltip("Abilitare il battito delle palpebre automatico")]
    [SerializeField] private bool enableBlinking = true;
    
    // Riferimenti ai componenti di supporto
    private AvatarManager avatarManager;
    private AnimationScheduler animationScheduler;
    private BlendShapeHelper blendShapeHelper;
    private BlinkingManager blinkingManager;
    
    // Animazione correntemente attiva
    private ExpressionState currentState = new NeutralState();
    private FaceAnimationItem currentAnimation;
    private Coroutine currentAnimationCoroutine;
    

    
    /// Inizializzazione del controller.
    private void Awake()
    {
        // Inizializza lo scheduler delle animazioni
        animationScheduler = new AnimationScheduler();
        
        // Ottiene il componente AvatarManager
        avatarManager = GetComponent<AvatarManager>();
        if (avatarManager == null)
        {
            Debug.LogError("ExpressionController: Impossibile trovare il componente AvatarManager!");
            enabled = false;
            return;
        }
        
        // Iscrizione all'evento di caricamento dell'avatar
        avatarManager.OnAvatarLoaded += OnAvatarLoaded;
        
        // Se l'avatar è già stato caricato, inizializza subito
        if (avatarManager.GetAvatar() != null)
        {
            OnAvatarLoaded(avatarManager.GetAvatar());
        }
    }
    
    /// Handler per l'evento di caricamento dell'avatar.
    private void OnAvatarLoaded(GameObject avatar)
    {
        Debug.Log("ExpressionController: Avatar caricato, inizializzazione controllo espressioni...");
        
        // Inizializza il BlendShapeHelper con l'avatar
        blendShapeHelper = new BlendShapeHelper(avatar);
        if (!blendShapeHelper.IsInitialized())
        {
            Debug.LogError("ExpressionController: BlendShapeHelper non è stato inizializzato correttamente.");
            return;
        }
        
        // Inizializza il BlinkingManager
        blinkingManager = new BlinkingManager(blendShapeHelper, this, animationScheduler);
        
        // Configura le impostazioni iniziali del blinking
        ConfigureBlinking(enableBlinking);
        
        // Imposta l'espressione iniziale
        SetCurrentExpression(currentExpression);
        
        Debug.Log("ExpressionController: Inizializzazione completata con successo.");
    }
    
    /// Aggiornamento del controller ad ogni frame.
    private void Update()
    {
        if (blendShapeHelper == null || !blendShapeHelper.IsInitialized())
            return;
            
        // Verifica se è necessario prelevare una nuova animazione
        if (currentAnimation == null || currentAnimation.IsComplete())
        {
            // Preleva la prossima animazione dalla coda
            FaceAnimationItem nextAnimation = animationScheduler.DequeueNextAnimation();
            
            if (nextAnimation != null)
            {
                // Avvia la nuova animazione
                StartNewAnimation(nextAnimation);
            }
        }
    }
    
    /// Avvia una nuova animazione.
    /// <param name="animation">L'animazione da avviare</param>
    private void StartNewAnimation(FaceAnimationItem animation)
    {
        if (animation == null)
            return;
        
        // Imposta e inizializza la nuova animazione
        currentAnimation = animation;
        IEnumerator coroutine = animation.AnimationCoroutine(currentState.GetBlendShapeValues(),blendShapeHelper);
        currentAnimationCoroutine = StartCoroutine(coroutine);    
    }
    
    /// <summary>
    /// Configura le impostazioni del battito delle palpebre.
    /// </summary>
    /// <param name="enable">Se abilitare il battito delle palpebre</param>
    private void ConfigureBlinking(bool enable)
    {
        if (blinkingManager == null)
            return;
            
        if (enable)
        {
            blinkingManager.StartBlinking();
        }
        else
        {
            blinkingManager.StopBlinking();
        }
    }
        
    /// <summary>
    /// Ripulisce le risorse quando l'oggetto viene distrutto.
    /// </summary>
    private void OnDestroy()
    {
        // Rimuove l'iscrizione all'evento
        if (avatarManager != null)
        {
            avatarManager.OnAvatarLoaded -= OnAvatarLoaded;
        }
        
        // Ferma il blinking
        if (blinkingManager != null)
        {
            blinkingManager.StopBlinking();
        }
    }
    
    #region API Pubblica
    
    /// <summary>
    /// Imposta l'espressione corrente dell'avatar.
    /// </summary>
    /// <param name="expressionType">Il tipo di espressione da impostare</param>
    public void SetCurrentExpression(ExpressionState.ExpressionType expressionType)
    {
        // Aggiorna la variabile serializzata
        currentExpression = expressionType;
        
        // Crea e pianifica la nuova animazione
        FaceAnimationItem animation = FaceAnimationItem.GetTransitoryTo(expressionType);
        animationScheduler.EnqueueAnimation(animation);
    }
    
    
    /// <summary>
    /// Ottiene l'espressione corrente.
    /// </summary>
    /// <returns>L'espressione corrente</returns>
    public ExpressionState.ExpressionType GetCurrentExpression()
    {
        return currentExpression;
    }
    
    /// <summary>
    /// Abilita o disabilita il battito delle palpebre.
    /// </summary>
    /// <param name="enable">Se abilitare il battito delle palpebre</param>
    public void SetBlinkingEnabled(bool enable)
    {
        enableBlinking = enable;
    }
    
    #endregion
}