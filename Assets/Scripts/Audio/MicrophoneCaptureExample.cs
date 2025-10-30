using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MicrophoneCapture 使用示例
/// 演示如何使用麦克风捕获并处理音频
/// </summary>
public class MicrophoneCaptureExample : MonoBehaviour
{
    [Header("组件引用")]
    public MicrophoneCapture microphoneCapture;

    [Header("UI 显示")]
    public Text statusText;
    public Text volumeText;
    public Slider volumeSlider;
    public Image speakingIndicator;

    [Header("颜色设置")]
    public Color speakingColor = Color.green;
    public Color silentColor = Color.gray;

    void Start()
    {
        // 如果未指定，自动查找 MicrophoneCapture
        if (microphoneCapture == null)
        {
            microphoneCapture = GetComponent<MicrophoneCapture>();
            if (microphoneCapture == null)
            {
                microphoneCapture = FindObjectOfType<MicrophoneCapture>();
            }
        }

        if (microphoneCapture == null)
        {
            Debug.LogError("MicrophoneCaptureExample: 未找到 MicrophoneCapture 组件");
            return;
        }

        // 订阅事件
        SubscribeToEvents();

        UpdateStatus("麦克风已就绪");
    }

    void Update()
    {
        // 更新音量显示
        UpdateVolumeDisplay();
    }

    #region 事件订阅

    /// <summary>
    /// 订阅麦克风事件
    /// </summary>
    void SubscribeToEvents()
    {
        // 音频数据更新
        microphoneCapture.OnAudioDataCaptured += OnAudioDataCaptured;

        // 音量更新
        microphoneCapture.OnVolumeUpdated += OnVolumeUpdated;

        // 说话状态变化
        microphoneCapture.OnSpeakingStarted += OnSpeakingStarted;
        microphoneCapture.OnSpeakingStopped += OnSpeakingStopped;
    }

    void OnDestroy()
    {
        if (microphoneCapture != null)
        {
            microphoneCapture.OnAudioDataCaptured -= OnAudioDataCaptured;
            microphoneCapture.OnVolumeUpdated -= OnVolumeUpdated;
            microphoneCapture.OnSpeakingStarted -= OnSpeakingStarted;
            microphoneCapture.OnSpeakingStopped -= OnSpeakingStopped;
        }
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 当捕获到音频数据时调用
    /// </summary>
    void OnAudioDataCaptured(float[] audioData)
    {
        // 在这里可以处理音频数据
        // 例如：发送到语音识别SDK、进行频谱分析等
    }

    /// <summary>
    /// 当音量更新时调用
    /// </summary>
    void OnVolumeUpdated(float volume)
    {
        // 音量已更新，可以在 Update 中读取
    }

    /// <summary>
    /// 当开始说话时调用
    /// </summary>
    void OnSpeakingStarted()
    {
        if (speakingIndicator != null)
        {
            speakingIndicator.color = speakingColor;
        }
        UpdateStatus("正在说话");
    }

    /// <summary>
    /// 当停止说话时调用
    /// </summary>
    void OnSpeakingStopped()
    {
        if (speakingIndicator != null)
        {
            speakingIndicator.color = silentColor;
        }
        UpdateStatus("沉默中");
    }

    #endregion

    #region UI 更新

    /// <summary>
    /// 更新状态文本
    /// </summary>
    void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    /// <summary>
    /// 更新音量显示
    /// </summary>
    void UpdateVolumeDisplay()
    {
        if (microphoneCapture == null)
            return;

        float volume = microphoneCapture.GetCurrentVolume();

        if (volumeText != null)
        {
            volumeText.text = $"音量: {(volume * 100):F1}%";
        }

        if (volumeSlider != null)
        {
            volumeSlider.value = volume;
        }
    }

    #endregion

    #region 高级示例

    /// <summary>
    /// 示例：语音识别集成（需要语音识别SDK）
    /// </summary>
    void ProcessWithSpeechRecognition(float[] audioData)
    {
        // 取消注释以下代码以启用语音识别集成
        /*
        // 将音频数据发送到语音识别SDK
        SpeechRecognitionSDK speechSDK = FindObjectOfType<SpeechRecognitionSDK>();
        if (speechSDK != null)
        {
            string result = speechSDK.RecognizeSpeech(audioData);
            Debug.Log($"识别结果: {result}");
        }
        */
    }

    /// <summary>
    /// 示例：音频频谱分析
    /// </summary>
    void AnalyzeSpectrum(float[] audioData)
    {
        // 进行FFT分析等
        // 可以用于可视化音频、节拍检测等
    }

    /// <summary>
    /// 示例：噪音检测
    /// </summary>
    bool DetectNoise(float[] audioData)
    {
        // 计算音频的能量
        float energy = 0f;
        for (int i = 0; i < audioData.Length; i++)
        {
            energy += audioData[i] * audioData[i];
        }
        energy /= audioData.Length;

        // 如果能量超过阈值，认为是噪音
        return energy > 0.01f;
    }

    #endregion
}
