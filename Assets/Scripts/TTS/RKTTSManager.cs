using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// RKTTS 管理器
/// 用于在 Unity 中调用 Android 原生的 RKTTS (文字转语音) 功能
/// 使用 TTSAudioPlayer 组件进行音频播放
/// </summary>
[DefaultExecutionOrder(100)]  // 延后执行顺序，等待 SDKLoader 启用
public class RKTTSManager : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("音频速度 (0.5 - 2.0)")]
    [Range(0.5f, 2.0f)]
    public float speed = 1.0f;

    [Tooltip("音频音调 (0.5 - 2.0)")]
    [Range(0.5f, 2.0f)]
    public float pitch = 1.0f;

    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    [Tooltip("TTS 采样率 - RKTTS 输出 44100 Hz")]
    public int sampleRate = 44100;

    [Tooltip("是否自动播放音频")]
    public bool autoPlayAudio = true;

    [Header("引用")]
    [Tooltip("音频播放器组件")]
    public TTSAudioPlayer audioPlayer;

    // Android JNI 相关
    private AndroidJavaObject ttsDetector;
    private AndroidJavaObject unityActivity;
    private bool isInitialized = false;
    private bool isPlaying = false;

    // 事件
    public event Action<float[], bool> OnTTSResult;  // FloatArray, isChunk
    public event Action<string> OnTTSError;
    public event Action OnTTSStarted;
    public event Action OnTTSFinished;

    // 单例
    private static RKTTSManager instance;
    public static RKTTSManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        LoggerManager.Debug("Awake() 被调用", "TTS");

        if (instance != null && instance != this)
        {
            LoggerManager.Debug("检测到重复实例，销毁当前对象", "TTS");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        // 自动创建或获取 TTSAudioPlayer
        if (audioPlayer == null)
        {
            audioPlayer = GetComponent<TTSAudioPlayer>();
            if (audioPlayer == null)
            {
                audioPlayer = gameObject.AddComponent<TTSAudioPlayer>();
                LoggerManager.Debug("已自动添加 TTSAudioPlayer 组件", "TTS");
            }
        }

        // 配置 audioPlayer 的采样率
        if (audioPlayer != null)
        {
            audioPlayer.sampleRate = sampleRate;
            audioPlayer.channels = 1;  // TTS 通常是单声道
        }

        // 尝试设置 Unity 音频配置为 44100 Hz
        TrySetupAudioConfiguration();

        LoggerManager.Debug("单例设置完成", "TTS");

        // 延迟加载模式：初始时禁用，等待 SDKLoader 启用
        enabled = false;
        LoggerManager.Debug("已禁用，等待 SDKLoader 延迟加载", "TTS");
    }

    void OnEnable()
    {
        LoggerManager.Debug("OnEnable() 被调用 - 开始初始化", "TTS");

        // 确保采样率设置正确（防止 Inspector 中被修改）
        if (sampleRate != 44100)
        {
            LoggerManager.Warning($"⚠️ 采样率不是 44100 Hz (当前: {sampleRate})，正在修复...", "TTS");
            sampleRate = 44100;
        }

        // 确保 audioPlayer 采样率正确
        if (audioPlayer != null && audioPlayer.sampleRate != 44100)
        {
            LoggerManager.Warning($"⚠️ AudioPlayer 采样率不是 44100 Hz (当前: {audioPlayer.sampleRate})，正在修复...", "TTS");
            audioPlayer.sampleRate = 44100;
        }

        // 被 SDKLoader 启用时才开始初始化
#if UNITY_ANDROID && !UNITY_EDITOR
        LoggerManager.Debug("检测到 Android 平台，准备初始化...", "TTS");

        // 请求音频录制权限（虽然 TTS 不需要录音，但参考示例中有此权限）
        RequestAudioPermissions();

        // 请求存储权限（SDK需要访问模型文件）
        RequestStoragePermissions();

        // 延迟初始化，等待权限授予
        StartCoroutine(InitializeAfterPermissions());
#else
        if (enableDebugLog)
            LoggerManager.Warning("当前平台不支持 RKTTS (仅支持 Android)", "TTS");
#endif
    }

    /// <summary>
    /// 尝试设置 Unity 音频配置
    /// </summary>
    private void TrySetupAudioConfiguration()
    {
        try
        {
            AudioConfiguration config = AudioSettings.GetConfiguration();

            LoggerManager.Debug($"当前 Unity 音频配置:", "TTS");
            LoggerManager.Debug($"  Sample Rate: {config.sampleRate} Hz", "TTS");
            LoggerManager.Debug($"  Output Sample Rate: {AudioSettings.outputSampleRate} Hz", "TTS");
            LoggerManager.Debug($"  DSP Buffer Size: {config.dspBufferSize}", "TTS");

            // 如果采样率不是 44100，尝试更改
            if (config.sampleRate != 44100 && AudioSettings.outputSampleRate != 44100)
            {
                LoggerManager.Warning($"音频采样率不是 44100 Hz", "TTS");
                LoggerManager.Warning($"  当前: {config.sampleRate} Hz", "TTS");
                LoggerManager.Warning($"  期望: 44100 Hz", "TTS");

                // Android 平台尝试更改可能无效
                config.sampleRate = 44100;
                bool success = AudioSettings.Reset(config);

                if (success)
                {
                    LoggerManager.Info($"✅ 已尝试更新音频采样率", "TTS");
                    LoggerManager.Info($"  新的 Output Sample Rate: {AudioSettings.outputSampleRate} Hz", "TTS");
                }
                else
                {
                    LoggerManager.Warning($"❌ 无法更改音频采样率", "TTS");
                    LoggerManager.Warning($"  TTSAudioPlayer 会自动处理采样率转换", "TTS");
                }
            }
            else
            {
                LoggerManager.Debug($"✅ 音频采样率已是 44100 Hz", "TTS");
            }
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"设置音频配置失败: {e.Message}", "TTS");
        }
    }

    /// <summary>
    /// 请求音频权限
    /// </summary>
    private void RequestAudioPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            LoggerManager.Debug("请求音频录制权限...", "TTS");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
        }
#endif
    }

    /// <summary>
    /// 请求存储权限
    /// </summary>
    private void RequestStoragePermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageRead))
        {
            LoggerManager.Debug("请求读取存储权限...", "TTS");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageRead);
        }

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite))
        {
            LoggerManager.Debug("请求写入存储权限...", "TTS");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageWrite);
        }
#endif
    }

    /// <summary>
    /// 等待权限授予后初始化
    /// </summary>
    private IEnumerator InitializeAfterPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 等待用户授予权限
        float timeout = 10f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            bool hasStorage = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageRead);
            bool hasAudio = UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone);

            if (hasStorage && hasAudio)
            {
                LoggerManager.Debug("所有权限已授予，开始初始化", "TTS");
                InitializeRKTTS();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        // 超时或用户拒绝权限
        LoggerManager.Warning("未获得全部权限，尝试继续初始化（可能失败）", "TTS");
        InitializeRKTTS();
#endif
        yield return null;
    }

    /// <summary>
    /// 初始化 RKTTS
    /// </summary>
    private void InitializeRKTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (enableDebugLog)
                LoggerManager.Debug("开始初始化 RKTTS...", "TTS");

            // 步骤 1: 获取 Unity Activity
            if (enableDebugLog)
                LoggerManager.Debug("[1/4] 获取 Unity Activity...", "TTS");

            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            if (unityActivity == null)
            {
                LoggerManager.Error("无法获取 Unity Activity", "TTS");
                return;
            }

            if (enableDebugLog)
                LoggerManager.Debug("[1/4] ✅ Unity Activity 获取成功", "TTS");

            // 步骤 2: 创建 SenseRKTtsDetector 实例
            if (enableDebugLog)
                LoggerManager.Debug("[2/4] 创建 SenseRKTtsDetector 实例...", "TTS");

            // 尝试 1: 使用 Application 参数的构造函数（最常见）
            try
            {
                AndroidJavaObject application = unityActivity.Call<AndroidJavaObject>("getApplication");
                ttsDetector = new AndroidJavaObject("com.senseflow.rktts.SenseRKTtsDetector", application);

                if (enableDebugLog)
                    LoggerManager.Debug("[2/4] ✅ 使用构造函数 (Application) 创建成功", "TTS");
            }
            catch (System.Exception e1)
            {
                LoggerManager.Warning($"构造函数 (Application) 失败: {e1.Message}", "TTS");

                // 尝试 2: 使用 Activity 参数的构造函数
                try
                {
                    ttsDetector = new AndroidJavaObject("com.senseflow.rktts.SenseRKTtsDetector", unityActivity);

                    if (enableDebugLog)
                        LoggerManager.Debug("[2/4] ✅ 使用构造函数 (Activity) 创建成功", "TTS");
                }
                catch (System.Exception e2)
                {
                    LoggerManager.Error($"构造函数 (Activity) 也失败: {e2.Message}", "TTS");

                    // 尝试 3: 无参构造函数
                    try
                    {
                        ttsDetector = new AndroidJavaObject("com.senseflow.rktts.SenseRKTtsDetector");

                        if (enableDebugLog)
                            LoggerManager.Debug("[2/4] ✅ 使用无参构造函数创建成功", "TTS");
                    }
                    catch (System.Exception e3)
                    {
                        LoggerManager.Error($"所有构造方法都失败", "TTS");
                        LoggerManager.Error($"  - Application: {e1.Message}", "TTS");
                        LoggerManager.Error($"  - Activity: {e2.Message}", "TTS");
                        LoggerManager.Error($"  - 无参: {e3.Message}", "TTS");
                        return;
                    }
                }
            }

            if (ttsDetector == null)
            {
                LoggerManager.Error("无法创建 SenseRKTtsDetector 实例", "TTS");
                return;
            }

            // 步骤 3: 设置结果监听器
            if (enableDebugLog)
                LoggerManager.Debug("[3/4] 设置结果监听器...", "TTS");

            ttsDetector.Call("setOnResultListener", new RKTTSResultListener(this));

            if (enableDebugLog)
                LoggerManager.Debug("[3/4] ✅ 结果监听器设置成功", "TTS");

            // 步骤 4: 初始化并启动
            if (enableDebugLog)
                LoggerManager.Debug("[4/4] 初始化并启动 TTS...", "TTS");

            ttsDetector.Call("initialize");
            ttsDetector.Call("start");

            isInitialized = true;

            if (enableDebugLog)
                LoggerManager.Info("✅ RKTTS 初始化完成", "TTS");
        }
        catch (Exception e)
        {
            LoggerManager.Error($"初始化失败 - {e.Message}\n{e.StackTrace}", "TTS");
            OnTTSError?.Invoke($"初始化失败: {e.Message}");
            isInitialized = false;
        }
#endif
    }

    /// <summary>
    /// 文字转语音
    /// </summary>
    /// <param name="text">要转换的文本</param>
    /// <param name="customSpeed">自定义速度（可选，默认使用配置的速度）</param>
    /// <param name="customPitch">自定义音调（可选，默认使用配置的音调）</param>
    public void Speak(string text, float? customSpeed = null, float? customPitch = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized)
        {
            LoggerManager.Error("RKTTS 未初始化", "TTS");
            OnTTSError?.Invoke("RKTTS 未初始化");
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            LoggerManager.Warning("文本不能为空", "TTS");
            return;
        }

        try
        {
            float actualSpeed = customSpeed ?? speed;
            float actualPitch = customPitch ?? pitch;

            if (enableDebugLog)
                LoggerManager.Debug($"开始 TTS - 文本: '{text}', 速度: {actualSpeed}, 音调: {actualPitch}", "TTS");

            // 清空 audioPlayer 的缓冲区，准备接收新数据（防止重复播放）
            if (audioPlayer != null)
            {
                audioPlayer.ClearBuffer();
                if (enableDebugLog)
                    LoggerManager.Debug("已清空音频缓冲区", "TTS");
            }

            // 根据 Kotlin 示例: tts(text: String, speed: Float, pitch: Int)
            int pitchInt = Mathf.RoundToInt(actualPitch);

            isPlaying = true;
            OnTTSStarted?.Invoke();

            ttsDetector.Call("tts", text, 1.0f, 1);
        }
        catch (Exception e)
        {
            LoggerManager.Error($"TTS 执行失败 - {e.Message}", "TTS");
            OnTTSError?.Invoke($"TTS 执行失败: {e.Message}");
            isPlaying = false;
        }
#else
        LoggerManager.Warning($"[模拟] TTS - {text}", "TTS");
        // 编辑器模式下的模拟
        StartCoroutine(SimulateTTS(text));
#endif
    }

    /// <summary>
    /// 停止 TTS 和音频播放
    /// </summary>
    public void Stop()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (isInitialized && ttsDetector != null)
        {
            try
            {
                if (enableDebugLog)
                    LoggerManager.Debug("停止 TTS", "TTS");

                ttsDetector.Call("stop");
            }
            catch (Exception e)
            {
                LoggerManager.Error($"停止 TTS 失败 - {e.Message}", "TTS");
            }
        }
#endif

        // 停止音频播放并清空缓冲区
        if (audioPlayer != null)
        {
            audioPlayer.Stop();
            audioPlayer.ClearBuffer();
        }

        isPlaying = false;
        OnTTSFinished?.Invoke();
    }

    /// <summary>
    /// 编辑器模式下的模拟 TTS
    /// </summary>
    private IEnumerator SimulateTTS(string text)
    {
        OnTTSStarted?.Invoke();
        yield return new WaitForSeconds(2f);

        // 模拟音频数据
        float[] fakeAudioData = new float[44100]; // 1秒的数据
        for (int i = 0; i < fakeAudioData.Length; i++)
        {
            fakeAudioData[i] = Mathf.Sin(i * 0.05f) * 0.5f;
        }

        OnTTSResult?.Invoke(fakeAudioData, false);

        // 自动播放（如果启用）
        if (autoPlayAudio && audioPlayer != null)
        {
            audioPlayer.AddAudioData(fakeAudioData);
            audioPlayer.Play();
        }

        OnTTSFinished?.Invoke();
    }

    /// <summary>
    /// 处理来自 Android 的结果���调
    /// </summary>
    internal void HandleResult(float[] result, bool isChunk)
    {
        if (enableDebugLog)
            LoggerManager.Debug($"收到音频数据 - 大小: {result?.Length ?? 0}, isChunk: {isChunk}", "TTS");

        OnTTSResult?.Invoke(result, isChunk);

        // 将音频数据添加到 TTSAudioPlayer
        if (result != null && result.Length > 0 && audioPlayer != null)
        {
            audioPlayer.AddAudioData(result);

            if (enableDebugLog)
                LoggerManager.Debug($"已添加 {result.Length} 个音频采样到播放器", "TTS");
        }

        // 如果不是分块（isChunk = false），说明音频接收完成
        if (!isChunk)
        {
            if (enableDebugLog)
                LoggerManager.Debug("TTS 音频接收完成", "TTS");

            // 自动播放（如果启用）
            if (autoPlayAudio && audioPlayer != null)
            {
                audioPlayer.Play();

                // 监听播放完成
                if (audioPlayer.GetDuration() > 0)
                {
                    StartCoroutine(WaitForAudioComplete(audioPlayer.GetDuration()));
                }
            }
        }
    }

    /// <summary>
    /// 等待音频播放完成
    /// </summary>
    private IEnumerator WaitForAudioComplete(float duration)
    {
        yield return new WaitForSeconds(duration);

        isPlaying = false;
        OnTTSFinished?.Invoke();

        if (enableDebugLog)
            LoggerManager.Debug("音频播放完成", "TTS");
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (ttsDetector != null)
        {
            try
            {
                if (isInitialized)
                {
                    ttsDetector.Call("stop");
                }
                ttsDetector.Dispose();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"销毁时出错 - {e.Message}", "TTS");
            }
        }
#endif
    }

    #region 公开 API

    /// <summary>
    /// 检查是否已初始化
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// 检查是否正在播放
    /// </summary>
    public bool IsPlaying()
    {
        return isPlaying || (audioPlayer != null && audioPlayer.IsPlaying());
    }

    /// <summary>
    /// 设置语音速度
    /// </summary>
    public void SetSpeed(float newSpeed)
    {
        speed = Mathf.Clamp(newSpeed, 0.5f, 2.0f);
        if (enableDebugLog)
            LoggerManager.Debug($"设置速度为 {speed}", "TTS");
    }

    /// <summary>
    /// 设置语音音调
    /// </summary>
    public void SetPitch(float newPitch)
    {
        pitch = Mathf.Clamp(newPitch, 0.5f, 2.0f);
        if (enableDebugLog)
            LoggerManager.Debug($"设置音调为 {pitch}", "TTS");
    }

    /// <summary>
    /// 设置采样率
    /// </summary>
    public void SetSampleRate(int newSampleRate)
    {
        sampleRate = newSampleRate;

        if (audioPlayer != null)
        {
            audioPlayer.SetSampleRate(newSampleRate);
        }

        if (enableDebugLog)
            LoggerManager.Debug($"设置采样率为 {sampleRate} Hz", "TTS");
    }

    /// <summary>
    /// 设置音量
    /// </summary>
    public void SetVolume(float volume)
    {
        if (audioPlayer != null)
        {
            audioPlayer.SetVolume(volume);
            if (enableDebugLog)
                LoggerManager.Debug($"设置音量为 {volume}", "TTS");
        }
    }

    /// <summary>
    /// 获取当前音频缓冲区大小
    /// </summary>
    public int GetBufferSize()
    {
        return audioPlayer != null ? audioPlayer.GetBufferSize() : 0;
    }

    /// <summary>
    /// 获取音频时长
    /// </summary>
    public float GetDuration()
    {
        return audioPlayer != null ? audioPlayer.GetDuration() : 0f;
    }

    /// <summary>
    /// 手动播放缓冲区音频（当 autoPlayAudio = false 时使用）
    /// </summary>
    public void PlayBufferedAudio()
    {
        if (audioPlayer != null)
        {
            audioPlayer.Play();
        }
    }

    /// <summary>
    /// 保存最后的 TTS 音频到文件（调试用）
    /// </summary>
    public void SaveLastAudioToFile(string filename)
    {
        if (audioPlayer != null)
        {
            audioPlayer.SaveToWavFile(filename);
        }
    }

    #endregion
}

/// <summary>
/// RKTTS 结果监听器（Android 回调）
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class RKTTSResultListener : AndroidJavaProxy
{
    private RKTTSManager manager;

    public RKTTSResultListener(RKTTSManager manager)
        : base("com.senseflow.rktts.OnResultListener")
    {
        this.manager = manager;
    }

    // 对应 Android 的 onResult(FloatArray result, Boolean isChunk)
    public void onResult(float[] result, bool isChunk)
    {
        // 切换到主线程处理
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            manager.HandleResult(result, isChunk);
        });
    }
}
#endif