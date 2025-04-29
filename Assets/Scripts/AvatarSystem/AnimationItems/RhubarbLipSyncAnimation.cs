using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

/// <summary>
/// Classe che gestisce l'animazione di sincronizzazione labiale utilizzando i dati di Rhubarb.
/// Estende FaceAnimationItem per integrarsi con il sistema di scheduling delle animazioni.
/// </summary>
public class RhubarbLipSyncAnimation : FaceAnimationItem
{
    // L'audio da riprodurre
    private AudioClip audioClip;
    
    // I dati di sincronizzazione labiale
    private RhubarbData lipSyncData;
    
    /// Classe che rappresenta un singolo frame di animazione per la sincronizzazione labiale utilizzando Rhubarb.
    [Serializable]
    public class MouthCue
    {
        public float start;  // Tempo di inizio in secondi
        public float end;    // Tempo di fine in secondi
        public string value; // Visema (A-H, X)
    }

    /// Classe contenitore per i dati di sincronizzazione labiale di Rhubarb.
    [Serializable]
    public class RhubarbData
    {
        public List<MouthCue> mouthCues = new List<MouthCue>();
    }

    // Dizionario di mappatura dai visemi di Rhubarb alle blend shapes ARKit
    private Dictionary<string, Dictionary<string, float>> visemeToBlendShapeMapping = new Dictionary<string, Dictionary<string, float>>
    {
        // A - Bocca chiusa per i suoni "P", "B", "M"
        { "A", new Dictionary<string, float> {
            { "mouthClose", 0.8f },
            { "mouthPucker", 0.1f }
        }},
        
        // B - Bocca leggermente aperta con denti stretti (consonanti e "EE")
        { "B", new Dictionary<string, float> {
            { "jawOpen", 0.2f },
            { "mouthClose", 0.3f },
            { "mouthStretchLeft", 0.3f },
            { "mouthStretchRight", 0.3f }
        }},
        
        // C - Bocca aperta per vocali come "E", "EH" in men
        { "C", new Dictionary<string, float> {
            { "jawOpen", 0.4f },
            { "mouthClose", 0.0f }
        }},
        
        // D - Bocca completamente aperta "AA" come in father
        { "D", new Dictionary<string, float> {
            { "jawOpen", 0.7f },
            { "mouthClose", 0.0f }
        }},
        
        // E - Bocca leggermente arrotondata per "AO" come in off
        { "E", new Dictionary<string, float> {
            { "jawOpen", 0.3f },
            { "mouthFunnel", 0.4f }
        }},
        
        // F - Labbra sporgenti per "UW" e "OW" e "W"
        { "F", new Dictionary<string, float> {
            { "jawOpen", 0.2f },
            { "mouthPucker", 0.6f }
        }},
        
        // G - Denti superiori su labbro inferiore per "F" e "V"
        { "G", new Dictionary<string, float> {
            { "jawOpen", 0.3f },
            { "mouthClose", 0.1f },
            { "mouthLowerDownLeft", 0.5f },
            { "mouthLowerDownRight", 0.5f },
            { "mouthPressLeft", 0.3f },
            { "mouthPressRight", 0.3f }
        }},
        
        // H - Lingua visibile per i suoni "L" prolungati
        { "H", new Dictionary<string, float> {
            { "jawOpen", 0.5f },
            { "tongueOut", 0.3f }
        }},
        
        // X - Posizione di riposo (bocca chiusa e rilassata)
        { "X", new Dictionary<string, float> {
            { "mouthClose", 0.5f }
        }}
    };
    
    // Lista di tutte le blend shapes utilizzate per il lip sync
    private List<string> allUsedBlendShapes;
    
    // Indice del visema corrente
    private int currentVisemeIndex = 0;
    
    // Flag che indica se l'animazione è completata
    private bool isComplete = false;
    
    // Riferimento all'AudioSource
    private AudioSource audioSource;
    
    // Valori originali delle blend shapes
    private Dictionary<string, float> originalBlendShapeValues;

    // Intensità dei visemi e velocità di interpolazione
    private float visemeIntensity = 1.2f;
    private float lerpSpeed = 0.2f;

    // Valori correnti delle blend shapes per l'interpolazione
    private Dictionary<string, float> currentBlendShapeValues = new Dictionary<string, float>();
    
    /// <summary>
    /// Costruttore che inizializza l'animazione con audio e dati di sincronizzazione.
    /// </summary>
    /// <param name="audioSource">L'AudioSource da utilizzare per la riproduzione</param>
    /// <param name="clip">Il clip audio da riprodurre</param>
    /// <param name="lipSyncJson">I dati di sincronizzazione in formato JSON</param>
    public RhubarbLipSyncAnimation(AudioSource audioSource, AudioClip clip, string lipSyncJson)
    {
        this.audioSource = audioSource;
        this.audioClip = clip;
        this.lipSyncData = JsonUtility.FromJson<RhubarbData>(lipSyncJson);
        
        // Inizializza la lista di tutte le blend shapes utilizzate
        InitializeAllUsedBlendShapes();
    }

    /// <summary>
    /// Inizializza la lista di tutte le blend shapes utilizzate dai visemi.
    /// </summary>
    private void InitializeAllUsedBlendShapes()
    {
        allUsedBlendShapes = new List<string>();
        HashSet<string> uniqueBlendShapes = new HashSet<string>();
        
        // Raccogli tutte le blend shapes uniche utilizzate nella mappatura
        foreach (var mapping in visemeToBlendShapeMapping.Values)
        {
            foreach (var blendShape in mapping.Keys)
            {
                uniqueBlendShapes.Add(blendShape);
            }
        }
        
        // Converti il set in lista
        allUsedBlendShapes.AddRange(uniqueBlendShapes);
    }

    /// <summary>
    /// Restituisce la priorità dell'animazione nello scheduler.
    /// </summary>
    /// <returns>Un valore alto di priorità (90)</returns>
    public override int GetPriority()
    {
        return 90; // Alta priorità per garantire che venga eseguita sopra altre animazioni
    }

    /// <summary>
    /// Verifica se l'animazione è completata.
    /// </summary>
    /// <returns>True se l'animazione è terminata</returns>
    public override bool IsComplete()
    {
        return isComplete;
    }

    /// <summary>
    /// Coroutine principale che gestisce il ciclo di vita dell'animazione di lip sync.
    /// </summary>
    /// <param name="currentBlendShapeValues">I valori correnti delle blend shapes</param>
    /// <param name="blendShapeHelper">Helper per manipolare le blend shapes</param>
    public override IEnumerator AnimationCoroutine(Dictionary<string, float> currentBlendShapeValues, BlendShapeHelper blendShapeHelper)
    {
        if (audioClip == null || lipSyncData == null || lipSyncData.mouthCues.Count == 0)
        {
            Debug.LogWarning("RhubarbLipSyncAnimation: AudioClip o dati di sincronizzazione non validi!");
            isComplete = true;
            yield break;
        }

        // Salva i valori originali delle blend shapes
        SaveOriginalBlendShapeValues(currentBlendShapeValues);
        
        // Inizializza i valori correnti
        this.currentBlendShapeValues = new Dictionary<string, float>();
        foreach (var blendShape in allUsedBlendShapes)
        {
            this.currentBlendShapeValues[blendShape] = currentBlendShapeValues.ContainsKey(blendShape) 
                ? currentBlendShapeValues[blendShape] : 0f;
        }

        audioSource.clip = audioClip;
        audioSource.playOnAwake = false;
        
        // Resetta l'indice del visema corrente
        currentVisemeIndex = 0;
        
        // Avvia la riproduzione audio
        audioSource.Play();
        Debug.Log("RhubarbLipSyncAnimation: Inizio animazione di sincronizzazione labiale.");
        
        // Tempo di inizio per calcolare il tempo trascorso
        float startTime = Time.time;
        
        // Visema corrente e precedente
        string currentViseme = "X";
        
        // Continua finché l'audio è in riproduzione
        while (audioSource.isPlaying)
        {
            // Tempo attuale all'interno dell'audio
            float currentAudioTime = Time.time - startTime;
            
            // Trova il visema corrente in base al tempo
            string newViseme = "X"; // Default a riposo
            
            for (int i = 0; i < lipSyncData.mouthCues.Count; i++)
            {
                var cue = lipSyncData.mouthCues[i];
                if (currentAudioTime >= cue.start && currentAudioTime < cue.end)
                {
                    newViseme = cue.value;
                    break;
                }
            }
            
            // Se il visema è cambiato, applicalo
            if (newViseme != currentViseme)
            {
                currentViseme = newViseme;
                Debug.Log($"RhubarbLipSyncAnimation: Cambio visema a {currentViseme} al tempo {currentAudioTime:F2}s");
                
                // Applica il nuovo visema
                ApplyViseme(currentViseme, blendShapeHelper);
            }
            
            yield return null;
        }
        
        // Assicurati che l'audio sia completamente terminato
        while (audioSource.isPlaying)
        {
            yield return null;
        }
        
        // Ripristina i valori originali delle blend shapes
        RestoreOriginalBlendShapeValues(blendShapeHelper);
        Debug.Log("RhubarbLipSyncAnimation: Animazione di sincronizzazione labiale terminata.");
        
        isComplete = true;
    }
    
    /// <summary>
    /// Salva i valori originali delle blend shapes che verranno modificate.
    /// </summary>
    /// <param name="currentValues">I valori correnti delle blend shapes</param>
    private void SaveOriginalBlendShapeValues(Dictionary<string, float> currentValues)
    {
        originalBlendShapeValues = new Dictionary<string, float>();
        
        // Salva i valori di tutte le blend shapes che potrebbero essere modificate
        foreach (var blendShape in allUsedBlendShapes)
        {
            if (currentValues.ContainsKey(blendShape))
            {
                originalBlendShapeValues[blendShape] = currentValues[blendShape];
            }
            else
            {
                originalBlendShapeValues[blendShape] = 0f;
            }
        }
    }
    
    /// <summary>
    /// Applica un visema specifico alle blend shapes.
    /// </summary>
    /// <param name="viseme">Il visema da applicare (A-H, X)</param>
    /// <param name="blendShapeHelper">Helper per manipolare le blend shapes</param>
    private void ApplyViseme(string viseme, BlendShapeHelper blendShapeHelper)
    {
        // Ottieni la mappatura per questo visema
        if (visemeToBlendShapeMapping.TryGetValue(viseme, out Dictionary<string, float> blendShapeValues))
        {
            // Crea un dizionario per i valori target
            Dictionary<string, float> targetValues = new Dictionary<string, float>();
            
            // Inizializza tutti i target a 0
            foreach (var blendShape in allUsedBlendShapes)
            {
                targetValues[blendShape] = 0f;
            }
            
            // Imposta i valori target dalle mappature
            foreach (var pair in blendShapeValues)
            {
                targetValues[pair.Key] = pair.Value * visemeIntensity;
            }
            
            // Interpola tra i valori correnti e i target
            foreach (var blendShape in allUsedBlendShapes)
            {
                float currentValue = currentBlendShapeValues.ContainsKey(blendShape) 
                    ? currentBlendShapeValues[blendShape] : 0f;
                
                float targetValue = targetValues.ContainsKey(blendShape) 
                    ? targetValues[blendShape] : 0f;
                
                // Interpola tra il valore corrente e il target
                float newValue = Mathf.Lerp(currentValue, targetValue, lerpSpeed);
                
                // Applica il nuovo valore
                blendShapeHelper.SetBlendShapeWeight(blendShape, newValue);
                
                // Aggiorna il valore corrente
                currentBlendShapeValues[blendShape] = newValue;
            }
        }
        else
        {
            Debug.LogWarning($"RhubarbLipSyncAnimation: Visema '{viseme}' non trovato nella mappatura.");
        }
    }
    
    /// <summary>
    /// Ripristina i valori originali delle blend shapes.
    /// </summary>
    /// <param name="blendShapeHelper">Helper per manipolare le blend shapes</param>
    private void RestoreOriginalBlendShapeValues(BlendShapeHelper blendShapeHelper)
    {
        if (originalBlendShapeValues != null)
        {
            foreach (var pair in originalBlendShapeValues)
            {
                blendShapeHelper.SetBlendShapeWeight(pair.Key, pair.Value);
            }
        }
    }
}