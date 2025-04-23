using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestisce il battito delle palpebre per l'avatar.
/// </summary>
public class BlinkingManager
{
    // Riferimento al BlendShapeHelper
    private BlendShapeHelper blendShapeHelper;
    
    // Riferimento al MonoBehaviour per le coroutine
    private MonoBehaviour coroutineHost;
    
    // Parametri di configurazione
    private float minBlinkInterval = 2.0f;
    private float maxBlinkInterval = 6.0f;
    
    // Stato corrente
    private bool isEnabled = false;
    private bool isBlinking = false;
    
    // Coroutine di blinking
    private Coroutine blinkingCoroutine = null;
    
    // Animazione di blink corrente
    private BlinkAnimation currentBlinkAnimation = null;
    
    // Scheduler per le animazioni
    private AnimationScheduler animationScheduler;
    
    /// <summary>
    /// Costruttore del BlinkingManager.
    /// </summary>
    /// <param name="blendShapeHelper">Helper per le blend shapes</param>
    /// <param name="coroutineHost">MonoBehaviour su cui eseguire le coroutine</param>
    /// <param name="blinkIntensity">Intensità del battito delle palpebre</param>
    /// <param name="minBlinkInterval">Intervallo minimo tra battiti (secondi)</param>
    /// <param name="maxBlinkInterval">Intervallo massimo tra battiti (secondi)</param>
    /// <param name="startEnabled">Se abilitare il battito all'avvio</param>
    public BlinkingManager(BlendShapeHelper blendShapeHelper, MonoBehaviour coroutineHost, AnimationScheduler scheduler)
    {
        this.blendShapeHelper = blendShapeHelper;
        this.coroutineHost = coroutineHost;
        this.animationScheduler = scheduler;
        
        StartBlinking();
    }
    
    /// <summary>
    /// Avvia il battito delle palpebre automatico.
    /// </summary>
    public void StartBlinking()
    {
        if (isEnabled)
            return;
            
        isEnabled = true;
        
        // Avvia la coroutine di blinking
        if (coroutineHost != null && blinkingCoroutine == null)
        {
            blinkingCoroutine = coroutineHost.StartCoroutine(BlinkingCoroutine());
        }
    }
    
    /// <summary>
    /// Ferma il battito delle palpebre automatico.
    /// </summary>
    public void StopBlinking()
    {
        if (!isEnabled)
            return;
            
        isEnabled = false;
        
        // Ferma la coroutine di blinking
        if (coroutineHost != null && blinkingCoroutine != null)
        {
            coroutineHost.StopCoroutine(blinkingCoroutine);
            blinkingCoroutine = null;
        }
    }
        
    /// <summary>
    /// Coroutine che gestisce il battito delle palpebre a intervalli casuali.
    /// </summary>
    private IEnumerator BlinkingCoroutine()
    {
        while (isEnabled)
        {
            // Attende un intervallo casuale
            float interval = UnityEngine.Random.Range(minBlinkInterval, maxBlinkInterval);
            yield return new WaitForSeconds(interval);
            
            // Verifica se è ancora abilitato
            if (!isEnabled)
                break;
                
            // Crea e schedula un'animazione di blink
            CreateAndScheduleBlinkAnimation();
        }
        
        blinkingCoroutine = null;
    }
    
    /// <summary>
    /// Crea e schedula un'animazione di battito delle palpebre.
    /// </summary>
    private void CreateAndScheduleBlinkAnimation()
    {
        // Crea una nuova animazione di blink
        currentBlinkAnimation = new BlinkAnimation();
        
        // Schedula l'animazione
        animationScheduler.EnqueueAnimation(currentBlinkAnimation);
        
        // Imposta lo stato di blinking
        isBlinking = true;
        
        // Avvia una coroutine per resettare lo stato dopo la durata del blink
        coroutineHost.StartCoroutine(ResetBlinkingState(0.3f)); // Durata totale del blink (incluse transizioni)
    }
    
    /// <summary>
    /// Coroutine per resettare lo stato di blinking dopo un certo tempo.
    /// </summary>
    private IEnumerator ResetBlinkingState(float delay)
    {
        yield return new WaitForSeconds(delay);
        isBlinking = false;
        currentBlinkAnimation = null;
    }
    
    /// <summary>
    /// Imposta gli intervalli del battito delle palpebre.
    /// </summary>
    /// <param name="minInterval">L'intervallo minimo in secondi</param>
    /// <param name="maxInterval">L'intervallo massimo in secondi</param>
    public void SetBlinkIntervals(float minInterval, float maxInterval)
    {
        minBlinkInterval = Mathf.Max(0.5f, minInterval);
        maxBlinkInterval = Mathf.Max(minBlinkInterval + 0.5f, maxInterval);
    }
    
    /// <summary>
    /// Verifica se il battito delle palpebre è attualmente abilitato.
    /// </summary>
    /// <returns>True se abilitato, false altrimenti</returns>
    public bool IsEnabled()
    {
        return isEnabled;
    }
    
    /// <summary>
    /// Verifica se è in corso un battito delle palpebre.
    /// </summary>
    /// <returns>True se è in corso un battito, false altrimenti</returns>
    public bool IsBlinking()
    {
        return isBlinking;
    }
}