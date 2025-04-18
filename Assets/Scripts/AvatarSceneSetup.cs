using UnityEngine;
using ReadyPlayerMe.Core;

public class AvatarSceneSetup : MonoBehaviour
{
    [Header("Avatar Settings")]
    [SerializeField] private string avatarUrl = "https://models.readyplayer.me/67617e532104de87ea4aae5e.glb";
    
    [Header("Camera Settings")]
    [SerializeField] private Vector3 cameraPosition = new Vector3(0, 1.58f, 0.48f);
    [SerializeField] private Vector3 cameraRotation = new Vector3(-4.27f, 180, 0);
    
    [Header("Background Settings")]
    [SerializeField] private Texture2D backgroundTexture;
    [SerializeField] private Color backgroundColor = new Color(0.2f, 0.3f, 0.5f);
    [SerializeField] private bool useTexture = true;
    
    private GameObject avatarObject;
    private Camera mainCamera;
    
    void Start()
    {
        mainCamera = Camera.main;
        SetupCamera();
        SetupBackground();
        SetupLighting();
        LoadAvatar();
    }
    
    private void LoadAvatar()
    {
        var avatarLoader = new AvatarObjectLoader();
        
        avatarLoader.OnCompleted += (sender, args) =>
        {
            avatarObject = args.Avatar;
            
            if (avatarObject != null)
            {
                avatarObject.transform.SetParent(transform);
                avatarObject.transform.localPosition = Vector3.zero;
                avatarObject.tag = "Avatar";
                
                // Posizionamento finale della camera
                FinalizeCamera();
            }
        };
        
        avatarLoader.OnFailed += (sender, args) =>
        {
            Debug.LogError($"Errore nel caricamento dell'avatar: {args.Message}");
        };
        
        avatarLoader.LoadAvatar(avatarUrl);
    }
    
    private void SetupCamera()
    {
        if (mainCamera == null) return;
        
        mainCamera.transform.position = cameraPosition;
        mainCamera.transform.rotation = Quaternion.Euler(cameraRotation);
        
        // Ottimizza le impostazioni della camera
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = new Color(0.2f, 0.2f, 0.2f);
    }
    
    private void FinalizeCamera()
    {
        // Possiamo fare aggiustamenti finali dopo che l'avatar è caricato
        mainCamera.transform.LookAt(new Vector3(0, 1.6f, 0)); // Punta verso l'altezza della testa
    }
    
    private void SetupBackground()
    {
        // Crea il piano di sfondo
        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "Background";
        
        // Posiziona il piano di sfondo
        background.transform.SetParent(transform);
        background.transform.localPosition = new Vector3(0, 1.5f, -0.5f);
        background.transform.localScale = new Vector3(5, 3, 1);
        background.transform.rotation = Quaternion.Euler(0, 180, 0);
        
        // Configura il materiale
        Material backgroundMaterial = new Material(Shader.Find("Unlit/Texture"));
        
        if (useTexture && backgroundTexture != null)
        {
            backgroundMaterial.mainTexture = backgroundTexture;
        }
        else
        {
            backgroundMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            backgroundMaterial.color = backgroundColor;
        }
        
        background.GetComponent<Renderer>().material = backgroundMaterial;
    }
    
    private void SetupLighting()
    {
        // Configura le luci
        
        // Key Light (principale)
        GameObject keyLight = new GameObject("KeyLight");
        Light keyLightComp = keyLight.AddComponent<Light>();
        keyLightComp.type = LightType.Directional;
        keyLightComp.intensity = 1.2f;
        keyLightComp.color = new Color(1.0f, 0.95f, 0.9f);
        keyLight.transform.rotation = Quaternion.Euler(45, -30, 0);
        keyLight.transform.SetParent(transform);
        
        // Fill Light (riempimento)
        GameObject fillLight = new GameObject("FillLight");
        Light fillLightComp = fillLight.AddComponent<Light>();
        fillLightComp.type = LightType.Directional;
        fillLightComp.intensity = 0.7f;
        fillLightComp.color = new Color(0.9f, 0.95f, 1.0f);
        fillLight.transform.rotation = Quaternion.Euler(30, 60, 0);
        fillLight.transform.SetParent(transform);
        
        // Rim Light (controluce)
        GameObject rimLight = new GameObject("RimLight");
        Light rimLightComp = rimLight.AddComponent<Light>();
        rimLightComp.type = LightType.Directional;
        rimLightComp.intensity = 0.5f;
        rimLightComp.color = new Color(1.0f, 1.0f, 1.0f);
        rimLight.transform.rotation = Quaternion.Euler(0, 180, 0);
        rimLight.transform.SetParent(transform);
        
        // Imposta l'ambient light
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.2f, 0.2f, 0.2f);
    }
}