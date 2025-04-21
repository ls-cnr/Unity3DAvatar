using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controller avanzato per le espressioni facciali che utilizza i nuovi componenti modulari.
/// </summary>
public class ExpressionController : MonoBehaviour {

    [Header("Expression Settings")]
    [Tooltip("Espressione corrente dell'avatar")]
    [SerializeField] private ExpressionMapping.ExpressionType currentExpression = ExpressionMapping.ExpressionType.Neutral;

    [Tooltip("Velocità di transizione tra le espressioni")]
    [Range(1, 10)]
    [SerializeField] private float transitionSpeed = 5.0f;

    [Header("Expression Intensities")]
    [Tooltip("Intensità dell'espressione felice")]
    [Range(0, 0.1f)]
    [SerializeField] private float happyIntensity = 0.005f;

    [Tooltip("Intensità dell'espressione triste")]
    [Range(0, 0.1f)]
    [SerializeField] private float sadIntensity = 0.0127f;

    [Tooltip("Intensità dell'espressione arrabbiata")]
    [Range(0, 0.1f)]
    [SerializeField] private float angryIntensity = 0.012f;

    [Tooltip("Intensità dell'espressione sorpresa")]
    [Range(0, 0.1f)]
    [SerializeField] private float surprisedIntensity = 0.0087f;

    [Tooltip("Intensità dell'espressione spaventata")]
    [Range(0, 0.1f)]
    [SerializeField] private float fearfulIntensity = 0.0033f;

    [Tooltip("Intensità dell'espressione disgustata")]
    [Range(0, 0.1f)]
    [SerializeField] private float disgustedIntensity = 0.016f;

    [Header("Blink Settings")]
    [Tooltip("Abilitare il battito delle palpebre automatico")]
    [SerializeField] private bool enableBlinking = true;

    [Tooltip("Intensità del battito delle palpebre")]
    [Range(0, 0.1f)]
    [SerializeField] private float blinkIntensity = 0.0167f;

    [Tooltip("Tempo minimo tra battiti di ciglia (secondi)")]
    [SerializeField] private float minBlinkInterval = 2.0f;

    [Tooltip("Tempo massimo tra battiti di ciglia (secondi)")]
    [SerializeField] private float maxBlinkInterval = 6.0f;

    private AvatarManager avatarSetup;

    // Variabili per tracciare i valori precedenti
    private ExpressionMapping.ExpressionType lastExpression = ExpressionMapping.ExpressionType.Neutral;
    private float lastHappyIntensity;
    private float lastSadIntensity;
    private float lastAngryIntensity;
    private float lastSurprisedIntensity;
    private float lastFearfulIntensity;
    private float lastDisgustedIntensity;
    private float lastBlinkIntensity;

    // Componenti modulari
    private ExpressionMapping expressionMapping;
    private BlendShapeHelper blendShapeHelper;
    private BlinkingManager blinkingManager;
    private ExpressionEventSystem eventSystem;

    // Dizionari per i valori delle blend shapes
    private Dictionary<string, float> currentBlendShapeValues = new Dictionary<string, float>();
    private Dictionary<string, float> targetBlendShapeValues = new Dictionary<string, float>();

    // Coroutine per espressioni transitorie
    private Dictionary<ExpressionMapping.ExpressionType, Coroutine> transitionCoroutines = new Dictionary<ExpressionMapping.ExpressionType, Coroutine>();

    /// <summary>
    /// Inizializzazione del controller.
    /// </summary>
    void Start() {
        // Inizializza i valori precedenti
        UpdateLastIntensities();

        // Ottiene il componente AdvancedAvatarSetup
        if (avatarSetup == null) {
            avatarSetup = GetComponent<AvatarManager>();
            if (avatarSetup == null) {
                //Debug.LogError("AdvancedExpressionController: Impossibile trovare il componente AdvancedAvatarSetup.");
                return;
            }
        }

        // Inizializza i componenti modulari
        expressionMapping = new ExpressionMapping();
        eventSystem = new ExpressionEventSystem();

        // Iscrizione all'evento di caricamento dell'avatar
        avatarSetup.OnAvatarLoaded += OnAvatarLoaded;

        // Configura gli eventi
        eventSystem.OnExpressionChanged += OnExpressionChanged;

        // Se l'avatar è già stato caricato, inizializza subito
        if (avatarSetup.GetAvatar() != null) {
            OnAvatarLoaded(avatarSetup.GetAvatar());
        }
    }

    /// <summary>
    /// Aggiornamento del controller ad ogni frame.
    /// </summary>
    void Update() {
        if (blendShapeHelper == null || !blendShapeHelper.IsInitialized())
            return;

        // Verifica se l'espressione è cambiata
        if (currentExpression != lastExpression) {
            // Verifica se l'espressione precedente era transitoria
            if (eventSystem.IsTransitoryExpression(lastExpression)) {
                CancelTransitoryExpression(lastExpression);
            }

            // Imposta la nuova espressione
            float intensity = GetIntensityForExpression(currentExpression);
            eventSystem.NotifyExpressionChange(currentExpression, intensity);

            // Aggiorna i valori memorizzati
            lastExpression = currentExpression;
            UpdateLastIntensities();
        }

        // Verifica se le intensità sono cambiate per l'espressione corrente
        if (IntensitiesChanged()) {
            float intensity = GetIntensityForExpression(currentExpression);
            eventSystem.NotifyExpressionChange(currentExpression, intensity);
            UpdateLastIntensities();

            // Aggiorna le impostazioni del blinking se necessario
            if (blinkingManager != null && Math.Abs(lastBlinkIntensity - blinkIntensity) > 0.0001f) {
                blinkingManager.SetBlinkIntensity(blinkIntensity);
                lastBlinkIntensity = blinkIntensity;
            }
        }

        //NOTE TO IMPROVE!!!
        // Interpola tutti i valori delle blend shapes
        if (!blinkingManager.IsBlinking())
            foreach (var shapeName in targetBlendShapeValues.Keys) {
                if (!currentBlendShapeValues.ContainsKey(shapeName)) {
                    currentBlendShapeValues[shapeName] = 0f;
                }

                // Interpola il valore corrente verso il valore target
                currentBlendShapeValues[shapeName] = Mathf.Lerp(
                    currentBlendShapeValues[shapeName],
                    targetBlendShapeValues[shapeName],
                    Time.deltaTime * transitionSpeed
                );

                // Applica il valore alla blend shape
                blendShapeHelper.SetBlendShapeWeight(shapeName, currentBlendShapeValues[shapeName]);
            }
    }

    /// <summary>
    /// Handler per l'evento di caricamento dell'avatar.
    /// </summary>
    private void OnAvatarLoaded(GameObject avatar) {
        //Debug.Log("AdvancedExpressionController: Avatar caricato, inizializzazione controllo espressioni...");

        // Inizializza il BlendShapeHelper con l'avatar
        blendShapeHelper = new BlendShapeHelper(avatar);
        if (!blendShapeHelper.IsInitialized()) {
            //Debug.LogError("ExpressionController: BlendShapeHelper non è stato inizializzato correttamente.");
            return;
        }

        if (blendShapeHelper.IsInitialized()) {
            // Inizializza il BlinkingManager
            //blinkingManager = new BlinkingManager(
            //    blendShapeHelper,
            //    this,
            //    blinkIntensity,
            //    minBlinkInterval,
            //    maxBlinkInterval,
            //    enableBlinking && blinkingManager.IsCompatibleWithExpression(currentExpression)
            //);

            //Debug.Log($"BlinkingManager inizializzato. Blinking abilitato: {enableBlinking}, Compatibile con espressione corrente: {blinkingManager.IsCompatibleWithExpression(currentExpression)}");


            // Inizializza i dizionari dei valori di blend shape
            string[] blendShapeNames = blendShapeHelper.GetAllBlendShapeNames();
            if (blendShapeNames != null) {
                foreach (var name in blendShapeNames) {
                    currentBlendShapeValues[name] = 0f;
                    targetBlendShapeValues[name] = 0f;
                }
            } else {
                Debug.LogError("ExpressionController: Impossibile ottenere i nomi delle blend shapes.");
                return;
            }

            // Imposta l'espressione iniziale
            SetExpression(currentExpression);

            blinkingManager = new BlinkingManager(
                blendShapeHelper,
                this,
                blinkIntensity,
                minBlinkInterval,
                maxBlinkInterval,
                enableBlinking
            );


            Debug.Log("AdvancedExpressionController: Inizializzazione completata con successo.");
        } else {
            Debug.LogError("AdvancedExpressionController: Impossibile inizializzare il BlendShapeHelper.");
        }
    }

    /// <summary>
    /// Handler per l'evento di cambiamento espressione.
    /// </summary>
    private void OnExpressionChanged(object sender, ExpressionEventSystem.ExpressionEventArgs e) {
        // Applica i valori dell'espressione
        ApplyExpression(e.NewExpression, e.Intensity);

        // Gestisci le espressioni transitorie
        if (eventSystem.IsTransitoryExpression(e.NewExpression) && !e.IsAutomaticTransition) {
            float duration = eventSystem.GetTransitionDuration(e.NewExpression);
            StartTransitoryExpression(e.NewExpression, duration);
        }

        // Gestisci il blinking in base alla compatibilità
        if (blinkingManager != null) {
            bool shouldBlink = enableBlinking && blinkingManager.IsCompatibleWithExpression(e.NewExpression);

            if (shouldBlink && !blinkingManager.IsEnabled()) {
                blinkingManager.StartBlinking();
            } else if (!shouldBlink && blinkingManager.IsEnabled()) {
                blinkingManager.StopBlinking();
            }
        }
    }

    /// <summary>
    /// Avvia una coroutine per la transizione automatica di un'espressione transitoria.
    /// </summary>
    private void StartTransitoryExpression(ExpressionMapping.ExpressionType expression, float duration) {
        // Cancella qualsiasi transizione esistente per questa espressione
        CancelTransitoryExpression(expression);

        // Avvia una nuova coroutine per la transizione
        Coroutine coroutine = StartCoroutine(TransitionAfterDelay(expression, duration));
        transitionCoroutines[expression] = coroutine;
    }

    /// <summary>
    /// Cancella una transizione automatica in corso.
    /// </summary>
    private void CancelTransitoryExpression(ExpressionMapping.ExpressionType expression) {
        if (transitionCoroutines.TryGetValue(expression, out Coroutine coroutine)) {
            if (coroutine != null) {
                StopCoroutine(coroutine);
            }
            transitionCoroutines.Remove(expression);
        }
    }

    /// <summary>
    /// Coroutine per la transizione automatica dopo un ritardo.
    /// </summary>
    private IEnumerator TransitionAfterDelay(ExpressionMapping.ExpressionType fromExpression, float delay) {
        yield return new WaitForSeconds(delay);

        // Verifica se l'espressione corrente è ancora quella da cui transire
        if (currentExpression == fromExpression) {
            // Transizione all'espressione neutrale
            currentExpression = ExpressionMapping.ExpressionType.Neutral;
            float intensity = GetIntensityForExpression(ExpressionMapping.ExpressionType.Neutral);

            // Notifica il cambiamento con flag di transizione automatica a true
            eventSystem.NotifyExpressionChange(currentExpression, intensity, true);

            // Aggiorna anche la variabile serializzata
            lastExpression = currentExpression;
        }

        // Rimuovi la coroutine dal dizionario
        if (transitionCoroutines.ContainsKey(fromExpression)) {
            transitionCoroutines.Remove(fromExpression);
        }
    }

    /// <summary>
    /// Applica i valori di una specifica espressione alle blend shapes.
    /// </summary>
    private void ApplyExpression(ExpressionMapping.ExpressionType expression, float intensity) {
        //// Resetta i valori target
        //foreach (var key in targetBlendShapeValues.Keys) {
        //    targetBlendShapeValues[key] = 0f;
        //}

        var shapesToReset = new Dictionary<string, float>(targetBlendShapeValues);
        foreach (var key in shapesToReset.Keys) {
            targetBlendShapeValues[key] = 0f;
        }

        // Ottieni i valori dell'espressione
        Dictionary<string, float> expressionValues = expressionMapping.GetExpressionValues(expression);

        // Applica i valori alle blend shapes target
        foreach (var pair in expressionValues) {
            string shapeName = pair.Key;
            float value = pair.Value * intensity * 100f; // Moltiplica per 100 per il range 0-100 di Unity

            if (targetBlendShapeValues.ContainsKey(shapeName)) {
                targetBlendShapeValues[shapeName] = value;
            }
        }
    }

    /// <summary>
    /// Imposta l'espressione facciale.
    /// </summary>
    /// <param name="expression">L'espressione da impostare.</param>
    private void SetExpression(ExpressionMapping.ExpressionType expression) {
        currentExpression = expression;

        // Il cambiamento effettivo viene gestito in Update
    }

    /// <summary>
    /// Ottiene l'intensità appropriata per una specifica espressione.
    /// </summary>
    private float GetIntensityForExpression(ExpressionMapping.ExpressionType expression) {
        switch (expression) {
            case ExpressionMapping.ExpressionType.Happy: return happyIntensity;
            case ExpressionMapping.ExpressionType.Sad: return sadIntensity;
            case ExpressionMapping.ExpressionType.Angry: return angryIntensity;
            case ExpressionMapping.ExpressionType.Surprised: return surprisedIntensity;
            case ExpressionMapping.ExpressionType.Fearful: return fearfulIntensity;
            case ExpressionMapping.ExpressionType.Disgusted: return disgustedIntensity;
            default: return 0f; // Neutral o valore di default
        }
    }

    /// <summary>
    /// Verifica se le intensità delle espressioni sono cambiate.
    /// </summary>
    private bool IntensitiesChanged() {
        switch (currentExpression) {
            case ExpressionMapping.ExpressionType.Happy:
                return !Mathf.Approximately(happyIntensity, lastHappyIntensity);
            case ExpressionMapping.ExpressionType.Sad:
                return !Mathf.Approximately(sadIntensity, lastSadIntensity);
            case ExpressionMapping.ExpressionType.Angry:
                return !Mathf.Approximately(angryIntensity, lastAngryIntensity);
            case ExpressionMapping.ExpressionType.Surprised:
                return !Mathf.Approximately(surprisedIntensity, lastSurprisedIntensity);
            case ExpressionMapping.ExpressionType.Fearful:
                return !Mathf.Approximately(fearfulIntensity, lastFearfulIntensity);
            case ExpressionMapping.ExpressionType.Disgusted:
                return !Mathf.Approximately(disgustedIntensity, lastDisgustedIntensity);
            default:
                return false;
        }
    }

    /// <summary>
    /// Aggiorna i valori memorizzati delle intensità.
    /// </summary>
    private void UpdateLastIntensities() {
        lastHappyIntensity = happyIntensity;
        lastSadIntensity = sadIntensity;
        lastAngryIntensity = angryIntensity;
        lastSurprisedIntensity = surprisedIntensity;
        lastFearfulIntensity = fearfulIntensity;
        lastDisgustedIntensity = disgustedIntensity;
        lastBlinkIntensity = blinkIntensity;
    }

    /// <summary>
    /// Pulisce le risorse quando l'oggetto viene distrutto.
    /// </summary>
    private void OnDestroy() {
        // Ferma eventuali coroutine attive
        foreach (var coroutine in transitionCoroutines.Values) {
            if (coroutine != null) {
                StopCoroutine(coroutine);
            }
        }
        transitionCoroutines.Clear();

        // Pulisci gli eventi
        if (eventSystem != null) {
            eventSystem.OnExpressionChanged -= OnExpressionChanged;
        }

        // Ferma il blinking
        if (blinkingManager != null) {
            blinkingManager.StopBlinking();
        }
    }

    #region API Pubblica

    /// <summary>
    /// Imposta l'espressione corrente.
    /// </summary>
    /// <param name="expression">L'espressione da impostare.</param>
    public void SetCurrentExpression(ExpressionMapping.ExpressionType expression) {
        currentExpression = expression;
    }

    /// <summary>
    /// Imposta l'intensità dell'espressione felice.
    /// </summary>
    /// <param name="intensity">L'intensità (0-0.1).</param>
    public void SetHappyIntensity(float intensity) {
        happyIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
    }

    /// <summary>
    /// Imposta l'intensità dell'espressione triste.
    /// </summary>
    /// <param name="intensity">L'intensità (0-0.1).</param>
    public void SetSadIntensity(float intensity) {
        sadIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
    }

    /// <summary>
    /// Imposta l'intensità dell'espressione arrabbiata.
    /// </summary>
    /// <param name="intensity">L'intensità (0-0.1).</param>
    public void SetAngryIntensity(float intensity) {
        angryIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
    }

    /// <summary>
    /// Imposta l'intensità dell'espressione sorpresa.
    /// </summary>
    /// <param name="intensity">L'intensità (0-0.1).</param>
    public void SetSurprisedIntensity(float intensity) {
        surprisedIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
    }

    /// <summary>
    /// Imposta l'intensità dell'espressione spaventata.
    /// </summary>
    /// <param name="intensity">L'intensità (0-0.1).</param>
    public void SetFearfulIntensity(float intensity) {
        fearfulIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
    }

    /// <summary>
    /// Imposta l'intensità dell'espressione disgustata.
    /// </summary>
    /// <param name="intensity">L'intensità (0-0.1).</param>
    public void SetDisgustedIntensity(float intensity) {
        disgustedIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
    }

    /// <summary>
    /// Imposta l'intensità del battito delle palpebre.
    /// </summary>
    /// <param name="intensity">L'intensità (0-0.1).</param>
    public void SetBlinkIntensity(float intensity) {
        blinkIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
        if (blinkingManager != null) {
            blinkingManager.SetBlinkIntensity(blinkIntensity);
        }
    }

    /// <summary>
    /// Abilita o disabilita il battito delle palpebre.
    /// </summary>
    /// <param name="enable">Se abilitare il battito.</param>
    public void SetBlinkingEnabled(bool enable) {
        enableBlinking = enable;
        if (blinkingManager != null) {
            if (enable && blinkingManager.IsCompatibleWithExpression(currentExpression)) {
                blinkingManager.StartBlinking();
            } else {
                blinkingManager.StopBlinking();
            }
        }
    }

    ///// <summary>
    ///// Forza un battito delle palpebre immediato.
    ///// </summary>
    //public void ForceBlink() {
    //    if (blinkingManager != null) {
    //        blinkingManager.ForceBlink();
    //    }
    //}

    /// <summary>
    /// Imposta la velocità di transizione tra le espressioni.
    /// </summary>
    /// <param name="speed">La velocità di transizione (1-10).</param>
    public void SetTransitionSpeed(float speed) {
        transitionSpeed = Mathf.Clamp(speed, 1f, 10f);
    }

    /// <summary>
    /// Imposta la durata di un'espressione transitoria.
    /// </summary>
    /// <param name="expression">L'espressione transitoria.</param>
    /// <param name="duration">La durata in secondi.</param>
    public void SetTransitionDuration(ExpressionMapping.ExpressionType expression, float duration) {
        if (eventSystem != null) {
            eventSystem.SetTransitionDuration(expression, duration);
        }
    }

    /// <summary>
    /// Aggiunge un ascoltatore per l'evento di cambiamento espressione.
    /// </summary>
    /// <param name="listener">Il metodo da chiamare quando l'espressione cambia.</param>
    public void AddExpressionChangedListener(EventHandler<ExpressionEventSystem.ExpressionEventArgs> listener) {
        if (eventSystem != null) {
            eventSystem.OnExpressionChanged += listener;
        }
    }

    /// <summary>
    /// Rimuove un ascoltatore per l'evento di cambiamento espressione.
    /// </summary>
    /// <param name="listener">Il metodo da rimuovere.</param>
    public void RemoveExpressionChangedListener(EventHandler<ExpressionEventSystem.ExpressionEventArgs> listener) {
        if (eventSystem != null) {
            eventSystem.OnExpressionChanged -= listener;
        }
    }

    /// <summary>
    /// Ottiene l'espressione corrente.
    /// </summary>
    /// <returns>L'espressione corrente.</returns>
    public ExpressionMapping.ExpressionType GetCurrentExpression() {
        return currentExpression;
    }

    /// <summary>
    /// Ottiene l'intensità dell'espressione corrente.
    /// </summary>
    /// <returns>L'intensità corrente.</returns>
    public float GetCurrentIntensity() {
        return GetIntensityForExpression(currentExpression);
    }

    #endregion
}