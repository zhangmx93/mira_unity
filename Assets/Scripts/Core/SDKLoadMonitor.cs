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
    [Tooltip("SenseOnnx 管理器")]
    public SenseOnnxManager onnxManager;

    [Tooltip("LLM 管理器")]
    public RKLLMManager llmManager;


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
    private bool isOnnxReady = false;
    private bool isLLMReady = false;
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
            LoggerManager.Warning("检测到重复实例，销毁当前对象", "SDKMonitor");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        if (enableDebugLog)
            LoggerManager.Info("初始化完成", "SDKMonitor");
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
        if (onnxManager == null)
        {
            onnxManager = FindObjectOfType<SenseOnnxManager>();
        }

        if (llmManager == null)
        {
            llmManager = FindObjectOfType<RKLLMManager>();
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
            LoggerManager.Info($"找到的 SDK 管理器:", "SDKMonitor");
            LoggerManager.Info($"  - Onnx: {(onnxManager != null ? "✅" : "❌")}", "SDKMonitor");
            LoggerManager.Info($"  - LLM: {(llmManager != null ? "✅" : "❌")}", "SDKMonitor");
            LoggerManager.Info($"  - Face: {(faceManager != null ? "✅" : "❌")}", "SDKMonitor");
            LoggerManager.Info($"  - Loader: {(sdkLoader != null ? "✅" : "❌")}", "SDKMonitor");
        }
    }

    /// <summary>
    /// 开始监控 SDK 加载状态
    /// </summary>
    public void StartMonitoring()
    {
        if (isMonitoring)
        {
            LoggerManager.Warning("已经在监控中", "SDKMonitor");
            return;
        }

        if (enableDebugLog)
            LoggerManager.Info("开始监控 SDK 加载状态...", "SDKMonitor");

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
                LoggerManager.Error($"SDK 加载超时！未就绪的 SDK: {failedSDKs}", "SDKMonitor");
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
                    LoggerManager.Info("========================================", "SDKMonitor");
                    LoggerManager.Info("✅ 所有 SDK 已就绪！", "SDKMonitor");
                    LoggerManager.Info($"总耗时: {elapsedTime:F2} 秒", "SDKMonitor");
                    LoggerManager.Info("========================================", "SDKMonitor");
                }

                // 自动关闭加载面板
                if (autoCloseLoadingPanel && loadingPanel != null)
                {
                    if (enableDebugLog)
                        LoggerManager.Info("自动关闭加载面板", "SDKMonitor");

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
        bool prevOnnxReady = isOnnxReady;
        bool prevLLMReady = isLLMReady;
        bool prevFaceReady = isFaceReady;

        // 检查 Onnx 状态（如果存在）
        isOnnxReady = onnxManager == null || onnxManager.IsInitialized();

        // 检查 LLM 状态
        isLLMReady = llmManager != null && llmManager.IsInitialized();



        // 检查 Face 状态（如果存在）
        isFaceReady = faceManager == null || faceManager.IsInitialized;

        // 输出状态变化
        if (enableDebugLog)
        {
            if (isOnnxReady && !prevOnnxReady && onnxManager != null)
                LoggerManager.Info("✅ SenseOnnx 已就绪", "SDKMonitor");

            if (isLLMReady && !prevLLMReady)
                LoggerManager.Info("✅ RKLLM 已就绪", "SDKMonitor");



            if (isFaceReady && !prevFaceReady && faceManager != null)
                LoggerManager.Info("✅ RKFace 已就绪", "SDKMonitor");
        }
    }

    /// <summary>
    /// 检查所有 SDK 是否都已就绪
    /// </summary>
    bool CheckAllSDKsReady()
    {
        return isOnnxReady && isLLMReady && isFaceReady;
    }

    /// <summary>
    /// 获取未就绪的 SDK 列表详细信息
    /// </summary>
    string GetFailedSDKsList()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        if (!isOnnxReady)
            sb.Append($"SenseOnnx(Init:{onnxManager?.IsInitialized() ?? false}), ");

        if (!isLLMReady)
            sb.Append($"RKLLM(Init:{llmManager?.IsInitialized() ?? false}), ");



        if (!isFaceReady)
            sb.Append($"RKFace(Init:{(faceManager != null ? faceManager.IsInitialized : false)})");

        if (sb.Length == 0) return "Unknown (All flags true but timeout triggered?)";
        
        return sb.ToString().TrimEnd(',', ' ');
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

        if (onnxManager != null)
            status += isOnnxReady ? "✅ Onnx 就绪\n" : "⏳ Onnx 加载中...\n";

        if (llmManager != null)
            status += isLLMReady ? "✅ LLM 就绪\n" : "⏳ LLM 加载中...\n";



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
    /// 安全地执行需要 Onnx 的操作
    /// </summary>
    public void SafeOnnxOperation(System.Action action, System.Action<string> onError = null)
    {
        if (!isOnnxReady)
        {
            string error = "SenseOnnx 未就绪，无法执行操作";
            LoggerManager.Warning(error, "SDKMonitor");
            onError?.Invoke(error);
            return;
        }

        if (operationsLocked)
        {
            string error = "SDK 正在加载中，请稍候";
            LoggerManager.Warning(error, "SDKMonitor");
            onError?.Invoke(error);
            return;
        }

        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"Onnx 操作执行失败 - {e.Message}", "SDKMonitor");
            onError?.Invoke(e.Message);
        }
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
            LoggerManager.Warning(error, "SDKMonitor");
            onError?.Invoke(error);
            return;
        }

        if (operationsLocked)
        {
            string error = "SDK 正在加载中，请稍候";
            LoggerManager.Warning(error, "SDKMonitor");
            onError?.Invoke(error);
            return;
        }

        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"LLM 操作执行失败 - {e.Message}", "SDKMonitor");
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
            LoggerManager.Warning(error, "SDKMonitor");
            onError?.Invoke(error);
            return;
        }

        if (operationsLocked)
        {
            string error = "SDK 正在加载中，请稍候";
            LoggerManager.Warning(error, "SDKMonitor");
            onError?.Invoke(error);
            return;
        }

        try
        {
            action?.Invoke();
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"Face 操作执行失败 - {e.Message}", "SDKMonitor");
            onError?.Invoke(e.Message);
        }
    }

    /// <summary>
    /// 获取各 SDK 的就绪状态
    /// </summary>
    public (bool onnx, bool llm, bool face) GetSDKReadyStatus()
    {
        return (isOnnxReady, isLLMReady, isFaceReady);
    }

    /// <summary>
    /// 手动标记所有 SDK 为未就绪（重新加载时使用）
    /// </summary>
    public void ResetAllSDKs()
    {
        if (enableDebugLog)
            LoggerManager.Info("重置所有 SDK 状态", "SDKMonitor");

        isOnnxReady = false;
        isLLMReady = false;

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
