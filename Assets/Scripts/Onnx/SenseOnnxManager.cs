using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// SenseOnnx 管理器
/// 只负责初始化和提供接口,不做业务处理
/// </summary>
[DefaultExecutionOrder(100)]
public class SenseOnnxManager : MonoBehaviour
{
    #region 单例模式

    private static SenseOnnxManager instance;

    public static SenseOnnxManager Instance
    {
        get { return instance; }
    }

    #endregion

    #region 配置参数

    [Header("TTS 配置")]
    [Tooltip("是否自动启用 TTS")]
    public bool enableTTS = true;

    [Tooltip("TTS 管理器引用（RKTTSManager - 非 Onnx）")]
    public RKTTSManager ttsManager;

    [Header("Onnx TTS 配置")]
    [Tooltip("是否启用 Onnx TTS（独立 SDK）")]
    public bool enableOnnxTTS = true;

    [Header("STT 配置")]
    [Tooltip("是否自动启用 STT")]
    public bool enableSTT = true;

    [Tooltip("STT 管理器引用（如果有）")]
    public MonoBehaviour sttManager;  // 预留 STT 管理器位置

    #endregion

    #region Android JNI 对象

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject senseOnnxInstance;  // SenseOnnx 单例
    private AndroidJavaObject recordAbility;      // 录音能力
    private AndroidJavaObject ttsAbility;         // TTS 能力
    private AndroidJavaObject sttAbility;         // STT 能力
    private AndroidJavaObject kwsAbility;         // KWS 唤醒词识别能力
    private AndroidJavaObject audioTrack;         // 音频播放器
    private AndroidJavaObject unityActivity;
#endif

    #endregion

    #region 状态变量

    private bool isInitialized = false;
    private int ttsSampleRate = 16000; // tts采样率
    private bool isTTSReady = false;
    private bool isSTTReady = false;
    private bool isOnnxTTSReady = false;
    private bool isOnnxSTTReady = false;
    private bool isRecordReady = false;
    private bool isKwsReady = false;
    private bool kwsSwitch = false;  // 唤醒词开关
    private bool wakeup = false;     // 唤醒状态

    #endregion

    #region 回调监听器

    // TTS 数据回调监听器
    private DataCallbackListener<float[]> ttsDataCallback;
#if UNITY_ANDROID && !UNITY_EDITOR
    private TtsCallbackProxy ttsProxy;
#endif

    // STT 数据回调监听器
    private DataCallbackListener<string> sttDataCallback;
#if UNITY_ANDROID && !UNITY_EDITOR
    private SttCallbackProxy sttProxy;
#endif

    // KWS 数据回调监听器
    private DataCallbackListener<string> kwsDataCallback;
#if UNITY_ANDROID && !UNITY_EDITOR
    private KwsCallbackProxy kwsProxy;
#endif

    // Record 数据回调监听器
    private DataCallbackListener<float[]> recordDataCallback;
#if UNITY_ANDROID && !UNITY_EDITOR
    private RecordCallbackProxy recordProxy;
#endif

    #endregion

    #region 事件

    public event Action OnSenseOnnxInitialized;
    public event Action<string> OnInitializationError;
    public event Action<string> OnConversationResponse;
    public event Action<string> OnSttResult;  // STT 识别结果事件
    public event Action<string> OnKwsDetected;  // KWS 唤醒词检测事件
    public event Action<float[]> OnTtsAudioChunk;  // TTS 音频块事件
    public event Action<float[]> OnTtsAudioDataRecevied; // 新增 TTS 数据事件
    public bool useInternalAudioPlayer = true; // 是否使用内部 AudioTrack播放

    #endregion

    #region 业务逻辑变量

    private bool isProcessing = false;

    #endregion

    #region Unity 生命周期

    void Awake()
    {
        LoggerManager.Debug("SenseOnnxManager Awake() 被调用", "SenseOnnx");

        // 单例检查
        if (instance != null && instance != this)
        {
            LoggerManager.Debug("检测到重复实例，销毁当前对象", "SenseOnnx");
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // 自动获取 TTS 管理器
        if (enableTTS && ttsManager == null)
        {
            ttsManager = GetComponent<RKTTSManager>();
            if (ttsManager == null)
            {
                ttsManager = FindObjectOfType<RKTTSManager>();
            }

            if (ttsManager != null)
            {
                LoggerManager.Debug("已找到 RKTTSManager", "SenseOnnx");
            }
        }

        // 自动获取 STT 管理器
        if (enableSTT && sttManager == null)
        {
            sttManager = GetComponent<MonoBehaviour>();
            if (sttManager == null)
            {
                sttManager = FindObjectOfType<MonoBehaviour>();
            }

            if (sttManager != null)
            {
                LoggerManager.Debug("已找到 STT Manager", "SenseOnnx");
            }
        }

        LoggerManager.Debug("单例设置完成", "SenseOnnx");

        // 延迟加载模式：等待 SDKLoader 启用
        enabled = false;
        LoggerManager.Debug("已禁用，等待 SDKLoader 延迟加载", "SenseOnnx");
    }

    void OnEnable()
    {
        LoggerManager.Debug("OnEnable() 被调用 - 开始初始化", "SenseOnnx");
        StartCoroutine(InitializeSenseOnnx());
    }

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化 SenseOnnx 管理器
    /// </summary>
    private IEnumerator InitializeSenseOnnx()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        LoggerManager.Info("开始初始化 SenseOnnx 管理器", "SenseOnnx");

        // 1. 等待权限授予
        LoggerManager.Debug("[1/6] 等待权限授予...", "SenseOnnx");
        yield return StartCoroutine(WaitForPermissions());

        // 2. 获取 Unity Activity
        LoggerManager.Debug("[2/6] 获取 Unity Activity...", "SenseOnnx");
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            if (unityActivity == null)
            {
                LoggerManager.Error("无法获取 Unity Activity", "SenseOnnx");
                OnInitializationError?.Invoke("无法获取 Unity Activity");
                yield break;
            }
            LoggerManager.Debug("[2/6] ✅ Unity Activity 获取成功", "SenseOnnx");
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"获取 Unity Activity 失败: {e.Message}", "SenseOnnx");
            OnInitializationError?.Invoke($"获取 Unity Activity 失败: {e.Message}");
            yield break;
        }

        // 3. 初始化 SenseOnnx 单例
        LoggerManager.Debug("[3/6] 初始化 SenseOnnx 单例...", "SenseOnnx");
        try
        {
            using (AndroidJavaClass senseOnnxClass = new AndroidJavaClass("com.sensetime.senseonnx.SenseOnnx"))
            {
                // 尝试 1: getInstance() 无参
                try 
                {
                    senseOnnxInstance = senseOnnxClass.CallStatic<AndroidJavaObject>("getInstance");
                }
                catch (Exception) { senseOnnxInstance = null; }

                // 尝试 2: getInstance(Context)
                if (senseOnnxInstance == null)
                {
                    try
                    {
                        senseOnnxInstance = senseOnnxClass.CallStatic<AndroidJavaObject>("getInstance", unityActivity);
                    }
                    catch (Exception) { senseOnnxInstance = null; }
                }

                // 尝试 3: 构造函数 new SenseOnnx(Context)
                if (senseOnnxInstance == null)
                {
                    try
                    {
                        senseOnnxInstance = new AndroidJavaObject("com.sensetime.senseonnx.SenseOnnx", unityActivity);
                    }
                    catch (Exception) { senseOnnxInstance = null; }
                }

                 // 尝试 4: 构造函数 new SenseOnnx()
                if (senseOnnxInstance == null)
                {
                    try
                    {
                        senseOnnxInstance = new AndroidJavaObject("com.sensetime.senseonnx.SenseOnnx");
                    }
                    catch (Exception) { senseOnnxInstance = null; }
                }

                if (senseOnnxInstance != null)
                {
                    // 有些版本可能需要显式 initialize，有些在构造函数或 getInstance 中已做
                    // 尝试调用 initialize，如果失败则忽略(可能不需要)
                    try 
                    {
                        senseOnnxInstance.Call("initialize", unityActivity);
                        LoggerManager.Debug("[3/6] 调用 initialize 成功", "SenseOnnx");
                    }
                    catch (Exception e) 
                    {
                        LoggerManager.Debug($"[3/6] 调用 initialize 异常 (可能不需要): {e.Message}", "SenseOnnx");
                    }
                    
                    LoggerManager.Debug("[3/6] ✅ SenseOnnx 单例初始化成功", "SenseOnnx");
                }
                else
                {
                    LoggerManager.Error("[3/6] SenseOnnx 单例获取失败 (尝试了 getInstance(), getInstance(ctx), new SenseOnnx(ctx), new SenseOnnx())", "SenseOnnx");
                    OnInitializationError?.Invoke("SenseOnnx 单例获取失败");
                    yield break;
                }
            }
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"SenseOnnx 单例初始化失败: {e.Message}", "SenseOnnx");
            OnInitializationError?.Invoke($"SenseOnnx 单例初始化失败: {e.Message}");
            yield break;
        }

        // 4. 获取各个能力实例
        LoggerManager.Debug("[4/6] 获取能力实例...", "SenseOnnx");
        yield return StartCoroutine(InitializeAbilities());

        // 5. 初始化 AudioTrack (用于 TTS 播放)
        if (enableOnnxTTS && isOnnxTTSReady)
        {
            LoggerManager.Debug("[5/6] 初始化 AudioTrack...", "SenseOnnx");
            yield return StartCoroutine(InitializeAudioTrack());
        }
        else
        {
            LoggerManager.Debug($"[5/6] AudioTrack 初始化已跳过 (enableOnnxTTS: {enableOnnxTTS}, isOnnxTTSReady: {isOnnxTTSReady})", "SenseOnnx");
        }

        // 6. 设置回调监听器
        LoggerManager.Debug("[6/6] 设置回调监听器...", "SenseOnnx");
        SetupDataCallbackListeners();

        // 7. 输出能力状态日志
        LogCapabilitiesStatus();

        isInitialized = true;
        LoggerManager.Info("✅ SenseOnnx 管理器初始化完成", "SenseOnnx");

        OnSenseOnnxInitialized?.Invoke();

#else
        LoggerManager.Warning("当前平台不支持 SenseOnnx (仅支持 Android)", "SenseOnnx");
        yield return null;
#endif
    }

    /// <summary>
    /// 等待权限授予
    /// </summary>
    private IEnumerator WaitForPermissions()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        float timeout = 10f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            bool hasStorage = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.ExternalStorageRead
            );
            bool hasAudio = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone
            );

            if (hasStorage && hasAudio)
            {
                LoggerManager.Debug("所有权限已授予", "SenseOnnx");
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        LoggerManager.Warning("权限授予超时，尝试继续初始化", "SenseOnnx");
#endif
        yield return null;
    }

    /// <summary>
    /// 等待 TTS 初始化完成
    /// </summary>
    private IEnumerator WaitForTTSReady()
    {
        float timeout = 15f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            if (ttsManager != null && ttsManager.IsInitialized())
            {
                isTTSReady = true;
                LoggerManager.Debug("✅ TTS 已就绪", "SenseOnnx");
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        LoggerManager.Warning("TTS 初始化超时", "SenseOnnx");
    }

    #endregion

    #region 公共 API - 状态查询

    /// <summary>
    /// 检查是否已初始化
    /// </summary>
    public bool IsInitialized()
    {
        return isInitialized;
    }

    /// <summary>
    /// 检查 TTS 是否就绪
    /// </summary>
    public bool IsTTSReady()
    {
        return isTTSReady && ttsManager != null && ttsManager.IsInitialized();
    }

    /// <summary>
    /// 检查 STT 是否就绪
    /// </summary>
    public bool IsSTTReady()
    {
        return isSTTReady && sttManager != null;
    }

    /// <summary>
    /// 获取 TTS 管理器引用
    /// </summary>
    public RKTTSManager GetTTSManager()
    {
        return ttsManager;
    }

    /// <summary>
    /// 获取 STT 管理器引用
    /// </summary>
    public MonoBehaviour GetSTTManager()
    {
        return sttManager;
    }

    #endregion

    #region 内部 API - Android 对象访问器 (供回调使用)

#if UNITY_ANDROID && !UNITY_EDITOR
    /// <summary>
    /// 获取 AudioTrack 对象 (内部使用)
    /// </summary>
    internal AndroidJavaObject GetAudioTrack()
    {
        return audioTrack;
    }

    /// <summary>
    /// 获取 KwsAbility 对象 (内部使用)
    /// </summary>
    internal AndroidJavaObject GetKwsAbility()
    {
        return kwsAbility;
    }

    /// <summary>
    /// 获取 SttAbility 对象 (内部使用)
    /// </summary>
    internal AndroidJavaObject GetSttAbility()
    {
        return sttAbility;
    }
#endif

    #endregion

    #region 内部 API - 事件触发器 (供回调使用)

    /// <summary>
    /// 触发 STT 结果事件 (内部使用)
    /// </summary>
    internal void RaiseSttResultEvent(string text)
    {
        OnSttResult?.Invoke(text);
    }

    /// <summary>
    /// 触发 KWS 检测事件 (内部使用)
    /// </summary>
    internal void RaiseKwsDetectedEvent(string keyword)
    {
        OnKwsDetected?.Invoke(keyword);
    }

    /// <summary>
    /// 触发 TTS 音频块事件 (内部使用)
    /// </summary>
    internal void RaiseTtsAudioChunkEvent(float[] audioData)
    {
        OnTtsAudioChunk?.Invoke(audioData);
    }

    #endregion

    #region 公共 API - TTS 接口

    /// <summary>
    /// 文字转语音
    /// </summary>
    public void Speak(string text)
    {
        if (!IsTTSReady())
        {
            LoggerManager.Warning("TTS 未就绪", "SenseOnnx");
            return;
        }

        ttsManager.Speak(text);
    }

    /// <summary>
    /// 停止 TTS 播放
    /// </summary>
    public void StopTTS()
    {
        if (ttsManager != null && ttsManager.IsPlaying())
        {
            ttsManager.Stop();
        }
    }

    /// <summary>
    /// 检查 TTS 是否正在播放
    /// </summary>
    public bool IsTTSPlaying()
    {
        return ttsManager != null && ttsManager.IsPlaying();
    }

    #endregion

    #region Onnx Abilities 初始化

    /// <summary>
    /// 初始化各个能力实例
    /// </summary>
    private IEnumerator InitializeAbilities()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 辅助方法：尝试多种方式获取单例
        AndroidJavaObject GetAbilityInstance(string className, string abilityName)
        {
            AndroidJavaObject instance = null;
            try
            {
                using (AndroidJavaClass cls = new AndroidJavaClass(className))
                {
                    // 策略 1: getInstance()
                    try { instance = cls.CallStatic<AndroidJavaObject>("getInstance"); }
                    catch (Exception) { }

                    // 策略 2: Kotlin Object INSTANCE 字段
                    if (instance == null)
                    {
                        try { instance = cls.GetStatic<AndroidJavaObject>("INSTANCE"); }
                        catch (Exception) { }
                    }

                    // 策略 3: Companion.getInstance()
                    if (instance == null)
                    {
                        try 
                        {
                            using (AndroidJavaObject companion = cls.GetStatic<AndroidJavaObject>("Companion"))
                            {
                                if (companion != null) instance = companion.Call<AndroidJavaObject>("getInstance");
                            }
                        }
                        catch (Exception) { }
                    }

                    // 策略 4: new className()
                    if (instance == null)
                    {
                        try { instance = new AndroidJavaObject(className); }
                        catch (Exception) { }
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerManager.Warning($"[API适配] 无法加载类 {abilityName} ({className}): {ex.Message}", "SenseOnnx");
            }

            if (instance != null)
                LoggerManager.Debug($"✅ {abilityName} 获取成功", "SenseOnnx");
            else
                LoggerManager.Warning($"❌ {abilityName} 获取失败 (尝试了 getInstance/INSTANCE/Companion/new)", "SenseOnnx");

            return instance;
        }

        try
        {
            // 确保使用正确的包名 com.sensetime
            string pkgPrefix = "com.sensetime.senseonnx";

            // 获取 RecordAbility 实例
            if (enableSTT)
            {
                LoggerManager.Debug("获取 RecordAbility 实例...", "SenseOnnx");
                recordAbility = GetAbilityInstance($"{pkgPrefix}.audio.RecordAbility", "RecordAbility");
                if (recordAbility != null) isRecordReady = true;
            }

            // 获取 TtsAbility 实例
            if (enableOnnxTTS)
            {
                LoggerManager.Debug("获取 TtsAbility 实例...", "SenseOnnx");
                ttsAbility = GetAbilityInstance($"{pkgPrefix}.tts.TtsAbility", "TtsAbility");
                LoggerManager.Debug($"TtsAbility 获取结果: {ttsAbility != null}", "SenseOnnx");
                if (ttsAbility != null) isOnnxTTSReady = true;
            }

            // 获取 SttAbility 实例
            if (enableSTT)
            {
                LoggerManager.Debug("获取 SttAbility 实例...", "SenseOnnx");
                sttAbility = GetAbilityInstance($"{pkgPrefix}.stt.SttAbility", "SttAbility");
                if (sttAbility != null) isOnnxSTTReady = true; // 修正为 OnnxSTT
            }

            // 获取 KwsAbility 实例
            if (enableSTT)
            {
                LoggerManager.Debug("获取 KwsAbility 实例...", "SenseOnnx");
                kwsAbility = GetAbilityInstance($"{pkgPrefix}.kws.KwsAbility", "KwsAbility");
                if (kwsAbility != null) isKwsReady = true;
            }

            LoggerManager.Info("✅ 所有能力实例获取流程完成", "SenseOnnx");
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"获取能力实例失败: {e.Message}", "SenseOnnx");
        }
#endif
        yield return null;
    }

    /// <summary>
    /// 初始化 AudioTrack (用于 TTS 播放)
    /// </summary>
    private IEnumerator InitializeAudioTrack()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (ttsAbility == null)
            {
                LoggerManager.Warning("TtsAbility 未就绪，无法初始化 AudioTrack", "SenseOnnx");
                yield break;
            }

            // 获取采样率
            int sampleRate = ttsAbility.Call<int>("getSampleRate");
            ttsSampleRate = sampleRate; // 存储采样率
            LoggerManager.Debug($"TTS 采样率: {sampleRate}", "SenseOnnx");

            // 获取最小缓冲区大小
            using (AndroidJavaClass audioTrackClass = new AndroidJavaClass("android.media.AudioTrack"))
            using (AndroidJavaClass audioFormatClass = new AndroidJavaClass("android.media.AudioFormat"))
            {
                int channelConfig = audioFormatClass.GetStatic<int>("CHANNEL_OUT_MONO");
                int audioFormat = audioFormatClass.GetStatic<int>("ENCODING_PCM_FLOAT");
                
                int bufferSize = audioTrackClass.CallStatic<int>("getMinBufferSize", 
                    sampleRate, channelConfig, audioFormat);
                
                LoggerManager.Debug($"AudioTrack 缓冲区大小: {bufferSize}", "SenseOnnx");

                // 创建 AudioAttributes
                using (AndroidJavaObject audioAttributesBuilder = new AndroidJavaObject("android.media.AudioAttributes$Builder"))
                using (AndroidJavaClass audioAttributesClass = new AndroidJavaClass("android.media.AudioAttributes"))
                {
                    int contentTypeSpeech = audioAttributesClass.GetStatic<int>("CONTENT_TYPE_SPEECH");
                    int usageMedia = audioAttributesClass.GetStatic<int>("USAGE_MEDIA");
                    
                    AndroidJavaObject audioAttributes = audioAttributesBuilder
                        .Call<AndroidJavaObject>("setContentType", contentTypeSpeech)
                        .Call<AndroidJavaObject>("setUsage", usageMedia)
                        .Call<AndroidJavaObject>("build");

                    // 创建 AudioFormat
                    using (AndroidJavaObject audioFormatBuilder = new AndroidJavaObject("android.media.AudioFormat$Builder"))
                    {
                        AndroidJavaObject format = audioFormatBuilder
                            .Call<AndroidJavaObject>("setEncoding", audioFormat)
                            .Call<AndroidJavaObject>("setChannelMask", channelConfig)
                            .Call<AndroidJavaObject>("setSampleRate", sampleRate)
                            .Call<AndroidJavaObject>("build");

                        // 创建 AudioTrack
                        using (AndroidJavaClass audioManagerClass = new AndroidJavaClass("android.media.AudioManager"))
                        {
                            int modeStream = audioTrackClass.GetStatic<int>("MODE_STREAM");
                            int sessionIdGenerate = audioManagerClass.GetStatic<int>("AUDIO_SESSION_ID_GENERATE");

                            audioTrack = new AndroidJavaObject("android.media.AudioTrack",
                                audioAttributes, format, bufferSize, modeStream, sessionIdGenerate);

                            if (audioTrack != null)
                            {
                                audioTrack.Call("play");
                                LoggerManager.Info("✅ AudioTrack 初始化成功并开始播放", "SenseOnnx");
                            }
                            else
                            {
                                LoggerManager.Error("AudioTrack 创建失败", "SenseOnnx");
                            }
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"AudioTrack 初始化失败: {e.Message}", "SenseOnnx");
        }
#endif
        yield return null;
    }

    /// <summary>
    /// 设置数据回调监听器
    /// </summary>
    public void SetupDataCallbackListeners()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        LoggerManager.Debug($"isOnnxTTSReady: {isOnnxTTSReady}, ttsAbility: {ttsAbility}", "SenseOnnx");
        // 设置 TTS 回调监听器
        if (isOnnxTTSReady && ttsAbility != null)
        {
            ttsDataCallback = new TtsDataCallback(this);
            try
            {
                ttsProxy = new TtsCallbackProxy(ttsDataCallback);
                ttsAbility.Call("setDataCallbackListener", ttsProxy);
                LoggerManager.Debug("✅ TTS 回调监听器已设置", "SenseOnnx");
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"设置 TTS 回调失败: {e.Message}", "SenseOnnx");
            }
        }

        // 设置 STT 回调监听器
        if (isOnnxSTTReady && sttAbility != null)
        {
            sttDataCallback = new SttDataCallback(this);
            try
            {
                sttProxy = new SttCallbackProxy(sttDataCallback);
                sttAbility.Call("setDataCallbackListener", sttProxy);
                LoggerManager.Debug("✅ STT 回调监听器已设置", "SenseOnnx");
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"设置 STT 回调失败: {e.Message}", "SenseOnnx");
            }
        }

        // 设置 KWS 回调监听器
        if (isKwsReady && kwsAbility != null)
        {
            kwsDataCallback = new KwsDataCallback(this);
            try
            {
                kwsProxy = new KwsCallbackProxy(kwsDataCallback);
                kwsAbility.Call("setDataCallbackListener", kwsProxy);
                LoggerManager.Debug("✅ KWS 回调监听器已设置", "SenseOnnx");
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"设置 KWS 回调失败: {e.Message}", "SenseOnnx");
            }
        }

        // 设置 Record 回调监听器
        if (isRecordReady && recordAbility != null)
        {
            recordDataCallback = new RecordDataCallback(this);
            try
            {
                recordProxy = new RecordCallbackProxy(recordDataCallback);
                recordAbility.Call("setDataCallbackListener", recordProxy);
                LoggerManager.Debug("✅ Record 回调监听器已设置", "SenseOnnx");
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"设置 Record 回调失败: {e.Message}", "SenseOnnx");
            }
        }
#endif
    }

    #endregion

    #region 公共 API - TTS 接口

    /// <summary>
    /// TTS 文字转语音 (使用 TtsAbility)
    /// </summary>
    public void TtsGenerate(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        LoggerManager.Debug($"ttsAbility: {ttsAbility}", "SenseOnnx");
        LoggerManager.Debug($"isOnnxTTSReady: {isOnnxTTSReady}", "SenseOnnx");
        LoggerManager.Debug($"audioTrack: {audioTrack}", "SenseOnnx");
        
        if (!isOnnxTTSReady || ttsAbility == null)
        {
            LoggerManager.Warning("TTS 未就绪", "SenseOnnx");
            return;
        }

        try
        {
            LoggerManager.Debug($"TTS 生成: {text}", "SenseOnnx");
            
            // 暂停、清空并重新播放 AudioTrack
            if (audioTrack != null)
            {
                LoggerManager.Debug("重置内部 AudioTrack", "SenseOnnx");
                audioTrack.Call("pause");
                audioTrack.Call("flush");
                audioTrack.Call("play");
            }
            
            if (ttsAbility != null)
            {
                LoggerManager.Debug("调用 ttsAbility.inputData(text)", "SenseOnnx");
                ttsAbility.Call("inputData", text);
            }
            else
            {
                LoggerManager.Error("ttsAbility 在调用 inputData 前变为 null", "SenseOnnx");
            }
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"TTS 生成失败: {e.Message}", "SenseOnnx");
        }
#else
        LoggerManager.Warning($"[模拟] TTS 生成: {text}", "SenseOnnx");
#endif
    }



    /// <summary>
    /// 获取 TTS 采样率
    /// </summary>
    public int GetTtsSampleRate()
    {
        return ttsSampleRate;
    }

    /// <summary>
    /// 检查 TTS Ability 是否就绪
    /// </summary>
    public bool IsTtsAbilityReady()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        return isOnnxTTSReady && ttsAbility != null;
#else
        return false;
#endif
    }

    #endregion

    #region 公共 API - STT 接口

    /// <summary>
    /// STT 开始识别 (使用 SttAbility)
    /// </summary>
    public void SttStartRecognition()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isSTTReady || sttAbility == null)
        {
            LoggerManager.Warning("STT 未就绪", "SenseOnnx");
            return;
        }

        try
        {
            LoggerManager.Debug("STT 开始识别", "SenseOnnx");
            // STT 通过 Record 的回调自动接收数据
            // 这里可以设置 STT 的状态或参数
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"STT 开始识别失败: {e.Message}", "SenseOnnx");
        }
#else
        LoggerManager.Warning("[模拟] STT 开始识别", "SenseOnnx");
#endif
    }

    /// <summary>
    /// 检查 STT 是否就绪
    /// </summary>
    public bool IsSttAbilityReady()
    {
        return isSTTReady;
    }

    #endregion

    #region 公共 API - KWS 接口

    /// <summary>
    /// 设置 KWS 唤醒词开关
    /// </summary>
    public void SetKwsSwitch(bool enabled)
    {
        kwsSwitch = enabled;
        LoggerManager.Debug($"KWS 开关设置为: {enabled}", "SenseOnnx");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (senseOnnxInstance != null)
        {
            try
            {
                senseOnnxInstance.Call("setKwsSwitch", enabled);
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"设置 KWS 开关失败: {e.Message}", "SenseOnnx");
            }
        }
#endif
    }

    /// <summary>
    /// 获取 KWS 唤醒词开关状态
    /// </summary>
    public bool GetKwsSwitch()
    {
        return kwsSwitch;
    }

    /// <summary>
    /// 设置唤醒状态
    /// </summary>
    public void SetWakeup(bool isWakeup)
    {
        wakeup = isWakeup;
        LoggerManager.Debug($"唤醒状态设置为: {isWakeup}", "SenseOnnx");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        if (senseOnnxInstance != null)
        {
            try
            {
                senseOnnxInstance.Call("setWakeup", isWakeup);
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"设置唤醒状态失败: {e.Message}", "SenseOnnx");
            }
        }
#endif
    }

    /// <summary>
    /// 获取唤醒状态
    /// </summary>
    public bool GetWakeup()
    {
        return wakeup;
    }

    /// <summary>
    /// 检查 KWS 是否就绪
    /// </summary>
    public bool IsKwsReady()
    {
        return isKwsReady;
    }

    #endregion

    #region 公共 API - Record 接口

    /// <summary>
    /// 开始录音
    /// </summary>
    public void StartRecord()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isRecordReady || recordAbility == null)
        {
            LoggerManager.Warning("Record 未就绪", "SenseOnnx");
            return;
        }

        try
        {
            LoggerManager.Debug("开始录音", "SenseOnnx");
            recordAbility.Call("start");
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"开始录音失败: {e.Message}", "SenseOnnx");
        }
#else
        LoggerManager.Warning("[模拟] 开始录音", "SenseOnnx");
#endif
    }

    /// <summary>
    /// 停止录音
    /// </summary>
    public void StopRecord()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isRecordReady || recordAbility == null)
        {
            LoggerManager.Warning("Record 未就绪", "SenseOnnx");
            return;
        }

        try
        {
            LoggerManager.Debug("停止录音", "SenseOnnx");
            recordAbility.Call("stop");
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"停止录音失败: {e.Message}", "SenseOnnx");
        }
#else
        LoggerManager.Warning("[模拟] 停止录音", "SenseOnnx");
#endif
    }

    /// <summary>
    /// 检查 Record 是否就绪
    /// </summary>
    public bool IsRecordReady()
    {
        return isRecordReady;
    }

    #endregion

    #region 公共 API - 业务逻辑接口

    /// <summary>
    /// 处理对话流程：文本 → LLM → TTS
    /// </summary>
    public void ProcessConversation(string message)
    {
        if (isProcessing)
        {
            LoggerManager.Warning("正在处理中，请稍候", "SenseOnnx");
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            LoggerManager.Warning("消息不能为空", "SenseOnnx");
            return;
        }

        isProcessing = true;
        LoggerManager.Info($"开始对话流程: {message}", "SenseOnnx");

        // TODO: 实现完整的对话流程
        // 1. 发送到 LLM
        // 2. 获取 LLM 响应
        // 3. 将响应转为语音

        // 目前简化实现：直接将消息转为语音
        if (IsTtsAbilityReady())
        {
            TtsGenerate(message);
        }
        else if (IsTTSReady())
        {
            Speak(message);
        }

        // 模拟响应
        OnConversationResponse?.Invoke(message);

        isProcessing = false;
    }

    /// <summary>
    /// 发送消息到 LLM
    /// </summary>
    public void SendToLLM(string message)
    {
        if (string.IsNullOrEmpty(message))
        {
            LoggerManager.Warning("消息不能为空", "SenseOnnx");
            return;
        }

        LoggerManager.Info($"发送到 LLM: {message}", "SenseOnnx");

        // TODO: 实现 LLM 调用逻辑
        // 目前模拟响应
        string response = $"收到消息: {message}";
        OnConversationResponse?.Invoke(response);
    }

    /// <summary>
    /// 停止所有操作
    /// </summary>
    public void Stop()
    {
        LoggerManager.Info("停止所有操作", "SenseOnnx");

        // 停止 RK TTS
        if (IsTTSReady())
        {
            StopTTS();
        }

        // 停止录音
        if (IsRecordReady())
        {
            StopRecord();
        }

        isProcessing = false;
    }

    /// <summary>
    /// 检查是否正在处理
    /// </summary>
    public bool IsProcessing()
    {
        return isProcessing;
    }

    /// <summary>
    /// 获取状态信息
    /// </summary>
    public string GetStatusInfo()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== SenseOnnx 管理器状态 ===");
        sb.AppendLine($"已初始化: {isInitialized}");
        sb.AppendLine($"正在处理: {isProcessing}");
        sb.AppendLine();
        sb.AppendLine("--- RK TTS ---");
        sb.AppendLine($"已启用: {enableTTS}");
        sb.AppendLine($"已就绪: {IsTTSReady()}");
        sb.AppendLine($"正在播放: {IsTTSPlaying()}");
        sb.AppendLine();
        sb.AppendLine("--- TTS Ability ---");
        sb.AppendLine($"已启用: {enableOnnxTTS}");
        sb.AppendLine($"已就绪: {IsTtsAbilityReady()}");
        sb.AppendLine();
        sb.AppendLine("--- STT ---");
        sb.AppendLine($"已启用: {enableSTT}");
        sb.AppendLine($"已就绪: {IsSTTReady()}");
        sb.AppendLine();
        sb.AppendLine("--- STT Ability ---");
        sb.AppendLine($"已启用: {enableSTT}");
        sb.AppendLine($"已就绪: {IsSttAbilityReady()}");

        return sb.ToString();
    }

    #endregion

    /// <summary>
    /// 输出能力状态日志
    /// </summary>
    private void LogCapabilitiesStatus()
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("=== SenseOnnx Capabilities Status ===");
        sb.AppendLine($"SenseOnnx Init: {isInitialized}");
        sb.AppendLine($"TTS (Unity): {(IsTTSReady() ? "Ready" : "Not Ready")}");
        sb.AppendLine($"Onnx TTS: {(isOnnxTTSReady ? "Ready" : "Not Ready")}");
        sb.AppendLine($"Onnx STT: {(isOnnxSTTReady ? "Ready" : "Not Ready")}");
        sb.AppendLine($"Record: {(isRecordReady ? "Ready" : "Not Ready")}");
        sb.AppendLine($"KWS: {(isKwsReady ? "Ready" : "Not Ready")}");
        sb.AppendLine("=====================================");

        LoggerManager.Info(sb.ToString(), "SenseOnnx");
    }

    #region 公共 API - STT 接口（预留）

    // TODO: 添加 STT 相关接口

    /// <summary>
    /// 触发 TTS 数据接收事件
    /// </summary>
    public void DispatchTtsData(float[] data)
    {
        int subscriberCount = OnTtsAudioDataRecevied?.GetInvocationList()?.Length ?? 0;
        if (data != null && data.Length > 0)
        {
            LoggerManager.Debug($"[SenseOnnxManager] DispatchTtsData: {data.Length} 个采样, 订阅者: {subscriberCount}", "SenseOnnx");
        }
        else
        {
             LoggerManager.Warning($"[SenseOnnxManager] DispatchTtsData 收到空数据", "SenseOnnx");
        }
        OnTtsAudioDataRecevied?.Invoke(data);
    }

    #endregion
}

#region TTS 回调实现

/// <summary>
/// TTS 数据回调实现
/// </summary>
public class TtsDataCallback : DataCallbackListener<float[]>
{
    private SenseOnnxManager manager;

    public TtsDataCallback(SenseOnnxManager manager)
    {
        this.manager = manager;
    }

    public void OnDataChunkCallback(float[] data)
    {
        LoggerManager.Debug($"[TtsDataCallback] OnDataChunkCallback: {data?.Length ?? 0} 个采样", "SenseOnnx");
        
        if (manager != null)
        {
            manager.DispatchTtsData(data);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        // 将音频数据写入 AudioTrack 播放
        if (manager != null && manager.useInternalAudioPlayer && data != null && data.Length > 0)
        {
            try
            {
                AndroidJavaObject audioTrack = manager.GetAudioTrack();
                if (audioTrack != null)
                {
                    using (AndroidJavaClass audioTrackClass = new AndroidJavaClass("android.media.AudioTrack"))
                    {
                        int writeBlocking = audioTrackClass.GetStatic<int>("WRITE_BLOCKING");
                        audioTrack.Call<int>("write", data, 0, data.Length, writeBlocking);
                    }
                }
            }
            catch (System.Exception e)
            {
                LoggerManager.Error($"写入 AudioTrack 失败: {e.Message}", "SenseOnnx");
            }
        }
#endif
        
        // 触发事件
        if (manager != null)
        {
            manager.RaiseTtsAudioChunkEvent(data);
        }
    }

    public void OnDataFinishCallback(float[] data)
    {
        LoggerManager.Debug($"TTS 数据完成回调: {data?.Length ?? 0} 个采样", "SenseOnnx");
        
#if UNITY_ANDROID && !UNITY_EDITOR
        // 停止 AudioTrack
        if (manager != null)
        {
            try
            {
                AndroidJavaObject audioTrack = manager.GetAudioTrack();
                if (audioTrack != null)
                {
                    audioTrack.Call("stop");
                }
            }
            catch (System.Exception e)
            {
                LoggerManager.Error($"停止 AudioTrack 失败: {e.Message}", "SenseOnnx");
            }
        }
#endif
    }
}

/// <summary>
/// TTS Android 回调代理
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class TtsCallbackProxy : AndroidJavaProxy
{
    private DataCallbackListener<float[]> callback;

    public TtsCallbackProxy(DataCallbackListener<float[]> callback)
        : base("com.sensetime.senseonnx.DataCallbackListener")
    {
        this.callback = callback;
    }

    // Android 回调方法
    public void onChunk(float[] data)
    {
        LoggerManager.Debug($"[TtsCallbackProxy] onChunk hit! length: {data?.Length ?? 0}", "SenseOnnx");
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataChunkCallback(data);
        });
    }

    public void onFinish(float[] data)
    {
        LoggerManager.Debug($"[TtsCallbackProxy] onFinish hit! length: {data?.Length ?? 0}", "SenseOnnx");
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataFinishCallback(data);
        });
    }

    public override AndroidJavaObject Invoke(string methodName, object[] args)
    {
        // 捕获所有可能的 JNI 调用
        string argsInfo = "";
        if (args != null)
        {
            argsInfo = $" with {args.Length} args";
            for (int i = 0; i < args.Length; i++)
            {
                argsInfo += $", arg[{i}]: {(args[i] != null ? args[i].GetType().Name : "null")}";
            }
        }
        LoggerManager.Debug($"[TtsCallbackProxy] Invoke: {methodName} called{argsInfo}", "SenseOnnx");

        // 如果是 onChunk 或 onFinish 以外的名称，尝试手动匹配
        if (methodName == "onDataChunk" || methodName == "OnChunk" || methodName == "onData")
        {
             LoggerManager.Info($"[TtsCallbackProxy] 匹配到别名方法: {methodName} -> onChunk", "SenseOnnx");
             if (args != null && args.Length > 0 && args[0] is float[] data)
             {
                 onChunk(data);
                 return null;
             }
        }
        
        return base.Invoke(methodName, args);
    }
}
#endif

#endregion

#region STT 回调实现

/// <summary>
/// STT 数据回调实现
/// </summary>
public class SttDataCallback : DataCallbackListener<string>
{
    private SenseOnnxManager manager;

    public SttDataCallback(SenseOnnxManager manager)
    {
        this.manager = manager;
    }

    public void OnDataChunkCallback(string data)
    {
        LoggerManager.Debug($"STT 数据块回调: {data}", "SenseOnnx");
        // 触发事件 - 中间识别结果
        if (manager != null)
        {
            manager.RaiseSttResultEvent(data);
        }
    }

    public void OnDataFinishCallback(string data)
    {
        LoggerManager.Debug($"STT 数据完成回调: {data}", "SenseOnnx");

        // 触发事件 - 最终识别结果
        if (manager != null)
        {
            manager.RaiseSttResultEvent(data);
        }
        
        // 将识别结果传给 TTS 进行语音合成 (如果需要)
        if (manager != null && manager.IsTtsAbilityReady() && !string.IsNullOrEmpty(data))
        {
            manager.TtsGenerate(data);
        }
    }
}

/// <summary>
/// STT Android 回调代理
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class SttCallbackProxy : AndroidJavaProxy
{
    private DataCallbackListener<string> callback;

    public SttCallbackProxy(DataCallbackListener<string> callback)
        : base("com.sensetime.senseonnx.DataCallbackListener")
    {
        this.callback = callback;
    }

    // Android 回调方法
    public void onChunk(string data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataChunkCallback(data);
        });
    }

    public void onFinish(string data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataFinishCallback(data);
        });
    }
}
#endif

#endregion

#region KWS 回调实现

/// <summary>
/// KWS 数据回调实现
/// </summary>
public class KwsDataCallback : DataCallbackListener<string>
{
    private SenseOnnxManager manager;

    public KwsDataCallback(SenseOnnxManager manager)
    {
        this.manager = manager;
    }

    public void OnDataChunkCallback(string data)
    {
        // KWS 通常只在检测到唤醒词时触发
    }

    public void OnDataFinishCallback(string data)
    {
        LoggerManager.Info($"KWS 检测到唤醒词: {data}", "SenseOnnx");
        
        // 设置唤醒状态
        if (manager != null)
        {
            manager.SetWakeup(true);
        }
        
        // 触发事件
        if (manager != null)
        {
            manager.RaiseKwsDetectedEvent(data);
        }
    }
}

/// <summary>
/// KWS Android 回调代理
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class KwsCallbackProxy : AndroidJavaProxy
{
    private DataCallbackListener<string> callback;

    public KwsCallbackProxy(DataCallbackListener<string> callback)
        : base("com.sensetime.senseonnx.DataCallbackListener")
    {
        this.callback = callback;
    }

    // Android 回调方法
    public void onChunk(string data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataChunkCallback(data);
        });
    }

    public void onFinish(string data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataFinishCallback(data);
        });
    }
}
#endif

#endregion

#region Record 回调实现

/// <summary>
/// Record 数据回调实现
/// </summary>
public class RecordDataCallback : DataCallbackListener<float[]>
{
    private SenseOnnxManager manager;

    public RecordDataCallback(SenseOnnxManager manager)
    {
        this.manager = manager;
    }

    public void OnDataChunkCallback(float[] data)
    {
        // 根据 Android demo 的逻辑:
        // 如果 KWS 开关开启且未唤醒，则将音频数据传给 KWS 进行唤醒词检测
        if (manager != null && data != null && data.Length > 0)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (manager.GetKwsSwitch() && !manager.GetWakeup())
            {
                try
                {
                    AndroidJavaObject kwsAbility = manager.GetKwsAbility();
                    if (kwsAbility != null)
                    {
                        kwsAbility.Call("inputData", data);
                    }
                }
                catch (System.Exception e)
                {
                    LoggerManager.Error($"KWS inputData 失败: {e.Message}", "SenseOnnx");
                }
            }
            
            // 如果已唤醒，可以将数据传给 STT 进行语音识别
            if (manager.GetWakeup())
            {
                try
                {
                    AndroidJavaObject sttAbility = manager.GetSttAbility();
                    if (sttAbility != null)
                    {
                        sttAbility.Call("inputData", data);
                    }
                }
                catch (System.Exception e)
                {
                    LoggerManager.Error($"STT inputData 失败: {e.Message}", "SenseOnnx");
                }
            }
#endif
        }
    }

    public void OnDataFinishCallback(float[] data)
    {
        // Record 通常是持续录音，不需要处理 finish 回调
        // 如果需要，可以在这里添加录音结束的处理逻辑
    }
}

/// <summary>
/// Record Android 回调代理
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class RecordCallbackProxy : AndroidJavaProxy
{
    private DataCallbackListener<float[]> callback;

    public RecordCallbackProxy(DataCallbackListener<float[]> callback)
        : base("com.sensetime.senseonnx.DataCallbackListener")
    {
        this.callback = callback;
    }

    // Android 回调方法
    public void onChunk(float[] data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataChunkCallback(data);
        });
    }

    public void onFinish(float[] data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataFinishCallback(data);
        });
    }
}
#endif

#endregion
