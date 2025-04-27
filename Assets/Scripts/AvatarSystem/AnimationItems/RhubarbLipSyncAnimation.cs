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

    List<MouthCue> mouthCues = new List<MouthCue>();
    

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

    // Dizionario di mappatura dai visemi di Rhubarb ai blend shapes
    private Dictionary<string, string> visemeToBlendShape = new Dictionary<string, string>
    {
        { "A", "viseme_PP" },  // Bocca chiusa
        { "B", "viseme_kk" },  // Denti stretti
        { "C", "viseme_I" },   // Bocca aperta come "E"
        { "D", "viseme_AA" },  // Bocca molto aperta
        { "E", "viseme_O" },   // Bocca arrotondata
        { "F", "viseme_U" },   // Labbra sporgenti
        { "G", "viseme_FF" },  // Denti superiori su labbro inferiore
        { "H", "viseme_TH" },  // Lingua visibile
        { "X", "viseme_PP" }   // Riposo (chiusa)
    };
    
    // Indice del visema corrente
    private int currentVisemeIndex = 0;
    
    // Flag che indica se l'animazione è completata
    private bool isComplete = false;
    
    // Riferimento all'AudioSource
    private AudioSource audioSource;
    
    // Valori originali delle blend shapes
    private Dictionary<string, float> originalBlendShapeValues;

    // Intensità dei visemi
    private float visemeIntensity = 1.0f;
    private float lerpSpeed = 0.2f;

    private Dictionary<string, float> currentBlendShapeValues = new Dictionary<string, float>();
    
    /// <summary>
    /// Costruttore che inizializza l'animazione con audio e dati di sincronizzazione.
    /// </summary>
    /// <param name="clip">Il clip audio da riprodurre</param>
    /// <param name="lipSyncJson">I dati di sincronizzazione in formato JSON</param>
    public RhubarbLipSyncAnimation(AudioClip clip, string lipSyncJson)
    {
        this.audioClip = clip;
        this.lipSyncData = JsonUtility.FromJson<RhubarbData>(lipSyncJson);
    }
    
    /// <summary>
    /// Costruttore alternativo che accetta direttamente l'oggetto RhubarbData.
    /// </summary>
    /// <param name="clip">Il clip audio da riprodurre</param>
    /// <param name="data">I dati di sincronizzazione già deserializzati</param>
    public RhubarbLipSyncAnimation(AudioClip clip, RhubarbData data)
    {
        this.audioClip = clip;
        this.lipSyncData = data;
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
        Debug.Log("RhubarbLipSyncAnimation: Inizio animazione di sincronizzazione labiale.");

        
        if (audioClip == null || lipSyncData == null || lipSyncData.mouthCues.Count == 0)
        {
            Debug.LogWarning("RhubarbLipSyncAnimation: AudioClip o dati di sincronizzazione non validi!");
            isComplete = true;
            yield break;
        }

        // Salva i valori originali delle blend shapes
        SaveOriginalBlendShapeValues(currentBlendShapeValues);
        
        // Crea un GameObject temporaneo per l'AudioSource
        GameObject audioContainer = new GameObject("LipSyncAudioContainer");
        audioSource = audioContainer.AddComponent<AudioSource>();
        audioSource.clip = audioClip;
        audioSource.playOnAwake = false;
        
        // Resetta l'indice del visema corrente
        currentVisemeIndex = 0;
        
        // Avvia la riproduzione audio
        audioSource.Play();
        
        // Tempo di inizio per calcolare il tempo trascorso
        float startTime = Time.time;
        
        // Continua finché l'audio è in riproduzione e ci sono visemi da processare
        while (audioSource.isPlaying && currentVisemeIndex < lipSyncData.mouthCues.Count)
        {
            // Tempo attuale all'interno dell'audio
            float currentAudioTime = Time.time - startTime;
            
            // Trova il visema corrente in base al tempo
            while (currentVisemeIndex < lipSyncData.mouthCues.Count &&
                   currentAudioTime >= lipSyncData.mouthCues[currentVisemeIndex].end)
            {
                currentVisemeIndex++;
            }
            
            if (currentVisemeIndex < lipSyncData.mouthCues.Count &&
                currentAudioTime >= lipSyncData.mouthCues[currentVisemeIndex].start)
            {
                // Ottieni il valore del visema corrente
                string viseme = lipSyncData.mouthCues[currentVisemeIndex].value;
                
                // Applica il visema alle blend shapes
                ApplyViseme(viseme, blendShapeHelper);
            }
            else if (currentVisemeIndex >= lipSyncData.mouthCues.Count)
            {
                // Se abbiamo processato tutti i visemi, applica il visema di riposo
                ApplyViseme("X", blendShapeHelper);
            }
            
            yield return null;
        }
        
        // Assicurati che l'audio sia completamente terminato
        while (audioSource.isPlaying)
        {
            yield return null;
        }
        
        // Cleanup
        if (audioContainer != null)
        {
            GameObject.Destroy(audioContainer);
        }
        
        // Ripristina i valori originali delle blend shapes
        RestoreOriginalBlendShapeValues(blendShapeHelper);
        
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
        foreach (var blendShape in visemeToBlendShape.Values)
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
        // Ottieni il nome della blend shape per questo viseme
        if (visemeToBlendShape.TryGetValue(viseme, out string blendShapeName))
        {
            // Per ogni blend shape utilizzata per i visemi
            foreach (var blendShape in visemeToBlendShape.Values)
            {
                // Ottieni il valore corrente
                float currentValue = currentBlendShapeValues.ContainsKey(blendShape) 
                    ? currentBlendShapeValues[blendShape] : 0f;
                
                // Calcola il valore target (visemeIntensity se è la blend shape attiva, 0 altrimenti)
                float targetValue = (blendShape == blendShapeName) ? visemeIntensity : 0f;
                
                // Interpola tra il valore corrente e il target
                float newValue = Mathf.Lerp(currentValue, targetValue, lerpSpeed);
                
                // Applica il nuovo valore
                blendShapeHelper.SetBlendShapeWeight(blendShape, newValue);
                
                // Aggiorna il valore corrente
                currentBlendShapeValues[blendShape] = newValue;
            }
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