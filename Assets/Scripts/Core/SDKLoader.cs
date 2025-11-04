using UnityEngine;
using System.Collections;

/// <summary>
/// SDK 延迟加载管理器
/// 在程序启动时按顺序加载 LLM 和 TTS，避免同时加载导致的启动缓慢
/// </summary>
public class SDKLoader : MonoBehaviour
{
    [Header("延迟加载配置")]
    [Tooltip("启动后延迟多久开始加载第一个 SDK（秒）")]
    public float initialDelay = 1.0f;  // 从 0.5 增加到 1.0

    [Tooltip("RKLLM 和 RKTTS 之间的延迟时间（秒）")]
    public float delayBetweenSDKs = 2.0f;  // 从 1.0 增加到 2.0

    [Header("SDK 引用")]
    [Tooltip("LLM 管理器（如果为空会自动查找）")]
    public RKLLMManager llmManager;

    [Tooltip("TTS 管理器（如果为空会自动查找）")]
    public RKTTSManager ttsManager;

    [Header("调试")]
    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    private bool isLoading = false;
    private bool loadingComplete = false;

    void Start()
    {
        if (enableDebugLog)
            Debug.Log("SDKLoader: 准备开始加载 SDK...");

        // 查找 SDK 管理器
        FindSDKManagers();

        // 开始加载流程
        StartCoroutine(LoadSDKsSequentially());
    }

    /// <summary>
    /// 查找场景中的 SDK 管理器
    /// </summary>
    void FindSDKManagers()
    {
        // 查找 LLM Manager
        if (llmManager == null)
        {
            llmManager = FindObjectOfType<RKLLMManager>();
            if (llmManager != null)
            {
                if (enableDebugLog)
                    Debug.Log("SDKLoader: ✅ 找到 RKLLMManager");
            }
            else
            {
                Debug.LogWarning("SDKLoader: ⚠️ 未找到 RKLLMManager");
            }
        }

        // 查找 TTS Manager
        if (ttsManager == null)
        {
            ttsManager = FindObjectOfType<RKTTSManager>();
            if (ttsManager != null)
            {
                if (enableDebugLog)
                    Debug.Log("SDKLoader: ✅ 找到 RKTTSManager");
            }
            else
            {
                Debug.LogWarning("SDKLoader: ⚠️ 未找到 RKTTSManager");
            }
        }
    }

    /// <summary>
    /// 按顺序加载 SDK
    /// </summary>
    IEnumerator LoadSDKsSequentially()
    {
        isLoading = true;

        // 初始延迟 - 等待 Unity 完成基础初始化
        if (enableDebugLog)
            Debug.Log($"SDKLoader: 等待 {initialDelay} 秒后开始加载...");

        yield return new WaitForSeconds(initialDelay);

        // === 第一步：加载 RKLLM ===
        if (enableDebugLog)
            Debug.Log("SDKLoader: [1/2] 正在加载 RKLLM...");

        if (llmManager != null)
        {
            // 启用 LLM Manager（会触发 OnEnable 开始初始化）
            llmManager.enabled = true;

            if (enableDebugLog)
                Debug.Log("SDKLoader: ✅ RKLLM 已启用，正在后台初始化");

            // 分帧加载，避免阻塞主线程
            yield return null;
            yield return null;
            yield return null;
        }
        else
        {
            Debug.LogWarning("SDKLoader: ⚠️ 跳过 RKLLM（未找到管理器）");
        }

        // 延迟一段时间再加载下一个 SDK
        if (enableDebugLog)
            Debug.Log($"SDKLoader: 等待 {delayBetweenSDKs} 秒...");

        yield return new WaitForSeconds(delayBetweenSDKs);

        // === 第二步：加载 RKTTS ===
        if (enableDebugLog)
            Debug.Log("SDKLoader: [2/2] 正在加载 RKTTS...");

        if (ttsManager != null)
        {
            // 启用 TTS Manager（会触发 OnEnable 开始初始化）
            ttsManager.enabled = true;

            if (enableDebugLog)
                Debug.Log("SDKLoader: ✅ RKTTS 已启用，正在后台初始化");

            // 分帧加载，避免阻塞主线程
            yield return null;
            yield return null;
            yield return null;
        }
        else
        {
            Debug.LogWarning("SDKLoader: ⚠️ 跳过 RKTTS（未找到管理器）");
        }

        // 完成
        isLoading = false;
        loadingComplete = true;

        if (enableDebugLog)
        {
            Debug.Log("SDKLoader: ==========================================");
            Debug.Log("SDKLoader: ✅ SDK 加载流程完成");
            Debug.Log("SDKLoader: 注意：SDK 仍在后台初始化（权限、模型加载等）");
            Debug.Log("SDKLoader: 请查看各 Manager 的日志了解初始化进度");
            Debug.Log("SDKLoader: ==========================================");
        }
    }

    /// <summary>
    /// 检查所有 SDK 是否都已初始化完成
    /// </summary>
    public bool AreAllSDKsReady()
    {
        bool llmReady = llmManager != null && llmManager.IsInitialized();
        bool ttsReady = ttsManager != null && ttsManager.IsInitialized();

        return llmReady && ttsReady;
    }

    /// <summary>
    /// 检查加载流程是否完成（不代表 SDK 已初始化）
    /// </summary>
    public bool IsLoadingComplete()
    {
        return loadingComplete;
    }

    /// <summary>
    /// 检查是否正在加载
    /// </summary>
    public bool IsLoading()
    {
        return isLoading;
    }

    /// <summary>
    /// 获取加载状态文本
    /// </summary>
    public string GetLoadingStatus()
    {
        if (isLoading)
        {
            return "正在加载 SDK...";
        }

        if (!loadingComplete)
        {
            return "准备加载...";
        }

        // 检查各 SDK 状态
        bool llmReady = llmManager != null && llmManager.IsInitialized();
        bool ttsReady = ttsManager != null && ttsManager.IsInitialized();

        if (llmReady && ttsReady)
        {
            return "✅ 所有 SDK 就绪";
        }

        string status = "SDK 初始化中:\n";

        if (llmManager != null)
        {
            status += llmReady ? "✅ LLM 就绪\n" : "⏳ LLM 初始化中...\n";
        }

        if (ttsManager != null)
        {
            status += ttsReady ? "✅ TTS 就绪" : "⏳ TTS 初始化中...";
        }

        return status;
    }

    #region 公开方法（可从其他脚本调用）

    /// <summary>
    /// 手动重新加载所有 SDK
    /// </summary>
    public void ReloadAllSDKs()
    {
        if (isLoading)
        {
            Debug.LogWarning("SDKLoader: 正在加载中，无法重新加载");
            return;
        }

        if (enableDebugLog)
            Debug.Log("SDKLoader: 手动重新加载 SDK");

        // 禁用所有 SDK
        if (llmManager != null) llmManager.enabled = false;
        if (ttsManager != null) ttsManager.enabled = false;

        // 重置状态
        loadingComplete = false;

        // 重新开始加载
        StartCoroutine(LoadSDKsSequentially());
    }

    #endregion
}