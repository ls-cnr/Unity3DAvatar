using UnityEngine;

public class LightingSetup : MonoBehaviour
{
    [SerializeField] private GameObject avatarObject;
    
    void Start()
    {
        // Se l'avatar non è stato assegnato, cerca di recuperarlo
        if (avatarObject == null)
        {
            avatarObject = GameObject.FindWithTag("Avatar");
        }
    }
    
    public void SetupLighting(GameObject avatar)
    {
        if (avatar != null) avatarObject = avatar;
        if (avatarObject == null) return;
        
        // Crea luce principale (key light)
        GameObject keyLight = new GameObject("KeyLight");
        Light keyLightComp = keyLight.AddComponent<Light>();
        keyLightComp.type = LightType.Directional;
        keyLightComp.intensity = 1.2f;
        keyLightComp.color = new Color(1.0f, 0.95f, 0.9f); // Luce leggermente calda
        keyLight.transform.rotation = Quaternion.Euler(45, -30, 0);
        
        // Crea luce di riempimento (fill light)
        GameObject fillLight = new GameObject("FillLight");
        Light fillLightComp = fillLight.AddComponent<Light>();
        fillLightComp.type = LightType.Directional;
        fillLightComp.intensity = 0.7f;
        fillLightComp.color = new Color(0.9f, 0.95f, 1.0f); // Luce leggermente fredda
        fillLight.transform.rotation = Quaternion.Euler(30, 60, 0);
        
        // Crea controluce (rim light) - opzionale
        GameObject rimLight = new GameObject("RimLight");
        Light rimLightComp = rimLight.AddComponent<Light>();
        rimLightComp.type = LightType.Directional;
        rimLightComp.intensity = 0.5f;
        rimLightComp.color = new Color(1.0f, 1.0f, 1.0f);
        rimLight.transform.rotation = Quaternion.Euler(0, 180, 0);
        
        // Organizza nella gerarchia
        keyLight.transform.SetParent(transform);
        fillLight.transform.SetParent(transform);
        rimLight.transform.SetParent(transform);
    }
}