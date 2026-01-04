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

    // 音频流播放相关
    private AudioSource audioSource;
    private System.Collections.Concurrent.ConcurrentQueue<float> audioBufferQueue = new System.Collections.Concurrent.ConcurrentQueue<float>();
    private int ttsSampleRate = 16000;
    private bool isStreaming = false;
    private int streamingCallbackCount = 0;



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
        
        // 注册 TTS 数据回调
        if (SenseOnnxManager.Instance != null)
        {
            SenseOnnxManager.Instance.OnTtsAudioDataRecevied += OnTtsDataCallback;
            LoggerManager.Info("[RKLLMExample] 成功订阅 SenseOnnxManager.OnTtsAudioDataRecevied 事件", "LLM");
        }
        else
        {
            LoggerManager.Warning("[RKLLMExample] SenseOnnxManager.Instance 为空，无法订阅 TTS 回调", "LLM");
        }



        // 初始化响应文本
        if (responseText != null)
        {
            responseText.text = "wait...";
        }
        if (responseText != null)
        {
            responseText.text = "wait...";
        }

        // 初始化音频组件
        SetupAudioPlayer();
    }
    
    private void SetupAudioPlayer()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
        audioSource.loop = true; // 循环播放以便持续流式读取
        
        // 禁用 SenseOnnxManager 内部播放，改由 AudioSource 播放
        if (SenseOnnxManager.Instance != null)
        {
            SenseOnnxManager.Instance.useInternalAudioPlayer = false;
        }

        // 默认目标采样率为 44100
        ttsSampleRate = 44100;

        // 添加采样率诊断工具
        if (GetComponent<TTSSampleRateFix>() == null)
        {
            gameObject.AddComponent<TTSSampleRateFix>();
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

        // 取消注册 TTS 数据回调
        if (SenseOnnxManager.Instance != null)
        {
            SenseOnnxManager.Instance.OnTtsAudioDataRecevied -= OnTtsDataCallback;
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
                    // 再次检查订阅，防止意外丢失
                    var invocationList = typeof(SenseOnnxManager).GetField("OnTtsAudioDataRecevied", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)?.GetValue(SenseOnnxManager.Instance) as System.Delegate;
                    int subCount = invocationList?.GetInvocationList()?.Length ?? 0;
                    LoggerManager.Debug($"[RKLLMExample] 调用 TtsGenerate 前检查，TTS 订阅者数量: {subCount}", "LLM");

                    if (SenseOnnxManager.Instance.IsTtsAbilityReady())
                    {
                        LoggerManager.Debug("使用 SenseOnnx TTS Ability", "LLM");
                        SenseOnnxManager.Instance.TtsGenerate(fullResponse);
                    }
                    else
                    {
                        LoggerManager.Warning("SenseOnnx TTS Ability 未就绪", "LLM");
                    }
                }
                else
                {
                    LoggerManager.Warning("SenseOnnxManager 实例不存在", "LLM");
                }
            }
            else
            {
                LoggerManager.Warning("响应内容为空，跳过 TTS", "LLM");
            }
        }
    }


    /// <summary>
    /// TTS 音频数据回调（在后台线程调用 - 注意性能）
    /// </summary>
    private void OnTtsDataCallback(float[] data)
    {
        if (data == null || data.Length == 0)
        {
            return;
        }

        // 异步处理音频数据，避免阻塞 JNI 线程
        System.Threading.ThreadPool.QueueUserWorkItem(_ => ProcessAudioData(data));
    }

    /// <summary>
    /// 在后台线程处理音频数据
    /// </summary>
    private void ProcessAudioData(float[] data)
    {
        try
        {
            int sourceRate = 16000;
            if (SenseOnnxManager.Instance != null && SenseOnnxManager.Instance.GetTtsSampleRate() > 0)
            {
                sourceRate = SenseOnnxManager.Instance.GetTtsSampleRate();
            }

            float[] dataToEnqueue = data;

            // 如果源采样率不是 44100，则进行重采样
            if (sourceRate != ttsSampleRate)
            {
                dataToEnqueue = Resample(data, sourceRate, ttsSampleRate);
            }

            // 将数据放入队列（ConcurrentQueue 是线程安全的）
            int beforeCount = audioBufferQueue.Count;
            foreach (float sample in dataToEnqueue)
            {
                audioBufferQueue.Enqueue(sample);
            }

            // 如果还没开始播放，且缓冲了一定数据，则开始播放
            int requiredBuffer = (int)(ttsSampleRate * 0.1f);
            if (!isStreaming && audioBufferQueue.Count > requiredBuffer)
            {
                // AudioClip.Create and audioSource.Play must be on Main Thread
                UnityMainThreadDispatcher.Instance().Enqueue(() => {
                    StartStreamingPlayback();
                });
            }
        }
        catch (System.Exception e)
        {
            UnityMainThreadDispatcher.Instance().Enqueue(() => {
                LoggerManager.Error($"[音频处理] 处理音频数据失败: {e.Message}", "LLM");
            });
        }
    }

    /// <summary>
    /// 优化的线性重采样（减少浮点运算）
    /// </summary>
    private float[] Resample(float[] input, int sourceRate, int targetRate)
    {
        if (sourceRate == targetRate) return input;

        // 预计算常量，避免在循环中重复计算
        float ratio = (float)sourceRate / targetRate;
        int newLength = (int)(input.Length / ratio);
        float[] output = new float[newLength];
        int inputLength = input.Length;

        // 使用整数索引和固定点数学减少浮点运算
        for (int i = 0; i < newLength; i++)
        {
            float position = i * ratio;
            int index = (int)position;

            // 边界检查
            if (index >= inputLength - 1)
            {
                output[i] = input[inputLength - 1];
            }
            else
            {
                // 线性插值
                float frac = position - index;
                output[i] = input[index] + (input[index + 1] - input[index]) * frac;
            }
        }

        return output;
    }

    private void StartStreamingPlayback()
    {
        if (isStreaming)
        {
            return;
        }

        // 强制使用 44100Hz
        ttsSampleRate = 44100;

        LoggerManager.Info($"[播放器] 开始流式播放 - 采样率: {ttsSampleRate}", "LLM");

        if (audioSource == null)
        {
            LoggerManager.Error("[播放器] AudioSource 为空！", "LLM");
            return;
        }

        // 创建流式 AudioClip
        // 长度设置为较短（例如 10秒循环），通过回调填充
        AudioClip clip = AudioClip.Create("TTS_Stream", ttsSampleRate * 10, 1, ttsSampleRate, true, OnAudioRead);
        audioSource.clip = clip;
        audioSource.volume = 1.0f; // 确保音量最大
        audioSource.Play();
        isStreaming = true;

        LoggerManager.Info($"[播放器] 播放已启动", "LLM");
    }

    // Unity AudioSource 的 PCM 读取回调 (在此线程填充数据)
    private void OnAudioRead(float[] data)
    {
        streamingCallbackCount++;
        int samplesRead = 0;

        for (int i = 0; i < data.Length; i++)
        {
            if (audioBufferQueue.TryDequeue(out float sample))
            {
                data[i] = sample;
                samplesRead++;
            }
            else
            {
                // 队列为空，填充静音
                data[i] = 0f;
            }
        }

        // 每500次回调输出一次日志（减少日志压力）
        if (streamingCallbackCount % 500 == 0)
        {
            LoggerManager.Debug($"[OnAudioRead] #{streamingCallbackCount}, 读取: {samplesRead}/{data.Length}, 队列: {audioBufferQueue.Count}", "LLM");
        }
    }
    
    // 检测是否静音太久可以停止播放（可选优化，目前简化为一直播放直到 Stop）

}
