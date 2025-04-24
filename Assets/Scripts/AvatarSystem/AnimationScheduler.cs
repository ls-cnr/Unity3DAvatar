using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Gestisce una coda di priorità per le animazioni facciali.
public class AnimationScheduler
{
    // Coda di priorità per le animazioni facciali
    private List<FaceAnimationItem> animationQueue = new List<FaceAnimationItem>();
    
    // Lock per le operazioni thread-safe
    private readonly object queueLock = new object();
    
    /// Aggiunge un'animazione alla coda con la priorità specificata.
    /// <param name="animation">L'animazione da aggiungere</param>
    public void EnqueueAnimation(FaceAnimationItem animation)
    {
        if (animation == null)
            return;
            
        lock (queueLock)
        {
            // Inserisce l'animazione nella coda mantenendo l'ordine per priorità
            // (priorità più alta all'inizio della coda)
            int insertIndex = 0;
            
            while (insertIndex < animationQueue.Count && 
                   animationQueue[insertIndex].GetPriority() > animation.GetPriority())
            {
                insertIndex++;
            }
            
            animationQueue.Insert(insertIndex, animation);
            
        }

        Debug.Log("Animazione Inserita!");
    }
    
    /// Preleva la prossima animazione dalla coda.
    /// <returns>La prossima animazione da eseguire o null se la coda è vuota</returns>
    public FaceAnimationItem DequeueNextAnimation()
    {
        lock (queueLock)
        {
            if (animationQueue.Count == 0)
                return null;
                
            // Prende la prima animazione (quella con priorità più alta)
            FaceAnimationItem nextAnimation = animationQueue[0];
            animationQueue.RemoveAt(0);
            
            return nextAnimation;
        }
    }
    
    /// Ispeziona la prossima animazione senza rimuoverla dalla coda.
    /// <returns>La prossima animazione o null se la coda è vuota</returns>
    public FaceAnimationItem PeekNextAnimation()
    {
        lock (queueLock)
        {
            return animationQueue.Count > 0 ? animationQueue[0] : null;
        }
    }
    
    /// Cancella tutte le animazioni dalla coda.
    public void ClearQueue()
    {
        lock (queueLock)
        {
            animationQueue.Clear();
        }
    }
    
    /// Ottiene il numero di animazioni attualmente in coda.
    /// <returns>Il conteggio delle animazioni in coda</returns>
    public int GetQueueCount()
    {
        lock (queueLock)
        {
            return animationQueue.Count;
        }
    }
    
    /// Rimuove tutte le animazioni del tipo specificato dalla coda.
    /// <typeparam name="T">Il tipo di animazione da rimuovere</typeparam>
    /// <returns>Il numero di animazioni rimosse</returns>
    public int RemoveAnimationsOfType<T>() where T : FaceAnimationItem
    {
        lock (queueLock)
        {
            int initialCount = animationQueue.Count;
            animationQueue.RemoveAll(anim => anim is T);
            int removedCount = initialCount - animationQueue.Count;
                        
            return removedCount;
        }
    }
    
    /// Rimuove un'animazione specifica dalla coda.
    /// <param name="animation">L'animazione da rimuovere</param>
    /// <returns>True se l'animazione è stata rimossa, altrimenti False</returns>
    public bool RemoveAnimation(FaceAnimationItem animation)
    {
        if (animation == null)
            return false;
            
        lock (queueLock)
        {
            bool removed = animationQueue.Remove(animation);
            
            return removed;
        }
    }
    
    /// Ottiene una snapshot delle animazioni attualmente in coda (utile per debug).
    /// <returns>Un array di animazioni in coda</returns>
    public FaceAnimationItem[] GetQueueSnapshot()
    {
        lock (queueLock)
        {
            return animationQueue.ToArray();
        }
    }
}