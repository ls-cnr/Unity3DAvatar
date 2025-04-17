using UnityEngine;
using ReadyPlayerMe.Core;

public class AvatarLoaderWithCamera : MonoBehaviour
{
    [SerializeField] private string avatarUrl = "https://models.readyplayer.me/67617e532104de87ea4aae5e.glb";

    // Controlli per la camera che possono essere modificati a runtime
    [Header("Camera Controls")]
    [SerializeField] private Vector3 cameraOffset = new Vector3(0, -0.09f, 1.54f);
    [SerializeField] private float cameraSpeed = 0.1f; // Velocità di movimento della camera con i controlli

    private GameObject avatarObject;
    private Camera mainCamera;
    private Vector3 headPosition;
    private bool avatarLoaded = false;

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("Camera principale non trovata nella scena!");
            return;
        }

        LoadAvatar();
    }

    void Update()
    {
        if (!avatarLoaded) return;

        // Controlli a runtime per regolare la posizione della camera

        // Modifica X (sinistra/destra)
        if (Input.GetKey(KeyCode.A))
            cameraOffset.x -= cameraSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.D))
            cameraOffset.x += cameraSpeed * Time.deltaTime;

        // Modifica Y (su/giù)
        if (Input.GetKey(KeyCode.W))
            cameraOffset.y += cameraSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.S))
            cameraOffset.y -= cameraSpeed * Time.deltaTime;

        // Modifica Z (avanti/indietro)
        if (Input.GetKey(KeyCode.Q))
            cameraOffset.z -= cameraSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.E))
            cameraOffset.z += cameraSpeed * Time.deltaTime;

        // Aggiorna la posizione della camera in base ai nuovi offset
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D) ||
            Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.S) ||
            Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E))
        {
            PositionCameraForAvatar();
            Debug.Log($"Camera Offset: {cameraOffset}");
        }
    }

    private void LoadAvatar()
    {
        var avatarLoader = new AvatarObjectLoader();

        // Imposta un callback per quando l'avatar è completato
        avatarLoader.OnCompleted += (sender, args) =>
        {
            avatarObject = args.Avatar;
            Debug.Log("Avatar caricato con successo!");

            if (avatarObject != null)
            {
                // Configura l'avatar
                avatarObject.transform.SetParent(transform);
                avatarObject.transform.localPosition = Vector3.zero;

                // Trova la posizione della testa
                headPosition = FindHeadPosition();

                // Posiziona la telecamera
                PositionCameraForAvatar();

                avatarLoaded = true;

                // Mostra istruzioni per i controlli
                Debug.Log("Usa WASDQE per regolare la posizione della camera:\n" +
                         "W/S: su/giù\n" +
                         "A/D: sinistra/destra\n" +
                         "Q/E: avanti/indietro");
            }
        };

        // Imposta un callback per gli errori
        avatarLoader.OnFailed += (sender, args) =>
        {
            Debug.LogError($"Errore nel caricamento dell'avatar: {args.Message}");
        };

        // Avvia il caricamento
        avatarLoader.LoadAvatar(avatarUrl);
    }

    private void PositionCameraForAvatar()
    {
        if (mainCamera == null || avatarObject == null) return;

        // Posiziona la camera rispetto alla testa
        mainCamera.transform.position = headPosition + cameraOffset;

        // Orienta la camera verso la testa
        mainCamera.transform.LookAt(headPosition);
    }

    private Vector3 FindHeadPosition()
    {
        // Cerca il Transform della testa, se esiste
        Transform headTransform = null;
        Transform[] allTransforms = avatarObject.GetComponentsInChildren<Transform>();

        foreach (var t in allTransforms)
        {
            if (t.name.Contains("Head") || t.name.Contains("head") || t.name.Contains("Face"))
            {
                headTransform = t;
                Debug.Log($"Trovato transform della testa: {t.name}");
                break;
            }
        }

        // Se abbiamo trovato la testa, usa la sua posizione
        if (headTransform != null)
        {
            return headTransform.position;
        }

        // Altrimenti, usa una stima basata sull'altezza dell'avatar
        Renderer[] renderers = avatarObject.GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            // Calcola i bounds combinati di tutti i renderer
            Bounds bounds = new Bounds(renderers[0].bounds.center, renderers[0].bounds.size);
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            // Stima la posizione della testa (parte superiore del bounds meno un piccolo offset)
            float headHeight = bounds.max.y - 0.1f;
            return new Vector3(bounds.center.x, headHeight, bounds.center.z);
        }

        // Se tutto fallisce, usa la posizione dell'avatar + un offset standard per l'altezza della testa
        return avatarObject.transform.position + new Vector3(0, 1.7f, 0);
    }

    // Metodo per ottenere i valori correnti della posizione della camera (utile per salvare la configurazione)
    public Vector3 GetCurrentCameraOffset()
    {
        return cameraOffset;
    }
}