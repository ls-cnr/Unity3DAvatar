using UnityEngine;
using ReadyPlayerMe.Core;
using System.Threading.Tasks;

public class SimpleAvatarTest : MonoBehaviour
{
    [SerializeField] private string avatarUrl = "https://models.readyplayer.me/67617e532104de87ea4aae5e.glb";

    private GameObject avatarObject;

    void Start()
    {
        Debug.Log("Inizializzazione del test dell'avatar...");

        try
        {
            // Verifica se possiamo accedere alle classi del core
            var avatarConfig = new AvatarConfig();
            Debug.Log("AvatarConfig disponibile!");

            // Verifica se possiamo creare un AvatarObjectLoader
            var avatarLoader = new AvatarObjectLoader();
            Debug.Log("AvatarObjectLoader disponibile!");

            // Avvia il caricamento senza await
            Debug.Log($"Tentativo di caricamento avatar da URL: {avatarUrl}");
            LoadAvatarNonAsync(avatarUrl);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Errore durante il test: {e.Message}");
            Debug.LogError($"Stack trace: {e.StackTrace}");
        }
    }

    private void LoadAvatarNonAsync(string url)
    {
        var avatarLoader = new AvatarObjectLoader();

        // Imposta un callback per quando l'avatar è completato
        avatarLoader.OnCompleted += (sender, args) =>
        {
            avatarObject = args.Avatar;
            Debug.Log("Avatar caricato con successo!");

            if (avatarObject != null)
            {
                avatarObject.transform.SetParent(transform);
                avatarObject.transform.localPosition = Vector3.zero;
            }
        };

        // Imposta un callback per gli errori
        avatarLoader.OnFailed += (sender, args) =>
        {
            Debug.LogError($"Errore nel caricamento dell'avatar: {args.Message}");
        };

        // Avvia il caricamento
        avatarLoader.LoadAvatar(url);
    }
}