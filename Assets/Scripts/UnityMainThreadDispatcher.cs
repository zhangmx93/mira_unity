using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity 主线程调度器
/// 用于将其他线程的操作调度到 Unity 主线程执行
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher instance;
    private static readonly Queue<Action> executionQueue = new Queue<Action>();

    public static UnityMainThreadDispatcher Instance()
    {
        if (!Exists())
        {
            throw new Exception("UnityMainThreadDispatcher 未初始化。请在场景中添加此组件。");
        }
        return instance;
    }

    public static bool Exists()
    {
        return instance != null;
    }

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        lock (executionQueue)
        {
            while (executionQueue.Count > 0)
            {
                executionQueue.Dequeue().Invoke();
            }
        }
    }

    /// <summary>
    /// 将操作加入队列，在主线程执行
    /// </summary>
    public void Enqueue(Action action)
    {
        if (action == null)
            return;

        lock (executionQueue)
        {
            executionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// 在主线程执行协程
    /// </summary>
    public void EnqueueCoroutine(IEnumerator coroutine)
    {
        Enqueue(() => StartCoroutine(coroutine));
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
