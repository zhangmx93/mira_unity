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
            LoggerManager.Debug("正在查找 RKLLMManager...", "LLM");
            rkllmManager = FindObjectOfType<RKLLMManager>();
        }

        if (rkllmManager != null)
        {
            LoggerManager.Info($"找到 RKLLMManager - IsInitialized: {rkllmManager.IsInitialized()}", "LLM");
        }
        else
        {
            LoggerManager.Error("未找到 RKLLMManager！请确保场景中有 RKLLMManager 组件", "LLM");
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
            LoggerManager.Error("未找到 RKLLMManager", "LLM");
        }

        // 查找 TTS 管理器
        if (enableAutoTTS)
        {
            ttsManager = RKTTSManager.Instance;
            if (ttsManager != null)
            {
                LoggerManager.Info("找到 RKTTSManager，已启用自动 TTS", "LLM");
            }
            else
            {
                LoggerManager.Warning("未找到 RKTTSManager，自动 TTS 将被禁用", "LLM");
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
            LoggerManager.Warning("输入内容为空", "LLM");
            return;
        }

        if (rkllmManager == null)
        {
            LoggerManager.Error("RKLLMManager 未设置", "LLM");
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

        LoggerManager.Debug($"收到响应 - {result}", "LLM");
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

        LoggerManager.Error($"LLM 错误 - {error}", "LLM");
    }

    /// <summary>
    /// 处理 LLM 对话完成（callState == 2）
    /// </summary>
    private void OnLLMComplete()
    {
        LoggerManager.Info("LLM 对话完成", "LLM");

        // 如果启用了自动 TTS，将完整的响应内容发送给 TTS
        if (enableAutoTTS)
        {
            string fullResponse = responseBuilder.ToString();

            if (!string.IsNullOrEmpty(fullResponse))
            {
                LoggerManager.Debug($"发送到 TTS - {fullResponse.Length} 个字符", "LLM");

                // 使用 SenseOnnxManager 进行 TTS
                if (SenseOnnxManager.Instance != null)
                {
                    // 优先使用 TTS Ability
                    if (SenseOnnxManager.Instance.IsTtsAbilityReady())
                    {
                        LoggerManager.Debug("使用 TTS Ability", "LLM");
                        SenseOnnxManager.Instance.TtsGenerate(fullResponse);
                    }
                    // 如果 TTS Ability 未就绪，使用 RK TTS
                    else if (SenseOnnxManager.Instance.IsTTSReady())
                    {
                        LoggerManager.Debug("使用 RK TTS", "LLM");
                        SenseOnnxManager.Instance.Speak(fullResponse);
                    }
                    else
                    {
                        LoggerManager.Warning("所有 TTS 均未就绪", "LLM");
                    }
                }
                else
                {
                    LoggerManager.Warning("SenseOnnxManager 实例不存在，回退到直接调用 TTS", "LLM");
                    // 回退：直接调用 ttsManager（如果存在）
                    // if (ttsManager != null)
                    // {
                    //     ttsManager.Speak(fullResponse);
                    // }
                }
            }
            else
            {
                LoggerManager.Warning("响应内容为空，跳过 TTS", "LLM");
            }
        }
    }
}
