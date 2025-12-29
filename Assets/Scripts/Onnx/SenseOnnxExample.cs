using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SenseOnnx 管理器使用示例
/// 展示如何使用 SenseOnnxManager 进行 TTS 和 LLM 对话
/// </summary>
public class SenseOnnxExample : MonoBehaviour
{
    [Header("UI 引用")]
    [Tooltip("输入框")]
    public InputField inputField;

    [Tooltip("发送按钮")]
    public Button sendButton;

    [Tooltip("语音按钮")]
    public Button speakButton;

    [Tooltip("对话按钮（文本 → LLM → TTS）")]
    public Button conversationButton;

    [Tooltip("停止按钮")]
    public Button stopButton;

    [Tooltip("状态文本")]
    public Text statusText;

    [Tooltip("响应文本")]
    public Text responseText;

    void Start()
    {
        // 等待 SenseOnnxManager 初始化
        if (SenseOnnxManager.Instance != null)
        {
            // 订阅事件
            SenseOnnxManager.Instance.OnSenseOnnxInitialized += OnInitialized;
            SenseOnnxManager.Instance.OnConversationResponse += OnResponse;
            SenseOnnxManager.Instance.OnInitializationError += OnError;
        }

        // 绑定 UI 按钮
        if (sendButton != null)
            sendButton.onClick.AddListener(OnSendButtonClicked);

        if (speakButton != null)
            speakButton.onClick.AddListener(OnSpeakButtonClicked);

        if (conversationButton != null)
            conversationButton.onClick.AddListener(OnConversationButtonClicked);

        if (stopButton != null)
            stopButton.onClick.AddListener(OnStopButtonClicked);

        UpdateStatus("等待初始化...");
    }

    /// <summary>
    /// 初始化完成回调
    /// </summary>
    private void OnInitialized()
    {
        LoggerManager.Info("SenseOnnx 初始化完成", "Example");
        UpdateStatus("就绪");

        // 启用按钮
        if (sendButton != null) sendButton.interactable = true;
        if (speakButton != null) speakButton.interactable = true;
        if (conversationButton != null) conversationButton.interactable = true;
    }

    /// <summary>
    /// 错误回调
    /// </summary>
    private void OnError(string error)
    {
        LoggerManager.Error($"初始化错误: {error}", "Example");
        UpdateStatus($"错误: {error}");
    }

    /// <summary>
    /// 响应回调
    /// </summary>
    private void OnResponse(string response)
    {
        LoggerManager.Info($"收到响应: {response}", "Example");
        if (responseText != null)
        {
            responseText.text = response;
        }
    }

    /// <summary>
    /// 发送按钮点击 - 仅发送到 LLM，不转语音
    /// </summary>
    private void OnSendButtonClicked()
    {
        if (inputField == null || string.IsNullOrEmpty(inputField.text))
        {
            LoggerManager.Warning("输入不能为空", "Example");
            return;
        }

        if (SenseOnnxManager.Instance == null || !SenseOnnxManager.Instance.IsInitialized())
        {
            LoggerManager.Warning("SenseOnnx 未初始化", "Example");
            return;
        }

        string message = inputField.text;
        LoggerManager.Info($"发送消息到 LLM: {message}", "Example");

        UpdateStatus("发送中...");
        SenseOnnxManager.Instance.SendToLLM(message);

        inputField.text = "";
    }

    /// <summary>
    /// 语音按钮点击 - 仅将文本转为语音
    /// </summary>
    private void OnSpeakButtonClicked()
    {
        if (inputField == null || string.IsNullOrEmpty(inputField.text))
        {
            LoggerManager.Warning("输入不能为空", "Example");
            return;
        }

        if (SenseOnnxManager.Instance == null || !SenseOnnxManager.Instance.IsTTSReady())
        {
            LoggerManager.Warning("TTS 未就绪", "Example");
            return;
        }

        string text = inputField.text;
        LoggerManager.Info($"朗读文本: {text}", "Example");

        UpdateStatus("朗读中...");
        SenseOnnxManager.Instance.Speak(text);

        inputField.text = "";
    }

    /// <summary>
    /// 对话按钮点击 - 完整流程: 文本 → LLM → TTS
    /// </summary>
    private void OnConversationButtonClicked()
    {
        if (inputField == null || string.IsNullOrEmpty(inputField.text))
        {
            LoggerManager.Warning("输入不能为空", "Example");
            return;
        }

        if (SenseOnnxManager.Instance == null || !SenseOnnxManager.Instance.IsInitialized())
        {
            LoggerManager.Warning("SenseOnnx 未初始化", "Example");
            return;
        }

        if (SenseOnnxManager.Instance.IsProcessing())
        {
            LoggerManager.Warning("正在处理中，请稍候", "Example");
            return;
        }

        string message = inputField.text;
        LoggerManager.Info($"开始对话流程: {message}", "Example");

        UpdateStatus("对话中...");
        SenseOnnxManager.Instance.ProcessConversation(message);

        inputField.text = "";
    }

    /// <summary>
    /// 停止按钮点击
    /// </summary>
    private void OnStopButtonClicked()
    {
        if (SenseOnnxManager.Instance != null)
        {
            LoggerManager.Info("停止当前操作", "Example");
            SenseOnnxManager.Instance.Stop();
            UpdateStatus("已停止");
        }
    }

    /// <summary>
    /// 更新状态文本
    /// </summary>
    private void UpdateStatus(string status)
    {
        if (statusText != null)
        {
            statusText.text = $"状态: {status}";
        }

        LoggerManager.Debug($"状态更新: {status}", "Example");
    }

    /// <summary>
    /// 显示当前状态信息（调试用）
    /// </summary>
    [ContextMenu("显示状态信息")]
    public void ShowStatusInfo()
    {
        if (SenseOnnxManager.Instance != null)
        {
            string info = SenseOnnxManager.Instance.GetStatusInfo();
            LoggerManager.Info(info, "Example");
            Debug.Log(info);
        }
        else
        {
            LoggerManager.Warning("SenseOnnxManager 实例不存在", "Example");
        }
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (SenseOnnxManager.Instance != null)
        {
            SenseOnnxManager.Instance.OnSenseOnnxInitialized -= OnInitialized;
            SenseOnnxManager.Instance.OnConversationResponse -= OnResponse;
            SenseOnnxManager.Instance.OnInitializationError -= OnError;
        }

        // 取消 UI 绑定
        if (sendButton != null)
            sendButton.onClick.RemoveListener(OnSendButtonClicked);

        if (speakButton != null)
            speakButton.onClick.RemoveListener(OnSpeakButtonClicked);

        if (conversationButton != null)
            conversationButton.onClick.RemoveListener(OnConversationButtonClicked);

        if (stopButton != null)
            stopButton.onClick.RemoveListener(OnStopButtonClicked);
    }
}
