using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestisce l'animazione del battito delle palpebre per un avatar.
/// </summary>
public class BlinkingManager {
    // Configurazione del battito delle palpebre
    private float blinkIntensity;
    private float minBlinkInterval;
    private float maxBlinkInterval;
    private bool enableBlinking;

    // Riferimento agli strumenti necessari
    private readonly BlendShapeHelper blendShapeHelper;
    private readonly MonoBehaviour coroutineRunner;

    // Stato interno
    private Coroutine blinkCoroutine;
    private bool isBlinking = false;

    // Nomi standard delle blend shapes per le palpebre
    private readonly string[] eyeBlinkBlendShapes = new string[] { "eyeBlinkLeft", "eyeBlinkRight" };

    /// <summary>
    /// Crea una nuova istanza del BlinkingManager.
    /// </summary>
    /// <param name="blendShapeHelper">Il BlendShapeHelper da utilizzare per manipolare le blend shapes.</param>
    /// <param name="coroutineRunner">Un MonoBehaviour che può eseguire coroutine.</param>
    /// <param name="intensity">L'intensità del battito delle palpebre (0-1).</param>
    /// <param name="minInterval">L'intervallo minimo tra i battiti (secondi).</param>
    /// <param name="maxInterval">L'intervallo massimo tra i battiti (secondi).</param>
    /// <param name="enable">Se abilitare il battito delle palpebre all'inizio.</param>

    public BlinkingManager(
        BlendShapeHelper blendShapeHelper,
        MonoBehaviour coroutineRunner,
        float intensity = 0.03f,
        float minInterval = 2.0f,
        float maxInterval = 6.0f,
        bool enable = true)
    {
        this.blendShapeHelper = blendShapeHelper;
        this.coroutineRunner = coroutineRunner;
        this.blinkIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
        this.minBlinkInterval = minInterval;
        this.maxBlinkInterval = maxInterval;
        this.enableBlinking = enable;

        // Avvia il battito delle palpebre se abilitato
        if (enable) {
            StartBlinking();
        }

    }

    /// <summary>
    /// Avvia l'animazione del battito delle palpebre.
    /// </summary>
    public void StartBlinking() {
        //Debug.Log("BlinkingManager: Avvio del battito delle palpebre");

        // Verifica se il BlendShapeHelper è inizializzato
        if (!blendShapeHelper.IsInitialized()) {
            Debug.LogWarning("BlinkingManager: Impossibile avviare il battito delle palpebre. BlendShapeHelper non inizializzato.");
            return;
        }

        // Ferma qualsiasi coroutine esistente
        StopBlinking();

        // Abilita il blinking
        enableBlinking = true;

        // Avvia la coroutine per il battito delle palpebre
        blinkCoroutine = coroutineRunner.StartCoroutine(BlinkingRoutine());
    }

    /// <summary>
    /// Ferma l'animazione del battito delle palpebre.
    /// </summary>
    public void StopBlinking() {
        enableBlinking = false;

        // Ferma la coroutine se è in esecuzione
        if (blinkCoroutine != null) {
            coroutineRunner.StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;

            // Assicurati che gli occhi siano aperti
            ResetEyes();
        }
    }

    /// <summary>
    /// Imposta l'intensità del battito delle palpebre.
    /// </summary>
    /// <param name="intensity">L'intensità del battito (0-0.1).</param>
    public void SetBlinkIntensity(float intensity) {
        blinkIntensity = Mathf.Clamp(intensity, 0f, 0.1f);
    }

    /// <summary>
    /// Imposta l'intervallo tra i battiti delle palpebre.
    /// </summary>
    /// <param name="minInterval">L'intervallo minimo (secondi).</param>
    /// <param name="maxInterval">L'intervallo massimo (secondi).</param>
    public void SetBlinkInterval(float minInterval, float maxInterval) {
        this.minBlinkInterval = Mathf.Max(0.5f, minInterval);
        this.maxBlinkInterval = Mathf.Max(this.minBlinkInterval + 0.5f, maxInterval);
    }

    /// <summary>
    /// Verifica se una specifica espressione può essere compatibile con il battito delle palpebre.
    /// </summary>
    /// <param name="expressionType">Il tipo di espressione da verificare.</param>
    /// <returns>True se il battito delle palpebre è compatibile con l'espressione.</returns>
    public bool IsCompatibleWithExpression(ExpressionMapping.ExpressionType expressionType) {
        // Espressioni compatibili con il battito delle palpebre
        // In generale, espressioni che non coinvolgono in modo significativo gli occhi
        switch (expressionType) {
            case ExpressionMapping.ExpressionType.Neutral:
            case ExpressionMapping.ExpressionType.Happy:
                return true;

            case ExpressionMapping.ExpressionType.Sad:
            case ExpressionMapping.ExpressionType.Angry:
            case ExpressionMapping.ExpressionType.Surprised:
            case ExpressionMapping.ExpressionType.Fearful:
            case ExpressionMapping.ExpressionType.Disgusted:
                return false;

            default:
                return false;
        }
    }

    /// <summary>
    /// Coroutine che gestisce l'animazione del battito delle palpebre.
    /// </summary>
    private IEnumerator BlinkingRoutine() {
        //Debug.Log("BlinkingManager: Esecuzione della routine di blinking");

        while (enableBlinking) {
            // Attendi un intervallo casuale prima del prossimo battito
            yield return new WaitForSeconds(Random.Range(minBlinkInterval, maxBlinkInterval));

            isBlinking = true;
            //Debug.Log("blick -> closed");

            float weight = 100f * blinkIntensity;
            // Applica il valore delle blend shapes per chiudere gli occhi
            foreach (string shapeName in eyeBlinkBlendShapes) {
                if (blendShapeHelper.HasBlendShape(shapeName)) {
                     blendShapeHelper.SetBlendShapeWeight(shapeName, weight);
                }
     
            }


            // Attendi per mantenere gli occhi chiusi
            yield return new WaitForSeconds(0.15f);

            //Debug.Log("blick -> opened");

            // Riapri gli occhi
            ResetEyes();


            isBlinking = false;
        }
    }

    ///// <summary>
    ///// Forza un battito di palpebre immediato.
    ///// </summary>
    //public void ForceBlink() {
    //    if (!enableBlinking || isBlinking) return;

    //    coroutineRunner.StartCoroutine(Blink());
    //}

    /// <summary>
    /// Ripristina gli occhi alla posizione aperta.
    /// </summary>
    private void ResetEyes() {
        foreach (string shapeName in eyeBlinkBlendShapes) {
            if (blendShapeHelper.HasBlendShape(shapeName)) {
                blendShapeHelper.SetBlendShapeWeight(shapeName, 0f);
            }
        }
    }

    /// <summary>
    /// Verifica se il battito delle palpebre è attualmente abilitato.
    /// </summary>
    /// <returns>True se il battito è abilitato, false altrimenti.</returns>
    public bool IsEnabled() {
        return enableBlinking;
    }

    /// <summary>
    /// Verifica se l'avatar sta attualmente battendo le palpebre.
    /// </summary>
    /// <returns>True se l'avatar sta battendo le palpebre, false altrimenti.</returns>
    public bool IsBlinking() {
        return isBlinking;
    }
}