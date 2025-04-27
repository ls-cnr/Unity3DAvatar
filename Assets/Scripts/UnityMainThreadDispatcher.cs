using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

/// <summary>
/// Un dispatcher che consente di eseguire azioni nel thread principale di Unity.
/// Implementato come singleton per facilità d'uso.
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher _instance;
    private static readonly object _lock = new object();
    
    private readonly Queue<Action> _executionQueue = new Queue<Action>();
    
    /// <summary>
    /// Ottiene l'istanza del dispatcher (crea un GameObject se non esiste)
    /// </summary>
    public static UnityMainThreadDispatcher Instance()
    {
        if (_instance == null)
        {
            lock (_lock)
            {
                if (_instance == null)
                {
                    var obj = new GameObject("UnityMainThreadDispatcher");
                    _instance = obj.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(obj);
                }
            }
        }
        return _instance;
    }
    
    void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
    }
    
    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }
    
    /// <summary>
    /// Aggiunge un'azione alla coda di esecuzione
    /// </summary>
    public void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }
    
    /// <summary>
    /// Esegue un'azione nel thread principale e attende il completamento
    /// </summary>
    public Task EnqueueAsync(Action action)
    {
        var tcs = new TaskCompletionSource<bool>();
        
        Enqueue(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        
        return tcs.Task;
    }
    
    void Update()
    {
        // Esegui le azioni in coda nel thread principale
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                Action action = _executionQueue.Dequeue();
                action();
            }
        }
    }
}