using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// RKLLM 使用示例
/// 演示如何使用 RKLLMManager 与 LLM 进行对话
/// </summary>
public class RKLLMExample : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("输入文本框")]
    public TMP_InputField inputField;

    [Tooltip("发送按钮")]
    public Button sendButton;

    [Tooltip("显示 LLM 回复的文本")]
    public TextMeshProUGUI responseText;

    [Header("引用")]
    [Tooltip("RKLLM 管理器")]
    public RKLLMManager rkllmManager;

    [Tooltip("是否在对话结束后自动播放 TTS")]
    public bool enableAutoTTS = true;

    // 累积的响应文本
    private System.Text.StringBuilder responseBuilder = new System.Text.StringBuilder();

    // TTS 管理器引用
    private RKTTSManager ttsManager;

    void Start()
    {
        // 确保有主线程调度器
        if (!UnityMainThreadDispatcher.Exists())
        {
            GameObject dispatcher = new GameObject("UnityMainThreadDispatcher");
            dispatcher.AddComponent<UnityMainThreadDispatcher>();
        }

        // 查找 RKLLMManager
        if (rkllmManager == null)
        {
            Debug.Log("RKLLMExample: 正在查找 RKLLMManager...");
            rkllmManager = FindObjectOfType<RKLLMManager>();
        }

        if (rkllmManager != null)
        {
            Debug.Log($"RKLLMExample: 找到 RKLLMManager - IsInitialized: {rkllmManager.IsInitialized()}");
        }
        else
        {
            Debug.LogError("RKLLMExample: 未找到 RKLLMManager！请确保场景中有 RKLLMManager 组件");
        }

        // 设置按钮点击事件
        if (sendButton != null)
        {
            sendButton.onClick.AddListener(OnSendButtonClicked);
        }

        // 订阅 LLM 结果事件
        if (rkllmManager != null)
        {
            rkllmManager.OnLLMResult += OnLLMResult;
            rkllmManager.OnLLMError += OnLLMError;
            rkllmManager.OnLLMComplete += OnLLMComplete;  // 订阅对话完成事件
        }
        else
        {
            Debug.LogError("RKLLMExample: 未找到 RKLLMManager");
        }

        // 查找 TTS 管理器
        if (enableAutoTTS)
        {
            ttsManager = RKTTSManager.Instance;
            if (ttsManager != null)
            {
                Debug.Log("RKLLMExample: 找到 RKTTSManager，已启用自动 TTS");
            }
            else
            {
                Debug.LogWarning("RKLLMExample: 未找到 RKTTSManager，自动 TTS 将被禁用");
                enableAutoTTS = false;
            }
        }

        // 初始化响应文本
        if (responseText != null)
        {
            responseText.text = "wait...";
        }
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (rkllmManager != null)
        {
            rkllmManager.OnLLMResult -= OnLLMResult;
            rkllmManager.OnLLMError -= OnLLMError;
            rkllmManager.OnLLMComplete -= OnLLMComplete;
        }
    }

    /// <summary>
    /// 发送按钮点击事件
    /// </summary>
    private void OnSendButtonClicked()
    {
        if (inputField == null || string.IsNullOrEmpty(inputField.text))
        {
            Debug.LogWarning("RKLLMExample: 输入内容为空");
            return;
        }

        if (rkllmManager == null)
        {
            Debug.LogError("RKLLMExample: RKLLMManager 未设置");
            return;
        }

        // 清空之前的响应
        responseBuilder.Clear();
        if (responseText != null)
        {
            responseText.text = "thinking...";
        }

        string message = inputField.text;

        // 只发送文本消息
        rkllmManager.Chat(message);

        // 清空输入框
        inputField.text = "";
    }

    /// <summary>
    /// 处理 LLM 结果
    /// </summary>
    private void OnLLMResult(string result)
    {
        // 累积响应文本
        responseBuilder.Append(result);

        // 更新 UI
        if (responseText != null)
        {
            responseText.text = responseBuilder.ToString();
        }

        Debug.Log($"RKLLMExample: 收到响应 - {result}");
    }

    /// <summary>
    /// 处理 LLM 错误
    /// </summary>
    private void OnLLMError(string error)
    {
        if (responseText != null)
        {
            responseText.text = $"error: {error}";
        }

        Debug.LogError($"RKLLMExample: LLM 错误 - {error}");
    }

    /// <summary>
    /// 处理 LLM 对话完成（callState == 2）
    /// </summary>
    private void OnLLMComplete()
    {
        Debug.Log("RKLLMExample: LLM 对话完成");

        // 如果启用了自动 TTS，将完整的响应内容发送给 TTS
        if (enableAutoTTS && ttsManager != null)
        {
            string fullResponse = responseBuilder.ToString();

            if (!string.IsNullOrEmpty(fullResponse))
            {
                Debug.Log($"RKLLMExample: 发送到 TTS - {fullResponse.Length} 个字符");
                ttsManager.Speak(fullResponse);
            }
            else
            {
                Debug.LogWarning("RKLLMExample: 响应内容为空，跳过 TTS");
            }
        }
    }
}
