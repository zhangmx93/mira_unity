using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// 麦克风捕获管理器
/// 用于持续监听麦克风并获取音频数据
/// </summary>
public class MicrophoneCapture : MonoBehaviour
{
    [Header("麦克风设置")]
    [Tooltip("目标麦克风设备名称，留空则使用默认麦克风")]
    public string targetMicrophoneName = "";

    [Tooltip("采样率（Hz）")]
    public int sampleRate = 44100;

    [Tooltip("录音片段长度（秒），循环录音")]
    public int recordLength = 10;

    [Header("音频分析")]
    [Tooltip("是否启用音量检测")]
    public bool enableVolumeDetection = true;

    [Tooltip("音量更新间隔（秒）")]
    [Range(0.01f, 1f)]
    public float volumeUpdateInterval = 0.1f;

    [Tooltip("音量阈值（用于检测是否在说话）")]
    [Range(0f, 1f)]
    public float volumeThreshold = 0.01f;

    [Header("调试")]
    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = false;

    // AudioClip 对象
    private AudioClip microphoneClip;

    // 麦克风是否正在录音
    private bool isRecording = false;

    // 当前音量
    private float currentVolume = 0f;

    // 音量检测计时器
    private float volumeTimer = 0f;

    // 最后读取的位置
    private int lastSamplePosition = 0;

    // 音频数据缓冲区
    private float[] audioBuffer;
    private int bufferSize = 1024;

    // 是否正在说话
    private bool isSpeaking = false;

    // 事件：音频数据更新时触发
    public event Action<float[]> OnAudioDataCaptured;

    // 事件：音量更新时触发
    public event Action<float> OnVolumeUpdated;

    // 事件：开始说话时触发
    public event Action OnSpeakingStarted;

    // 事件：停止说话时触发
    public event Action OnSpeakingStopped;

    #region Unity 生命周期

    void Start()
    {
        // 如果有PermissionManager，等待它完成权限请求
        // 否则自己请求权限
        PermissionManager permissionManager = FindObjectOfType<PermissionManager>();
        if (permissionManager != null)
        {
            StartCoroutine(WaitForPermissionManager());
        }
        else
        {
            StartCoroutine(RequestMicrophonePermissionAndStart());
        }
    }

    /// <summary>
    /// 等待PermissionManager完成权限请求
    /// </summary>
    private System.Collections.IEnumerator WaitForPermissionManager()
    {
        PermissionManager permissionManager = PermissionManager.Instance;

        // 等待PermissionManager完成所有权限请求
        while (permissionManager != null && !permissionManager.AreAllPermissionsGranted())
        {
            yield return new WaitForSeconds(0.1f);
        }

        // 权限请求完成后，直接启动麦克风
        StartMicrophone();
    }


    void Update()
    {
        if (!isRecording)
            return;

        // 更新音量检测
        if (enableVolumeDetection)
        {
            volumeTimer += Time.deltaTime;
            if (volumeTimer >= volumeUpdateInterval)
            {
                volumeTimer = 0f;
                UpdateVolume();
            }
        }

        // 捕获音频数据
        CaptureAudioData();
    }

    void OnDestroy()
    {
        StopMicrophone();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            PauseMicrophone();
        }
        else
        {
            ResumeMicrophone();
        }
    }

    #endregion

    #region 麦克风控制

    /// <summary>
    /// 请求麦克风权限并启动
    /// </summary>
    private System.Collections.IEnumerator RequestMicrophonePermissionAndStart()
    {
        // 方法1：Unity的权限API（Android/iOS）
        #if UNITY_ANDROID || UNITY_IOS
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.LogError("MicrophoneCapture: 麦克风权限被拒绝");
            yield break;
        }
        #endif

        // 等待一帧
        yield return new WaitForEndOfFrame();

        // 方法2：强制触发系统权限弹窗（macOS/Windows）
        // 在macOS上，必须实际访问Microphone.devices才会触发系统权限弹窗
        int deviceCount = Microphone.devices.Length;

        // 等待权限弹窗响应
        yield return new WaitForSeconds(0.5f);

        // 启动麦克风
        StartMicrophone();
    }

    /// <summary>
    /// 启动麦克风
    /// </summary>
    public void StartMicrophone()
    {
        if (isRecording)
        {
            if (enableDebugLog)
                Debug.LogWarning("MicrophoneCapture: 麦克风已在录音中");
            return;
        }

        // 检查是否有可用的麦克风
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("MicrophoneCapture: 未找到可用的麦克风设备");
            return;
        }

        // 选择麦克风
        string selectedDevice = "";
        if (!string.IsNullOrEmpty(targetMicrophoneName))
        {
            // 使用指定的麦克风
            bool found = false;
            foreach (string device in Microphone.devices)
            {
                if (device == targetMicrophoneName)
                {
                    selectedDevice = device;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                Debug.LogWarning($"MicrophoneCapture: 未找到名为 '{targetMicrophoneName}' 的麦克风，使用默认麦克风");
                selectedDevice = Microphone.devices[0];
            }
        }
        else
        {
            // 使用默认麦克风
            selectedDevice = Microphone.devices[0];
        }

        // 初始化音频缓冲区
        audioBuffer = new float[bufferSize];

        // 开始录音
        microphoneClip = Microphone.Start(selectedDevice, true, recordLength, sampleRate);
        isRecording = true;
        lastSamplePosition = 0;
    }

    /// <summary>
    /// 停止麦克风
    /// </summary>
    public void StopMicrophone()
    {
        if (isRecording)
        {
            Microphone.End(GetCurrentMicrophoneName());
            isRecording = false;
        }

        if (microphoneClip != null)
        {
            Destroy(microphoneClip);
            microphoneClip = null;
        }
    }

    /// <summary>
    /// 暂停麦克风
    /// </summary>
    public void PauseMicrophone()
    {
        if (isRecording)
        {
            Microphone.End(GetCurrentMicrophoneName());
            isRecording = false;
        }
    }

    /// <summary>
    /// 恢复麦克风
    /// </summary>
    public void ResumeMicrophone()
    {
        if (!isRecording && microphoneClip != null)
        {
            StartMicrophone();
        }
    }

    #endregion

    #region 音频捕获

    /// <summary>
    /// 捕获音频数据
    /// </summary>
    private void CaptureAudioData()
    {
        if (microphoneClip == null)
            return;

        int currentPosition = Microphone.GetPosition(GetCurrentMicrophoneName());

        // 避免位置相同，没有新数据
        if (currentPosition == lastSamplePosition)
            return;

        // 处理循环录音的情况
        int samplesToRead;
        if (currentPosition > lastSamplePosition)
        {
            samplesToRead = currentPosition - lastSamplePosition;
        }
        else
        {
            // 循环了，需要读取到末尾再从头开始
            samplesToRead = microphoneClip.samples - lastSamplePosition + currentPosition;
        }

        // 限制读取大小
        if (samplesToRead > bufferSize)
        {
            samplesToRead = bufferSize;
        }

        // 读取音频数据
        if (audioBuffer.Length < samplesToRead)
        {
            audioBuffer = new float[samplesToRead];
        }

        microphoneClip.GetData(audioBuffer, lastSamplePosition);
        lastSamplePosition = currentPosition;

        // 触发事件
        OnAudioDataCaptured?.Invoke(audioBuffer);
    }

    /// <summary>
    /// 更新音量
    /// </summary>
    private void UpdateVolume()
    {
        if (microphoneClip == null || audioBuffer == null)
            return;

        // 计算音量（RMS - Root Mean Square）
        float sum = 0f;
        for (int i = 0; i < audioBuffer.Length; i++)
        {
            sum += audioBuffer[i] * audioBuffer[i];
        }
        currentVolume = Mathf.Sqrt(sum / audioBuffer.Length);

        // 触发音量更新事件
        OnVolumeUpdated?.Invoke(currentVolume);

        // 检测是否在说话
        bool wasIsSpeaking = isSpeaking;
        isSpeaking = currentVolume > volumeThreshold;

        // 触发说话状态变化事件
        if (isSpeaking && !wasIsSpeaking)
        {
            OnSpeakingStarted?.Invoke();
        }
        else if (!isSpeaking && wasIsSpeaking)
        {
            OnSpeakingStopped?.Invoke();
        }
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 获取当前使用的麦克风名称
    /// </summary>
    private string GetCurrentMicrophoneName()
    {
        if (!string.IsNullOrEmpty(targetMicrophoneName))
        {
            foreach (string device in Microphone.devices)
            {
                if (device == targetMicrophoneName)
                    return device;
            }
        }
        return Microphone.devices.Length > 0 ? Microphone.devices[0] : null;
    }

    /// <summary>
    /// 获取麦克风录音状态
    /// </summary>
    public bool IsRecording()
    {
        return isRecording;
    }

    /// <summary>
    /// 获取当前音量
    /// </summary>
    public float GetCurrentVolume()
    {
        return currentVolume;
    }

    /// <summary>
    /// 获取是否正在说话
    /// </summary>
    public bool IsSpeaking()
    {
        return isSpeaking;
    }

    /// <summary>
    /// 获取 AudioClip 对象
    /// </summary>
    public AudioClip GetAudioClip()
    {
        return microphoneClip;
    }

    /// <summary>
    /// 列出所有可用的麦克风设备
    /// </summary>
    public static string[] GetAvailableMicrophones()
    {
        return Microphone.devices;
    }

    /// <summary>
    /// 设置缓冲区大小
    /// </summary>
    public void SetBufferSize(int size)
    {
        bufferSize = size;
        audioBuffer = new float[bufferSize];
    }

    #endregion
}
