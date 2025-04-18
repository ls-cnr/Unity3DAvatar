using UnityEngine;

public class BackgroundSetup : MonoBehaviour
{
    [SerializeField] private Material backgroundMaterial;
    [SerializeField] private Texture2D backgroundTexture;
    [SerializeField] private Color backgroundColor = Color.blue;
    [SerializeField] private bool useTexture = true;
    
    public void SetupBackground()
    {
        // Crea un piano di sfondo
        GameObject background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "Background";
        
        // Posiziona il piano di sfondo dietro l'avatar
        background.transform.SetParent(transform);
        background.transform.localPosition = new Vector3(0, 1.5f, -1.0f);
        background.transform.localScale = new Vector3(5, 3, 1);
        background.transform.rotation = Quaternion.Euler(0, 180, 0);
        
        // Crea o assegna il materiale per lo sfondo
        if (backgroundMaterial == null)
        {
            backgroundMaterial = new Material(Shader.Find("Unlit/Texture"));
        }
        
        if (useTexture && backgroundTexture != null)
        {
            backgroundMaterial.mainTexture = backgroundTexture;
        }
        else
        {
            backgroundMaterial.color = backgroundColor;
        }
        
        background.GetComponent<Renderer>().material = backgroundMaterial;
    }
}