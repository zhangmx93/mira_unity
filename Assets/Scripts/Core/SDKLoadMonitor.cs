using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// SDK 加载监控器
/// 监控所有 SDK 的加载状态，防止在 SDK 未就绪时调用导致线程崩溃
/// 提供统一的 SDK 就绪状态检查和事件通知
/// </summary>
public class SDKLoadMonitor : MonoBehaviour
{
    [Header("SDK 引用")]
    [Tooltip("LLM 管理器")]
    public RKLLMManager llmManager;

    [Tooltip("TTS 管理器")]
    public RKTTSManager ttsManager;

    [Tooltip("Face 管理器")]
    public RKFaceManager faceManager;

    [Tooltip("SDK 加载器")]
    public SDKLoader sdkLoader;

    [Header("监控配置")]
    [Tooltip("检查 SDK 状态的间隔时间（秒）")]
    public float checkInterval = 0.5f;

    [Tooltip("最大等待时间（秒），超时后强制标记为失败")]
    public float maxWaitTime = 30f;

    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    [Header("UI 配置")]
    [Tooltip("加载面板（可选，SDK 加载完成后会自动关闭）")]
    public GameObject loadingPanel;

    [Tooltip("是否在 SDK 就绪后自动关闭加载面板")]
    public bool autoCloseLoadingPanel = true;

    [Header("事件")]
    [Tooltip("所有 SDK 就绪时触发")]
    public UnityEvent OnAllSDKsReady;

    [Tooltip("SDK 加载失败时触发")]
    public UnityEvent<string> OnSDKLoadFailed;

    // SDK 状态
    private bool isLLMReady = false;
    private bool isTTSReady = false;
    private bool isFaceReady = false;
    private bool allSDKsReady = false;
    private bool isMonitoring = false;
    private float monitoringStartTime = 0f;

    // 操作锁定
    private bool operationsLocked = true;

    // 单例
    private static SDKLoadMonitor instance;
    public static SDKLoadMonitor Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("SDKLoadMonitor: 检测到重复实例，销毁当前对象");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableDebugLog)
            Debug.Log("SDKLoadMonitor: 初始化完成");
    }

    void Start()
    {
        // 查找所有 SDK 管理器
        FindAllSDKManagers();

        // 开始监控
        StartMonitoring();
    }

    /// <summary>
    /// 查找场景中的所有 SDK 管理器
    /// </summary>
    void FindAllSDKManagers()
    {
        if (llmManager == null)
        {
            llmManager = FindObjectOfType<RKLLMManager>();
        }

        if (ttsManager == null)
        {
            ttsManager = FindObjectOfType<RKTTSManager>();
        }

        if (faceManager == null)
        {
            faceManager = FindObjectOfType<RKFaceManager>();
        }

        if (sdkLoader == null)
        {
            sdkLoader = FindObjectOfType<SDKLoader>();
        }

        if (enableDebugLog)
        {
            Debug.Log($"SDKLoadMonitor: 找到的 SDK 管理器:");
            Debug.Log($"  - LLM: {(llmManager != null ? "✅" : "❌")}");
            Debug.Log($"  - TTS: {(ttsManager != null ? "✅" : "❌")}");
            Debug.Log($"  - Face: {(faceManager != null ? "✅" : "❌")}");
            Debug.Log($"  - Loader: {(sdkLoader != null ? "✅" : "❌")}");
        }
    }

    /// <summary>
    /// 开始监控 SDK 加载状态
    /// </summary>
    public void StartMonitoring()
    {
        if (isMonitoring)
        {
            Debug.LogWarning("SDKLoadMonitor: 已经在监控中");
            return;
        }

        if (enableDebugLog)
            Debug.Log("SDKLoadMonitor: 开始监控 SDK 加载状态...");

        isMonitoring = true;
        monitoringStartTime = Time.time;
        operationsLocked = true;

        StartCoroutine(MonitorSDKLoadingRoutine());
    }

    /// <summary>
    /// 监控 SDK 加载状态的协程
    /// </summary>
    IEnumerator MonitorSDKLoadingRoutine()
    {
        while (isMonitoring)
        {
            // 检查是否超时
            float elapsedTime = Time.time - monitoringStartTime;
            if (elapsedTime > maxWaitTime)
            {
                string failedSDKs = GetFailedSDKsList();
                Debug.LogError($"SDKLoadMonitor: SDK 加载超时！未就绪的 SDK: {failedSDKs}");
                OnSDKLoadFailed?.Invoke($"SDK 加载超时: {failedSDKs}");
                isMonitoring = false;
                yield break;
            }

            // 更新各 SDK 状态
            UpdateSDKStatus();

            // 检查是否所有 SDK 都已就绪
            if (CheckAllSDKsReady())
            {
                allSDKsReady = true;
                operationsLocked = false;
                isMonitoring = false;

                if (enableDebugLog)
                {
                    Debug.Log("========================================");
                    Debug.Log("SDKLoadMonitor: ✅ 所有 SDK 已就绪！");
                    Debug.Log($"SDKLoadMonitor: 总耗时: {elapsedTime:F2} 秒");
                    Debug.Log("========================================");
                }

                // 自动关闭加载面板
                if (autoCloseLoadingPanel && loadingPanel != null)
                {
                    if (enableDebugLog)
                        Debug.Log("SDKLoadMonitor: 自动关闭加载面板");

                    loadingPanel.SetActive(false);
                }

                OnAllSDKsReady?.Invoke();
                yield break;
            }

            // 等待下一次检查
            yield return new WaitForSeconds(checkInterval);
        }
    }

    /// <summary>
    /// 更新各 SDK 的状态
    /// </summary>
    void UpdateSDKStatus()
    {
        bool prevLLMReady = isLLMReady;
        bool prevTTSReady = isTTSReady;
        bool prevFaceReady = isFaceReady;

        // 检查 LLM 状态
        isLLMReady = llmManager != null && llmManager.IsInitialized();

        // 检查 TTS 状态
        isTTSReady = ttsManager != null && ttsManager.IsInitialized();

        // 检查 Face 状态（如果存在）
        isFaceReady = faceManager == null || faceManager.IsInitialized;

        // 输出状态变化
        if (enableDebugLog)
        {
            if (isLLMReady && !prevLLMReady)
                Debug.Log("SDKLoadMonitor: ✅ RKLLM 已就绪");

            if (isTTSReady && !prevTTSReady)
                Debug.Log("SDKLoadMonitor: ✅ RKTTS 已就绪");

            if (isFaceReady && !prevFaceReady)
                Debug.Log("SDKLoadMonitor: ✅ RKFace 已就绪");
        }
    }

    /// <summary>
    /// 检查所有 SDK 是否都已就绪
    /// </summary>
    bool CheckAllSDKsReady()
    {
        return isLLMReady && isTTSReady && isFaceReady;
    }

    /// <summary>
    /// 获取未就绪的 SDK 列表
    /// </summary>
    string GetFailedSDKsList()
    {
        List<string> failedSDKs = new List<string>();

        if (!isLLMReady && llmManager != null)
            failedSDKs.Add("RKLLM");

        if (!isTTSReady && ttsManager != null)
            failedSDKs.Add("RKTTS");

        if (!isFaceReady && faceManager != null)
            failedSDKs.Add("RKFace");

        return string.Join(", ", failedSDKs);
    }

    /// <summary>
    /// 获取当前加载状态文本
    /// </summary>
    public string GetStatusText()
    {
        if (allSDKsReady)
            return "✅ 所有 SDK 已就绪";

        if (!isMonitoring)
            return "等待加载...";

        float elapsedTime = Time.time - monitoringStartTime;
        string status = $"正在加载 SDK... ({elapsedTime:F1}s)\n";

        if (llmManager != null)
            status += isLLMReady ? "✅ LLM 就绪\n" : "⏳ LLM 加载中...\n";

        if (ttsManager != null)
            status += isTTSReady ? "✅ TTS 就绪\n" : "⏳ TTS 加载中...\n";

        if (faceManager != null)
            status += isFaceReady ? "✅ Face 就绪" : "⏳ Face 加载中...";

        return status;
    }

    #region 公开 API

    /// <summary>
    /// 检查所有 SDK 是否都已就绪
    /// </summary>
    public bool AreAllSDKsReady()
    {
        return allSDKsReady;
    }

    /// <summary>
    /// 检查是否可以执行操作（SDK 已就绪且未锁定）
    /// </summary>
    public bool CanPerformOperations()
    {
        return !operationsLocked && allSDKsReady;
    }

    /// <summary>
    /// 安全地执行需要 LLM 的操作
    /// </summary>
    /// <param name="action">要执行的操作</param>
    /// <param name="onError">错误回调</param>
    public void SafeLLMOperation(System.Action action, System.Action<string> onError = null)
    {
        if (!isLLMReady)
        {
            string error = "RKLLM 未就绪，无法执行操作";
            Debug.LogWarning($"SDKLoadMonitor: {error}");
            onError?.Invoke(error);
            return;
        }

        if (operationsLocked)
        {
            string error = "SDK 正在加载中，请稍候";
            Debug.LogWarning($"SDKLoadMonitor: {error}");
            onError?.Invoke(error);
            return;
        }

        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SDKLoadMonitor: LLM 操作执行失败 - {e.Message}");
            onError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// 安全地执行需要 TTS 的操作
    /// </summary>
    public void SafeTTSOperation(System.Action action, System.Action<string> onError = null)
    {
        if (!isTTSReady)
        {
            string error = "RKTTS 未就绪，无法执行操作";
            Debug.LogWarning($"SDKLoadMonitor: {error}");
            onError?.Invoke(error);
            return;
        }

        if (operationsLocked)
        {
            string error = "SDK 正在加载中，请稍候";
            Debug.LogWarning($"SDKLoadMonitor: {error}");
            onError?.Invoke(error);
            return;
        }

        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SDKLoadMonitor: TTS 操作执行失败 - {e.Message}");
            onError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// 安全地执行需要 Face 的操作
    /// </summary>
    public void SafeFaceOperation(System.Action action, System.Action<string> onError = null)
    {
        if (!isFaceReady)
        {
            string error = "RKFace 未就绪，无法执行操作";
            Debug.LogWarning($"SDKLoadMonitor: {error}");
            onError?.Invoke(error);
            return;
        }

        if (operationsLocked)
        {
            string error = "SDK 正在加载中，请稍候";
            Debug.LogWarning($"SDKLoadMonitor: {error}");
            onError?.Invoke(error);
            return;
        }

        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SDKLoadMonitor: Face 操作执行失败 - {e.Message}");
            onError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// 获取各 SDK 的就绪状态
    /// </summary>
    public (bool llm, bool tts, bool face) GetSDKReadyStatus()
    {
        return (isLLMReady, isTTSReady, isFaceReady);
    }

    /// <summary>
    /// 手动标记所有 SDK 为未就绪（重新加载时使用）
    /// </summary>
    public void ResetAllSDKs()
    {
        if (enableDebugLog)
            Debug.Log("SDKLoadMonitor: 重置所有 SDK 状态");

        isLLMReady = false;
        isTTSReady = false;
        isFaceReady = false;
        allSDKsReady = false;
        operationsLocked = true;

        // 重新开始监控
        if (!isMonitoring)
        {
            StartMonitoring();
        }
    }

    #endregion

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
