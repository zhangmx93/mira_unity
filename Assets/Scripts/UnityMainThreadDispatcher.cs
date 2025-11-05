using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Unity 主线程调度器
/// 用于将其他线程的操作调度到 Unity 主线程执行
/// 增强版本：添加了线程安全检查和错误处理
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour
{
    private static UnityMainThreadDispatcher instance;
    private static readonly Queue<Action> executionQueue = new Queue<Action>();
    private static int mainThreadId = -1;

    [Header("配置")]
    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = false;

    [Tooltip("队列最大容量（0 = 无限制）")]
    public int maxQueueSize = 1000;

    // 统计信息
    private int totalEnqueued = 0;
    private int totalExecuted = 0;
    private int totalErrors = 0;

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

            // 记录主线程 ID
            mainThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;

            if (enableDebugLog)
                Debug.Log($"UnityMainThreadDispatcher: 初始化完成 (主线程 ID: {mainThreadId})");
        }
        else if (instance != this)
        {
            Debug.LogWarning("UnityMainThreadDispatcher: 检测到重复实例，销毁当前对象");
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // 主线程安全检查
        if (System.Threading.Thread.CurrentThread.ManagedThreadId != mainThreadId)
        {
            Debug.LogError("UnityMainThreadDispatcher: Update() 不在主线程执行！");
            return;
        }

        lock (executionQueue)
        {
            int executeCount = 0;
            while (executionQueue.Count > 0)
            {
                Action action = executionQueue.Dequeue();

                try
                {
                    action?.Invoke();
                    totalExecuted++;
                    executeCount++;
                }
                catch (Exception e)
                {
                    totalErrors++;
                    Debug.LogError($"UnityMainThreadDispatcher: 执行操作时发生错误 - {e.Message}\n{e.StackTrace}");
                }
            }

            if (enableDebugLog && executeCount > 0)
            {
                Debug.Log($"UnityMainThreadDispatcher: 本帧执行了 {executeCount} 个操作");
            }
        }
    }

    /// <summary>
    /// 将操作加入队列，在主线程执行
    /// </summary>
    public void Enqueue(Action action)
    {
        if (action == null)
        {
            Debug.LogWarning("UnityMainThreadDispatcher: 尝试添加空操作到队列");
            return;
        }

        lock (executionQueue)
        {
            // 检查队列容量
            if (maxQueueSize > 0 && executionQueue.Count >= maxQueueSize)
            {
                Debug.LogError($"UnityMainThreadDispatcher: 队列已满 (大小: {executionQueue.Count})，无法添加新操作");
                return;
            }

            executionQueue.Enqueue(action);
            totalEnqueued++;

            if (enableDebugLog)
            {
                int threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
                bool isMainThread = threadId == mainThreadId;
                Debug.Log($"UnityMainThreadDispatcher: 添加操作到队列 (队列大小: {executionQueue.Count}, 来自{(isMainThread ? "主" : "子")}线程 ID: {threadId})");
            }
        }
    }

    /// <summary>
    /// 在主线程执行协程
    /// </summary>
    public void EnqueueCoroutine(IEnumerator coroutine)
    {
        if (coroutine == null)
        {
            Debug.LogWarning("UnityMainThreadDispatcher: 尝试添加空协程到队列");
            return;
        }

        Enqueue(() =>
        {
            try
            {
                StartCoroutine(coroutine);
            }
            catch (Exception e)
            {
                Debug.LogError($"UnityMainThreadDispatcher: 启动协程时发生错误 - {e.Message}");
            }
        });
    }

    /// <summary>
    /// 检查当前是否在主线程
    /// </summary>
    public static bool IsMainThread()
    {
        return System.Threading.Thread.CurrentThread.ManagedThreadId == mainThreadId;
    }

    /// <summary>
    /// 获取队列当前大小
    /// </summary>
    public int GetQueueSize()
    {
        lock (executionQueue)
        {
            return executionQueue.Count;
        }
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    public (int enqueued, int executed, int errors) GetStatistics()
    {
        return (totalEnqueued, totalExecuted, totalErrors);
    }

    /// <summary>
    /// 清空队列
    /// </summary>
    public void ClearQueue()
    {
        lock (executionQueue)
        {
            int count = executionQueue.Count;
            executionQueue.Clear();

            if (enableDebugLog)
                Debug.Log($"UnityMainThreadDispatcher: 清空队列，移除了 {count} 个操作");
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            if (enableDebugLog)
            {
                Debug.Log($"UnityMainThreadDispatcher: 销毁 (统计信息 - 入队: {totalEnqueued}, 已执行: {totalExecuted}, 错误: {totalErrors})");
            }

            instance = null;
        }
    }

    void OnApplicationQuit()
    {
        // 应用退出时清空队列
        lock (executionQueue)
        {
            executionQueue.Clear();
        }
    }
}
