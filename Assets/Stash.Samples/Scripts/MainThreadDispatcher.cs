using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Helper class to run actions on the main thread from background threads
/// </summary>
public class MainThreadDispatcher : MonoBehaviour
{
    private static MainThreadDispatcher _instance;
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static bool _isInitialized = false;

    public static MainThreadDispatcher Instance
    {
        get
        {
            if (_instance == null)
            {
                // Create a GameObject if needed
                GameObject go = new GameObject("MainThreadDispatcher");
                _instance = go.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(go);
                _isInitialized = true;
                Debug.Log("MainThreadDispatcher initialized");
            }
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            _isInitialized = true;
            Debug.Log("MainThreadDispatcher initialized in Awake");
        }
        else if (_instance != this)
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        // Execute all queued actions
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                Action action = _executionQueue.Dequeue();
                try
                {
                    action();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }

    /// <summary>
    /// Enqueues an action to be executed on the main thread
    /// </summary>
    /// <param name="action">The action to execute on the main thread</param>
    public static void RunOnMainThread(Action action)
    {
        if (action == null)
        {
            Debug.LogWarning("MainThreadDispatcher: Null action provided");
            return;
        }

        // Initialize the dispatcher if needed
        if (!_isInitialized)
        {
            Instance.ToString(); // This will trigger initialization
        }

        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Ensures that an action is executed on the main thread,
    /// either immediately if already on main thread, or queued if not
    /// </summary>
    /// <param name="action">The action to execute</param>
    public static void EnsureMainThread(Action action)
    {
        if (action == null) return;

        // Check if we're already on the main thread
        if (IsMainThread())
        {
            action();
        }
        else
        {
            RunOnMainThread(action);
        }
    }

    /// <summary>
    /// Checks if the current thread is the main Unity thread
    /// </summary>
    public static bool IsMainThread()
    {
        return System.Threading.Thread.CurrentThread.ManagedThreadId == 1;
    }
} 