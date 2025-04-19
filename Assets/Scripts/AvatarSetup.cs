using UnityEngine;
using ReadyPlayerMe.Core;
using System;

public class AvatarSetup : MonoBehaviour {
    [SerializeField] private string avatarUrl = "https://models.readyplayer.me/67617e532104de87ea4aae5e.glb";
    [SerializeField] private RuntimeAnimatorController animatorController;

    private GameObject avatarObject;
    private bool isLoading = false;

    // Evento per notificare il completamento del caricamento
    public event Action<GameObject> OnAvatarLoaded;

    // Metodo pubblico per avviare il caricamento dell'avatar
    public void ActivateAvatarLoading() {
        if (isLoading) return;

        isLoading = true;
        Debug.Log("Avvio del caricamento dell'avatar...");
        LoadAvatar(avatarUrl);
    }

    private void LoadAvatar(string url) {
        var avatarLoader = new AvatarObjectLoader();

        // Callback per completamento
        avatarLoader.OnCompleted += (sender, args) => {
            avatarObject = args.Avatar;
            Debug.Log("Avatar caricato con successo!");

            if (avatarObject != null) {
                avatarObject.transform.SetParent(transform);
                avatarObject.transform.localPosition = Vector3.zero;
                avatarObject.tag = "Avatar";

                // Configura l'animazione
                SetupAnimation();

                // Notifica chi è in ascolto che l'avatar è stato caricato
                isLoading = false;
                OnAvatarLoaded?.Invoke(avatarObject);
            }
        };

        // Callback per errori
        avatarLoader.OnFailed += (sender, args) => {
            Debug.LogError($"Errore nel caricamento dell'avatar: {args.Message}");
            isLoading = false;
        };

        // Avvia il caricamento
        avatarLoader.LoadAvatar(url);
    }

    private void SetupAnimation() {
        Animator animator = avatarObject.GetComponent<Animator>();
        if (animator == null) {
            animator = avatarObject.AddComponent<Animator>();
        }

        if (animatorController != null) {
            animator.runtimeAnimatorController = animatorController;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = false;

            // Avvia l'animazione Idle
            animator.Play("Idle", 0, 0f);
            Debug.Log("Animator Controller applicato con successo!");
        } else {
            Debug.LogWarning("Nessun Animator Controller assegnato nell'Inspector!");
        }
    }

    // Metodo pubblico per ottenere l'avatar
    public GameObject GetAvatar() {
        return avatarObject;
    }
}