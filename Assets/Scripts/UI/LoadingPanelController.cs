using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 加载界面控制器
/// 自动显示 SDK 加载状态，加载完成后关闭面板
/// </summary>
public class LoadingPanelController : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("加载面板根对象（会在加载完成后自动隐藏）")]
    public GameObject loadingPanel;

    [Tooltip("状态文本（可选，用于显示加载进度）")]
    public Text statusText;

    [Tooltip("进度条（可选）")]
    public Slider progressBar;

    [Tooltip("提示文本（可选）")]
    public Text tipText;

    [Header("动画配置")]
    [Tooltip("是否使用淡出动画")]
    public bool useFadeOutAnimation = true;

    [Tooltip("淡出动画时长（秒）")]
    public float fadeOutDuration = 0.5f;

    [Tooltip("加载完成后延迟关闭的时间（秒）")]
    public float closeDelay = 0.5f;

    [Header("状态更新")]
    [Tooltip("状态文本更新间隔（秒）")]
    public float statusUpdateInterval = 0.5f;

    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    [Header("角色动画控制")]
    [Tooltip("角色动画管理器引用")]
    public AnimationManager characterAnimationManager;

    [Tooltip("是否在加载时停止角色动画")]
    public bool pauseCharacterAnimationOnLoad = true;

    // 组件引用
    private CanvasGroup canvasGroup;
    private bool isClosing = false;
    private bool characterAnimationWasPaused = false;

    // 提示文本列表
    private readonly string[] tips = new string[]
    {
        "Initializing AI models...",
        "Loading speech synthesis engine...",
        "Preparing interaction system...",
        "Almost ready, please wait...",
    };

    void Awake()
    {
        // 确保加载面板在开始时是显示的
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
        }

        // 获取或添加 CanvasGroup（用于淡出动画）
        if (useFadeOutAnimation)
        {
            if (loadingPanel != null)
            {
                canvasGroup = loadingPanel.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = loadingPanel.AddComponent<CanvasGroup>();
                }
                canvasGroup.alpha = 1f;
            }
        }

        if (enableDebugLog)
            Debug.Log("LoadingPanelController: 初始化完成");
    }

    void Start()
    {
        // 检查 SDKLoadMonitor 是否存在
        if (SDKLoadMonitor.Instance == null)
        {
            Debug.LogError("LoadingPanelController: 未找到 SDKLoadMonitor，无法监听加载事件");
            return;
        }

        // 停止角色动画（加载时）
        if (pauseCharacterAnimationOnLoad && characterAnimationManager != null)
        {
            characterAnimationManager.PauseAnimation();
            characterAnimationWasPaused = true;

            if (enableDebugLog)
                Debug.Log("LoadingPanelController: 已暂停角色动画");
        }

        // 订阅 SDK 加载完成事件
        SDKLoadMonitor.Instance.OnAllSDKsReady.AddListener(OnSDKsLoadComplete);

        // 订阅 SDK 加载失败事件
        SDKLoadMonitor.Instance.OnSDKLoadFailed.AddListener(OnSDKsLoadFailed);

        // 开始更新状态
        StartCoroutine(UpdateStatusRoutine());

        // 开始轮播提示
        if (tipText != null)
        {
            StartCoroutine(UpdateTipsRoutine());
        }

        if (enableDebugLog)
            Debug.Log("LoadingPanelController: 开始监听 SDK 加载状态");
    }

    /// <summary>
    /// SDK 加载完成回调
    /// </summary>
    void OnSDKsLoadComplete()
    {
        if (enableDebugLog)
            Debug.Log("LoadingPanelController: 收到 SDK 加载完成事件，准备关闭加载面板");

        // 延迟关闭面板
        StartCoroutine(CloseLoadingPanelWithDelay());
    }

    /// <summary>
    /// SDK 加载失败回调
    /// </summary>
    void OnSDKsLoadFailed(string error)
    {
        Debug.LogError($"LoadingPanelController: SDK 加载失败 - {error}");

        // 显示错误信息
        if (statusText != null)
        {
            statusText.text = $"加载失败: {error}\n请重启应用";
            statusText.color = Color.red;
        }

        // 停止进度条动画
        StopAllCoroutines();
    }

    /// <summary>
    /// 延迟关闭加载面板
    /// </summary>
    IEnumerator CloseLoadingPanelWithDelay()
    {
        if (isClosing) yield break;
        isClosing = true;

        // 显示完成状态
        if (statusText != null)
        {
            statusText.text = "✅ 加载完成！";
            statusText.color = Color.green;
        }

        if (progressBar != null)
        {
            progressBar.value = 1f;
        }

        // 恢复角色动画
        if (characterAnimationWasPaused && characterAnimationManager != null)
        {
            characterAnimationManager.ResumeAnimation();

            if (enableDebugLog)
                Debug.Log("LoadingPanelController: 已恢复角色动画");
        }

        // 等待一小段时间让用户看到完成状态
        yield return new WaitForSeconds(closeDelay);

        // 执行关闭动画
        if (useFadeOutAnimation && canvasGroup != null)
        {
            yield return StartCoroutine(FadeOutPanel());
        }

        // 隐藏面板
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);

            if (enableDebugLog)
                Debug.Log("LoadingPanelController: 加载面板已关闭");
        }
    }

    /// <summary>
    /// 淡出动画
    /// </summary>
    IEnumerator FadeOutPanel()
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;
        float startAlpha = canvasGroup.alpha;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / fadeOutDuration);
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// 更新状态文本
    /// </summary>
    IEnumerator UpdateStatusRoutine()
    {
        while (!isClosing)
        {
            if (SDKLoadMonitor.Instance != null && statusText != null)
            {
                statusText.text = SDKLoadMonitor.Instance.GetStatusText();
            }

            // 更新进度条
            if (SDKLoadMonitor.Instance != null && progressBar != null)
            {
                var (llm, tts, face) = SDKLoadMonitor.Instance.GetSDKReadyStatus();
                int readyCount = (llm ? 1 : 0) + (tts ? 1 : 0) + (face ? 1 : 0);
                int totalCount = 3;

                // 如果没有 Face Manager，总数为 2
                if (FindObjectOfType<RKFaceManager>() == null)
                    totalCount = 2;

                progressBar.value = (float)readyCount / totalCount;
            }

            yield return new WaitForSeconds(statusUpdateInterval);
        }
    }

    /// <summary>
    /// 轮播提示文本
    /// </summary>
    IEnumerator UpdateTipsRoutine()
    {
        int tipIndex = 0;

        while (!isClosing)
        {
            if (tipText != null)
            {
                tipText.text = tips[tipIndex];
                tipIndex = (tipIndex + 1) % tips.Length;
            }

            yield return new WaitForSeconds(2f);
        }
    }

    /// <summary>
    /// 手动关闭加载面板（供外部调用）
    /// </summary>
    public void ClosePanel()
    {
        if (!isClosing)
        {
            StartCoroutine(CloseLoadingPanelWithDelay());
        }
    }

    /// <summary>
    /// 立即关闭加载面板（无动画）
    /// </summary>
    public void CloseImmediately()
    {
        StopAllCoroutines();
        isClosing = true;

        if (loadingPanel != null)
        {
            loadingPanel.SetActive(false);
        }

        if (enableDebugLog)
            Debug.Log("LoadingPanelController: 立即关闭加载面板");
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (SDKLoadMonitor.Instance != null)
        {
            SDKLoadMonitor.Instance.OnAllSDKsReady.RemoveListener(OnSDKsLoadComplete);
            SDKLoadMonitor.Instance.OnSDKLoadFailed.RemoveListener(OnSDKsLoadFailed);
        }
    }
}
