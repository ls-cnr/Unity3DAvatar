using UnityEngine;

public class SceneSetup : MonoBehaviour {
    [Header("References")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private GameObject avatarContainer;
    [SerializeField] private GameObject backgroundQuad;
    [SerializeField] private Light keyLight;  // Ora è riferimento diretto a Light
    [SerializeField] private Light fillLight; // Ora è riferimento diretto a Light
    [SerializeField] private Light rimLight;  // Ora è riferimento diretto a Light

    [Header("Camera Settings")]
    [SerializeField] private Vector3 cameraPosition = new Vector3(0, 1.58f, 0.48f);
    [SerializeField] private Vector3 cameraRotation = new Vector3(-4.27f, 180, 0);

    [Header("Background Settings")]
    [SerializeField] private Material backgroundMaterial;
    [SerializeField] private Texture2D backgroundTexture;
    [SerializeField] private Color backgroundColor = Color.blue;
    [SerializeField] private bool useTexture = true;

    [Header("Light Settings")]
    [SerializeField] private Color keyLightColor = new Color(1.0f, 0.95f, 0.9f);
    [SerializeField] private float keyLightIntensity = 1.2f;
    [SerializeField] private Color fillLightColor = new Color(0.9f, 0.95f, 1.0f);
    [SerializeField] private float fillLightIntensity = 0.7f;
    [SerializeField] private Color rimLightColor = Color.white;
    [SerializeField] private float rimLightIntensity = 0.5f;

    private AvatarManager avatarSetup;

    void Start() {
        // Verifica che tutti i riferimenti siano stati assegnati
        if (!ValidateReferences()) return;

        // Ottieni il componente AvatarSetup
        avatarSetup = avatarContainer.GetComponent<AvatarManager>();
        if (avatarSetup == null) {
            Debug.LogError("Componente AvatarSetup non trovato nel GameObject avatarContainer!");
            return;
        }

        // Inizializza gli elementi della scena
        InitializeScene();

        // Iscrizione all'evento di caricamento dell'avatar completato
        avatarSetup.OnAvatarLoaded += OnAvatarLoaded;

        // Avvia il caricamento dell'avatar
        avatarSetup.ActivateAvatarLoading();
    }

    private bool ValidateReferences() {
        if (mainCamera == null) {
            mainCamera = Camera.main;
            if (mainCamera == null) {
                Debug.LogError("Nessuna camera principale trovata nella scena!");
                return false;
            }
        }

        if (avatarContainer == null) {
            Debug.LogError("Riferimento al GameObject dell'avatar mancante!");
            return false;
        }

        if (backgroundQuad == null) {
            Debug.LogError("Riferimento al Quad di background mancante!");
            return false;
        }

        if (keyLight == null) {
            Debug.LogError("Riferimento a Key Light mancante!");
            return false;
        }

        if (fillLight == null) {
            Debug.LogError("Riferimento a Fill Light mancante!");
            return false;
        }

        if (rimLight == null) {
            Debug.LogError("Riferimento a Rim Light mancante!");
            return false;
        }

        return true;
    }

    private void InitializeScene() {
        // Setup camera
        SetupCamera();

        // Setup background
        SetupBackground();
    }

    private void SetupCamera() {
        mainCamera.transform.position = cameraPosition;
        mainCamera.transform.rotation = Quaternion.Euler(cameraRotation);

        // Ottimizza le impostazioni della camera
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
    }

    private void SetupBackground() {
        // Configura il quad di background
        backgroundQuad.transform.SetParent(transform);
        backgroundQuad.transform.localPosition = new Vector3(0, 1.5f, -1.0f);
        backgroundQuad.transform.localScale = new Vector3(5, 3, 1);
        backgroundQuad.transform.rotation = Quaternion.Euler(0, 180, 0);

        // Crea o assegna il materiale per lo sfondo
        if (backgroundMaterial == null) {
            backgroundMaterial = new Material(Shader.Find("Unlit/Texture"));
        }

        if (useTexture && backgroundTexture != null) {
            backgroundMaterial.mainTexture = backgroundTexture;
        } else {
            backgroundMaterial.color = backgroundColor;
        }

        backgroundQuad.GetComponent<Renderer>().material = backgroundMaterial;
    }

    private void OnAvatarLoaded(GameObject avatar) {
        Debug.Log("SceneSetup: Avatar caricato, configurazione della scena in corso...");

        // Configura le luci
        SetupLighting();

        // Posiziona la camera per inquadrare correttamente l'avatar
        PositionCameraForAvatar();
    }

    private void SetupLighting() {
        // Configura key light
        keyLight.type = LightType.Directional;
        keyLight.intensity = keyLightIntensity;
        keyLight.color = keyLightColor;
        keyLight.transform.rotation = Quaternion.Euler(45, -30, 0);

        // Configura fill light
        fillLight.type = LightType.Directional;
        fillLight.intensity = fillLightIntensity;
        fillLight.color = fillLightColor;
        fillLight.transform.rotation = Quaternion.Euler(30, 60, 0);

        // Configura rim light
        rimLight.type = LightType.Directional;
        rimLight.intensity = rimLightIntensity;
        rimLight.color = rimLightColor;
        rimLight.transform.rotation = Quaternion.Euler(0, 180, 0);
    }

    private void PositionCameraForAvatar() {
        // Posiziona la camera per inquadrare l'avatar al meglio
        mainCamera.transform.LookAt(new Vector3(0, 1.6f, 0)); // Punta all'altezza della testa
    }
}