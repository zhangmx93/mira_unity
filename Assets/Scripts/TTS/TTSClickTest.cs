using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// TTS 点击测试
/// 点击屏幕时随机播放一段对话
/// </summary>
public class TTSClickTest : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("TTS 管理器（如果为空，会自动查找）")]
    public RKTTSManager ttsManager;

    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    [Tooltip("测试对话列表")]
    [TextArea(3, 10)]
    public string[] testDialogues = new string[]
    {
        "你好，我是语音助手",
        "今天天气真不错",
        "人工智能正在改变世界",
        "Unity 是一个强大的游戏引擎",
        "机器学习让计算机能够自主学习",
        "语音识别技术越来越先进",
        "欢迎使用文字转语音功能",
        "这是一个测试语音",
        "科技让生活更美好",
        "请问有什么可以帮助您的吗"
    };

    [Header("UI 引用（可选）")]
    [Tooltip("状态文本 - 显示当前播放的内容")]
    public Text statusText;

    [Tooltip("提示文本 - 显示操作说明")]
    public Text hintText;

    private int clickCount = 0;

    void Start()
    {
        // 查找 TTS 管理器
        if (ttsManager == null)
        {
            ttsManager = RKTTSManager.Instance;

            if (ttsManager == null)
            {
                ttsManager = FindObjectOfType<RKTTSManager>();
            }

            if (ttsManager == null)
            {
                LoggerManager.Error("未找到 RKTTSManager！", "TTS");
                if (statusText != null)
                    statusText.text = "错误: 未找到 TTS 管理器";
                return;
            }
        }

        // 订阅 TTS 事件
        ttsManager.OnTTSStarted += OnTTSStarted;
        ttsManager.OnTTSFinished += OnTTSFinished;
        ttsManager.OnTTSError += OnTTSError;

        // 显示提示
        if (hintText != null)
        {
            hintText.text = "点击屏幕播放随机对话";
        }

        if (statusText != null)
        {
            statusText.text = "就绪 - 点击屏幕开始测试";
        }

        if (enableDebugLog)
            LoggerManager.Debug("初始化完成，点击屏幕播放随机对话", "TTS");
    }

    void Update()
    {
        // 检测鼠标点击或触摸
        bool clicked = false;

        // PC: 鼠标左键
        if (Input.GetMouseButtonDown(0))
        {
            clicked = true;
        }

        // 移动设备: 触摸
        if (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began)
        {
            clicked = true;
        }

        // 空格键也可以触发（方便测试）
        if (Input.GetKeyDown(KeyCode.Space))
        {
            clicked = true;
        }

        if (clicked)
        {
            PlayRandomDialogue();
        }
    }

    /// <summary>
    /// 播放随机对话
    /// </summary>
    void PlayRandomDialogue()
    {
        if (ttsManager == null)
        {
            LoggerManager.Error("TTS 管理器未设置", "TTS");
            return;
        }

        if (!ttsManager.IsInitialized())
        {
            LoggerManager.Warning("TTS 尚未初始化完成，请稍候", "TTS");
            if (statusText != null)
                statusText.text = "TTS 正在初始化，请稍候...";
            return;
        }

        if (testDialogues == null || testDialogues.Length == 0)
        {
            LoggerManager.Error("测试对话列表为空", "TTS");
            return;
        }

        // 随机选择一段对话
        int randomIndex = Random.Range(0, testDialogues.Length);
        string dialogue = testDialogues[randomIndex];

        clickCount++;

        if (enableDebugLog)
            LoggerManager.Debug($"[{clickCount}] 播放随机对话 (索引 {randomIndex}): {dialogue}", "TTS");

        // 播放 TTS
        ttsManager.Speak(dialogue);

        // 更新 UI
        if (statusText != null)
        {
            statusText.text = $"[{clickCount}] 正在播放:\n{dialogue}";
        }
    }

    /// <summary>
    /// TTS 开始回调
    /// </summary>
    void OnTTSStarted()
    {
        if (enableDebugLog)
            LoggerManager.Debug("TTS 开始", "TTS");
    }

    /// <summary>
    /// TTS 完成回调
    /// </summary>
    void OnTTSFinished()
    {
        if (enableDebugLog)
            LoggerManager.Debug("TTS 完成", "TTS");

        if (statusText != null)
        {
            statusText.text = $"已完成 {clickCount} 次播放\n点击屏幕继续";
        }
    }

    /// <summary>
    /// TTS 错误回调
    /// </summary>
    void OnTTSError(string error)
    {
        LoggerManager.Error($"TTS 错误 - {error}", "TTS");

        if (statusText != null)
        {
            statusText.text = $"错误: {error}";
        }
    }

    void OnDestroy()
    {
        // 取消订阅事件
        if (ttsManager != null)
        {
            ttsManager.OnTTSStarted -= OnTTSStarted;
            ttsManager.OnTTSFinished -= OnTTSFinished;
            ttsManager.OnTTSError -= OnTTSError;
        }
    }

    #region 公开方法（可从 UI Button 调用）

    /// <summary>
    /// 手动触发播放（可从 Button 调用）
    /// </summary>
    public void OnPlayButtonClick()
    {
        PlayRandomDialogue();
    }

    /// <summary>
    /// 停止 TTS（可从 Button 调用）
    /// </summary>
    public void OnStopButtonClick()
    {
        if (ttsManager != null)
        {
            ttsManager.Stop();

            if (statusText != null)
            {
                statusText.text = "已停止播放";
            }

            if (enableDebugLog)
                LoggerManager.Debug("停止 TTS", "TTS");
        }
    }

    /// <summary>
    /// 播放指定索引的对话（可从 UI 调用）
    /// </summary>
    public void PlayDialogueByIndex(int index)
    {
        if (testDialogues == null || index < 0 || index >= testDialogues.Length)
        {
            LoggerManager.Error($"无效的对话索引 {index}", "TTS");
            return;
        }

        string dialogue = testDialogues[index];

        if (enableDebugLog)
            LoggerManager.Debug($"播放对话 [{index}]: {dialogue}", "TTS");

        if (ttsManager != null)
        {
            ttsManager.Speak(dialogue);
        }

        if (statusText != null)
        {
            statusText.text = $"[{index}] 正在播放:\n{dialogue}";
        }
    }

    #endregion
}