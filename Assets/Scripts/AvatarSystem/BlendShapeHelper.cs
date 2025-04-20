using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Helper class per gestire le blend shapes di un avatar.
/// Fornisce metodi per trovare il renderer della testa e gestire le blend shapes.
/// </summary>
public class BlendShapeHelper {
    // Il renderer della testa dell'avatar
    private SkinnedMeshRenderer headMeshRenderer;

    // Dizionario che mappa i nomi delle blend shapes ai loro indici
    private Dictionary<string, int> blendShapeIndices = new Dictionary<string, int>();

    // Flag che indica se le blend shapes sono state inizializzate
    private bool isInitialized = false;

    /// <summary>
    /// Costruttore che accetta un avatar e inizializza il helper.
    /// </summary>
    /// <param name="avatar">L'oggetto avatar da cui estrarre le blend shapes.</param>
    public BlendShapeHelper(GameObject avatar) {
        if (avatar != null) {
            Initialize(avatar);
        }
    }

    /// <summary>
    /// Inizializza il helper con un nuovo avatar.
    /// </summary>
    /// <param name="avatar">L'oggetto avatar da cui estrarre le blend shapes.</param>
    /// <returns>True se l'inizializzazione è riuscita, false altrimenti.</returns>
    public bool Initialize(GameObject avatar) {
        if (avatar == null) {
            Debug.LogError("BlendShapeHelper: L'avatar non può essere null!");
            isInitialized = false;
            return false;
        }

        // Trova il renderer della testa
        headMeshRenderer = FindHeadMeshRenderer(avatar);

        if (headMeshRenderer != null) {
            // Inizializza le blend shapes
            InitializeBlendShapeIndices();
            isInitialized = true;
            return true;
        } else {
            Debug.LogError("BlendShapeHelper: Impossibile trovare un renderer valido per la testa dell'avatar!");
            isInitialized = false;
            return false;
        }
    }

    /// <summary>
    /// Trova il SkinnedMeshRenderer della testa nell'avatar.
    /// </summary>
    /// <param name="avatar">L'oggetto avatar da cui cercare il renderer della testa.</param>
    /// <returns>Il SkinnedMeshRenderer della testa, o null se non trovato.</returns>
    private SkinnedMeshRenderer FindHeadMeshRenderer(GameObject avatar) {
        // In Ready Player Me, la mesh della testa è in genere uno dei SkinnedMeshRenderer sotto l'avatar
        SkinnedMeshRenderer[] renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>();

        // Cerca prima un renderer con "head" nel nome
        foreach (SkinnedMeshRenderer renderer in renderers) {
            if (renderer.name.ToLower().Contains("head") ||
                renderer.name.ToLower().Contains("face") ||
                renderer.name.ToLower().Contains("wolf3d_head")) {
                return renderer;
            }
        }

        // Se non lo troviamo, prova a utilizzare il primo renderer che ha blend shapes
        foreach (SkinnedMeshRenderer renderer in renderers) {
            if (renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 0) {
                return renderer;
            }
        }

        // Se ancora non è stato trovato, prova a utilizzare il primo renderer
        if (renderers.Length > 0) {
            return renderers[0];
        }

        return null;
    }

    /// <summary>
    /// Inizializza il dizionario degli indici delle blend shapes.
    /// </summary>
    private void InitializeBlendShapeIndices() {
        blendShapeIndices.Clear();

        if (headMeshRenderer == null || headMeshRenderer.sharedMesh == null)
            return;

        int count = headMeshRenderer.sharedMesh.blendShapeCount;
        Debug.Log($"BlendShapeHelper: L'avatar ha {count} blend shapes:");

        // Ottieni gli indici di tutte le blend shapes disponibili
        for (int i = 0; i < count; i++) {
            string name = headMeshRenderer.sharedMesh.GetBlendShapeName(i);
            blendShapeIndices[name] = i;
            //Debug.Log($"- Blend shape {i}: {name}");
        }
    }

    /// <summary>
    /// Verifica se il helper è stato inizializzato correttamente.
    /// </summary>
    /// <returns>True se il helper è inizializzato, false altrimenti.</returns>
    public bool IsInitialized() {
        return isInitialized && headMeshRenderer != null;
    }

    /// <summary>
    /// Ottiene il renderer della testa.
    /// </summary>
    /// <returns>Il SkinnedMeshRenderer della testa.</returns>
    public SkinnedMeshRenderer GetHeadMeshRenderer() {
        return headMeshRenderer;
    }

    /// <summary>
    /// Verifica se una blend shape specifica esiste.
    /// </summary>
    /// <param name="blendShapeName">Il nome della blend shape da verificare.</param>
    /// <returns>True se la blend shape esiste, false altrimenti.</returns>
    public bool HasBlendShape(string blendShapeName) {
        return blendShapeIndices.ContainsKey(blendShapeName);
    }

    /// <summary>
    /// Ottiene l'indice di una blend shape.
    /// </summary>
    /// <param name="blendShapeName">Il nome della blend shape.</param>
    /// <returns>L'indice della blend shape, o -1 se non trovata.</returns>
    public int GetBlendShapeIndex(string blendShapeName) {
        if (blendShapeIndices.TryGetValue(blendShapeName, out int index)) {
            return index;
        }
        return -1;
    }

    /// <summary>
    /// Imposta il peso di una blend shape.
    /// </summary>
    /// <param name="blendShapeName">Il nome della blend shape.</param>
    /// <param name="weight">Il peso da impostare (0-100).</param>
    /// <returns>True se l'operazione è riuscita, false altrimenti.</returns>
    public bool SetBlendShapeWeight(string blendShapeName, float weight) {
        if (!IsInitialized()) return false;

        int index = GetBlendShapeIndex(blendShapeName);
        //if (weight!=0)
        //    Debug.Log($"[{blendShapeName}] weight is not zero = {weight}");
        if (index >= 0) {
            if (index==50)
                Debug.Log($"[{blendShapeName}] weight is not zero = {weight}");
            headMeshRenderer.SetBlendShapeWeight(index, weight);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Ottiene il peso corrente di una blend shape.
    /// </summary>
    /// <param name="blendShapeName">Il nome della blend shape.</param>
    /// <returns>Il peso corrente della blend shape, o 0 se non trovata.</returns>
    public float GetBlendShapeWeight(string blendShapeName) {
        if (!IsInitialized()) return 0f;

        int index = GetBlendShapeIndex(blendShapeName);
        if (index >= 0) {
            return headMeshRenderer.GetBlendShapeWeight(index);
        }
        return 0f;
    }

    /// <summary>
    /// Ottiene tutti i nomi delle blend shapes disponibili.
    /// </summary>
    /// <returns>Un array contenente i nomi di tutte le blend shapes.</returns>
    public string[] GetAllBlendShapeNames() {
        string[] names = new string[blendShapeIndices.Count];
        blendShapeIndices.Keys.CopyTo(names, 0);
        return names;
    }

    /// <summary>
    /// Reimposta tutte le blend shapes a zero.
    /// </summary>
    public void ResetAllBlendShapes() {
        if (!IsInitialized()) return;

        foreach (var pair in blendShapeIndices) {
            headMeshRenderer.SetBlendShapeWeight(pair.Value, 0f);
        }
    }
}