using UnityEngine;
using System.Collections.Generic;
using System.Collections;

/// <summary>
/// TTS 流式音频播放器
/// 支持收到 chunk 后立即播放，无需等待所有数据接收完成
/// 适用于实时语音合成场景
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class TTSStreamingAudioPlayer : MonoBehaviour
{
    [Header("音频配置")]
    [Tooltip("采样率（Hz）- RKTTS 使用 44100")]
    public int sampleRate = 44100;

    [Tooltip("声道数（1=单声道，2=立体声）")]
    public int channels = 1;

    [Tooltip("音量 (0-1)")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("流式播放配置")]
    [Tooltip("开始播放前的最小缓冲大小（采样数）")]
    public int minBufferSizeToStart = 4410; // 约 0.1 秒 @ 44100Hz

    [Tooltip("是否启用流式播放（收到第一个 chunk 就开始播放）")]
    public bool enableStreamingPlayback = true;

    [Tooltip("缓冲区不足时是否暂停播放")]
    public bool pauseOnBufferUnderrun = true;

    [Header("动画集成")]
    [Tooltip("是否启用说话动画")]
    public bool enableTalkAnimation = true;

    [Tooltip("动画管理器（如果为空会自动查找）")]
    public AnimationManager animationManager;

    [Tooltip("说话动画名称")]
    public string talkAnimationName = "Talk";

    [Tooltip("空闲动画名称")]
    public string idleAnimationName = "Idle";

    [Header("调试")]
    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    // 音频组件
    private AudioSource audioSource;

    // 音频缓冲队列
    private Queue<float> audioQueue = new Queue<float>();
    private object queueLock = new object();

    // 当前播放的 AudioClip
    private AudioClip streamingClip;
    private int clipPosition = 0;
    private float[] clipBuffer;

    // 状态
    private bool isStreaming = false;
    private bool isReceivingData = false;
    private bool hasStartedPlaying = false;
    private int totalReceivedSamples = 0;
    private int underrunCount = 0;  // 记录缓冲不足次数
    private float lastUnderrunWarningTime = 0f;  // 上次警告时间

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.volume = volume;
        audioSource.loop = false;

        // 确保采样率为 44100 Hz（RKTTS 固定输出）
        if (sampleRate != 44100)
        {
            LoggerManager.Warning($"⚠️ 采样率不是 44100 Hz (当前: {sampleRate})，正在修复...", "TTS");
            sampleRate = 44100;

            // 重新计算最小缓冲大小（保持 0.1 秒）
            minBufferSizeToStart = (int)(sampleRate * 0.1f);
            LoggerManager.Info($"✅ 已修正采样率为 {sampleRate} Hz，最小缓冲: {minBufferSizeToStart} 采样", "TTS");
        }

        // 自动查找 AnimationManager
        if (enableTalkAnimation && animationManager == null)
        {
            animationManager = FindObjectOfType<AnimationManager>();
            if (animationManager != null)
            {
                LogDebug("已自动找到 AnimationManager");
            }
            else
            {
                LoggerManager.Warning("未找到 AnimationManager，说话动画将不可用", "TTS");
            }
        }
    }

    /// <summary>
    /// 添加音频数据块（chunk）
    /// </summary>
    public void AddAudioChunk(float[] data, bool isFinalChunk)
    {
        if (data == null || data.Length == 0)
        {
            LogDebug("收到空数据块，跳过");
            return;
        }

        lock (queueLock)
        {
            // 添加数据到队列
            foreach (float sample in data)
            {
                audioQueue.Enqueue(sample);
            }

            totalReceivedSamples += data.Length;
            isReceivingData = true;

            LogDebug($"添加音频块: {data.Length} 采样, 队列总计: {audioQueue.Count}, 是否最后: {isFinalChunk}");
        }

        // 如果启用流式播放且未开始播放，检查是否可以开始
        if (enableStreamingPlayback && !hasStartedPlaying)
        {
            if (audioQueue.Count >= minBufferSizeToStart)
            {
                StartStreamingPlayback();
            }
        }

        // 如果是最后一个块，标记数据接收完成
        if (isFinalChunk)
        {
            isReceivingData = false;
            LogDebug($"数据接收完成，总计: {totalReceivedSamples} 采样");

            // 如果未启用流式播放，则在接收完成后一次性播放
            if (!enableStreamingPlayback && !hasStartedPlaying)
            {
                PlayBufferedAudio();
            }
        }
    }

    /// <summary>
    /// 开始流式播放
    /// </summary>
    private void StartStreamingPlayback()
    {
        if (hasStartedPlaying)
        {
            LogDebug("已经在播放中，跳过");
            return;
        }

        LoggerManager.Info("开始流式播放", "TTS");

        // 重置计数器
        underrunCount = 0;
        lastUnderrunWarningTime = 0f;

        // 创建流式 AudioClip
        int clipLength = sampleRate * 60; // 创建 60 秒的缓冲区（足够大）
        streamingClip = AudioClip.Create(
            "TTS_Streaming_" + Time.time,
            clipLength,
            channels,
            sampleRate,
            true,
            OnAudioRead,
            OnAudioSetPosition
        );

        audioSource.clip = streamingClip;
        audioSource.Play();

        hasStartedPlaying = true;
        isStreaming = true;

        // 启动说话动画
        if (enableTalkAnimation && animationManager != null)
        {
            animationManager.PlayAnimation(talkAnimationName);
            LogDebug($"已启动说话动画 '{talkAnimationName}'");
        }
    }

    /// <summary>
    /// 一次性播放缓冲区音频（非流式模式）
    /// </summary>
    private void PlayBufferedAudio()
    {
        if (hasStartedPlaying)
        {
            LogDebug("已经在播放中，跳过");
            return;
        }

        int bufferSize = audioQueue.Count;
        if (bufferSize == 0)
        {
            LoggerManager.Warning("音频缓冲区为空", "TTS");
            return;
        }

        LoggerManager.Info($"开始播放缓冲音频: {bufferSize} 采样", "TTS");

        // 创建 AudioClip
        AudioClip clip = AudioClip.Create(
            "TTS_Buffered_" + Time.time,
            bufferSize,
            channels,
            sampleRate,
            false
        );

        // 填充数据
        float[] buffer = new float[bufferSize];
        lock (queueLock)
        {
            for (int i = 0; i < bufferSize; i++)
            {
                buffer[i] = audioQueue.Dequeue();
            }
        }

        clip.SetData(buffer, 0);

        // 播放
        audioSource.clip = clip;
        audioSource.Play();

        hasStartedPlaying = true;
        isStreaming = false;

        float duration = (float)bufferSize / sampleRate;
        LogDebug($"播放音频 - 时长: {duration:F2}秒");

        // 启动说话动画
        if (enableTalkAnimation && animationManager != null)
        {
            animationManager.PlayAnimation(talkAnimationName);
            LogDebug($"已启动说话动画 '{talkAnimationName}'");
        }

        // 监听播放完成
        StartCoroutine(WaitForPlaybackComplete(duration));
    }

    /// <summary>
    /// 音频读取回调（用于流式播放）
    /// </summary>
    private void OnAudioRead(float[] data)
    {
        lock (queueLock)
        {
            int samplesToRead = Mathf.Min(data.Length, audioQueue.Count);

            // 从队列中读取数据
            for (int i = 0; i < samplesToRead; i++)
            {
                data[i] = audioQueue.Dequeue();
            }

            // 如果数据不足，填充静音
            if (samplesToRead < data.Length)
            {
                for (int i = samplesToRead; i < data.Length; i++)
                {
                    data[i] = 0f;
                }

                // 如果数据接收完成且队列已空，停止流式播放
                if (!isReceivingData && audioQueue.Count == 0)
                {
                    LogDebug("流式播放完成");
                    StopStreamingPlayback();
                }
                else if (pauseOnBufferUnderrun && isReceivingData)
                {
                    underrunCount++;

                    // 只在关键时刻输出警告（避免刷屏）
                    // 1. 每秒最多输出一次
                    // 2. 只在还在接收数据时警告
                    float currentTime = Time.time;
                    if (currentTime - lastUnderrunWarningTime > 1.0f)
                    {
                        LoggerManager.Warning(
                            $"缓冲区不足: 需要 {data.Length}, 只有 {samplesToRead} (累计 {underrunCount} 次)",
                            "TTS"
                        );
                        lastUnderrunWarningTime = currentTime;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 音频位置设置回调
    /// </summary>
    private void OnAudioSetPosition(int newPosition)
    {
        clipPosition = newPosition;
    }

    /// <summary>
    /// 停止流式播放
    /// </summary>
    private void StopStreamingPlayback()
    {
        if (!isStreaming)
            return;

        isStreaming = false;

        // 在主线程中停止
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            if (audioSource != null && audioSource.isPlaying)
            {
                audioSource.Stop();
            }

            // 停止说话动画
            if (enableTalkAnimation && animationManager != null)
            {
                animationManager.PlayAnimation(idleAnimationName);
                LogDebug($"已切换到 '{idleAnimationName}' 动画");
            }

            hasStartedPlaying = false;
            LoggerManager.Info("流式播放已停止", "TTS");
        });
    }

    /// <summary>
    /// 等待播放完成（非流式模式）
    /// </summary>
    private IEnumerator WaitForPlaybackComplete(float duration)
    {
        yield return new WaitForSeconds(duration);

        // 停止说话动画
        if (enableTalkAnimation && animationManager != null)
        {
            animationManager.PlayAnimation(idleAnimationName);
            LogDebug($"音频播放完成，已切换到 '{idleAnimationName}' 动画");
        }

        hasStartedPlaying = false;
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        if (isStreaming)
        {
            StopStreamingPlayback();
        }
        else if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        // 停止说话动画
        if (enableTalkAnimation && animationManager != null)
        {
            animationManager.PlayAnimation(idleAnimationName);
            LogDebug($"已停止播放，切换到 '{idleAnimationName}' 动画");
        }

        hasStartedPlaying = false;
        isStreaming = false;
    }

    /// <summary>
    /// 重置播放器
    /// </summary>
    public void Reset()
    {
        Stop();
        ClearBuffer();
    }

    /// <summary>
    /// 清空音频缓冲区
    /// </summary>
    public void ClearBuffer()
    {
        lock (queueLock)
        {
            audioQueue.Clear();
            totalReceivedSamples = 0;
            isReceivingData = false;
            underrunCount = 0;
            lastUnderrunWarningTime = 0f;
            LogDebug("已清空音频缓冲区");
        }
    }

    /// <summary>
    /// 获取当前是否正在播放
    /// </summary>
    public bool IsPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }

    /// <summary>
    /// 获取音频缓冲区大小（采样数）
    /// </summary>
    public int GetBufferSize()
    {
        lock (queueLock)
        {
            return audioQueue.Count;
        }
    }

    /// <summary>
    /// 获取已接收的总采样数
    /// </summary>
    public int GetTotalReceivedSamples()
    {
        return totalReceivedSamples;
    }

    /// <summary>
    /// 设置音量
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }

    /// <summary>
    /// 设置采样率（仅在未播放时有效）
    /// </summary>
    public void SetSampleRate(int newSampleRate)
    {
        if (hasStartedPlaying)
        {
            LoggerManager.Warning("播放器正在运行，无法修改采样率", "TTS");
            return;
        }

        // 强制使用 44100 Hz（RKTTS 固定输出）
        if (newSampleRate != 44100)
        {
            LoggerManager.Warning($"⚠️ 尝试设置非标准采样率 {newSampleRate} Hz，强制使用 44100 Hz", "TTS");
            newSampleRate = 44100;
        }

        sampleRate = newSampleRate;

        // 重新计算最小缓冲大小（保持 0.1 秒）
        minBufferSizeToStart = (int)(sampleRate * 0.1f);

        LoggerManager.Info($"✅ 采样率设置为 {sampleRate} Hz，最小缓冲: {minBufferSizeToStart} 采样", "TTS");
    }

    /// <summary>
    /// 调试日志
    /// </summary>
    private void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            LoggerManager.Debug(message, "TTS");
        }
    }

    void OnDestroy()
    {
        Stop();
    }
}
