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
    public bool enableOnnxTTS = false;

    [Header("STT 配置")]
    [Tooltip("是否自动启用 STT")]
    public bool enableSTT = false;

    [Tooltip("STT 管理器引用（如果有）")]
    public MonoBehaviour sttManager;  // 预留 STT 管理器位置

    #endregion

    #region Android JNI 对象

#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject onnxTtsDetector;  // Onnx TTS 检测器
    private AndroidJavaObject onnxSttDetector;  // Onnx STT 检测器
    private AndroidJavaObject unityActivity;
#endif

    #endregion

    #region 状态变量

    private bool isInitialized = false;
    private bool isTTSReady = false;
    private bool isSTTReady = false;
    private bool isOnnxTTSReady = false;
    private bool isOnnxSTTReady = false;

    #endregion

    #region 回调监听器

    // Onnx TTS 数据回调监听器
    private DataCallbackListener<float[]> onnxTtsDataCallback;

    // Onnx STT 数据回调监听器
    private DataCallbackListener<string> onnxSttDataCallback;

    #endregion

    #region 事件

    public event Action OnSenseOnnxInitialized;
    public event Action<string> OnInitializationError;
    public event Action<string> OnConversationResponse;

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
        LoggerManager.Debug("[1/5] 等待权限授予...", "SenseOnnx");
        yield return StartCoroutine(WaitForPermissions());

        // 2. 初始化 TTS (RKTTSManager)
        if (enableTTS && ttsManager != null)
        {
            LoggerManager.Debug("[2/5] 初始化 TTS (RKTTSManager)...", "SenseOnnx");
            yield return StartCoroutine(WaitForTTSReady());
        }
        else
        {
            LoggerManager.Debug("[2/5] RK TTS 已禁用或未找到管理器", "SenseOnnx");
        }

        // 3. 初始化 Onnx TTS
        if (enableOnnxTTS)
        {
            LoggerManager.Debug("[3/5] 初始化 Onnx TTS...", "SenseOnnx");
            yield return StartCoroutine(InitializeOnnxTTS());
        }
        else
        {
            LoggerManager.Debug("[3/5] Onnx TTS 已禁用", "SenseOnnx");
        }

        // 4. 初始化 STT
        if (enableSTT && sttManager != null)
        {
            LoggerManager.Debug("[4/5] 初始化 STT...", "SenseOnnx");
            // TODO: 实现 STT 初始化
        }
        else
        {
            LoggerManager.Debug("[4/5] STT 已禁用或未找到管理器", "SenseOnnx");
        }

        // 5. 初始化 Onnx STT
        if (enableSTT)
        {
            LoggerManager.Debug("[5/5] 初始化 Onnx STT...", "SenseOnnx");
            yield return StartCoroutine(InitializeOnnxSTT());
        }
        else
        {
            LoggerManager.Debug("[5/5] Onnx STT 已禁用", "SenseOnnx");
        }

        // 设置回调监听器
        SetupDataCallbackListeners();

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

    #region Onnx TTS/STT 初始化

    /// <summary>
    /// 初始化 Onnx TTS
    /// </summary>
    private IEnumerator InitializeOnnxTTS()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            LoggerManager.Debug("开始初始化 Onnx TTS...", "SenseOnnx");

            // 步骤 1: 加载 senseonnx native 库
            LoggerManager.Debug("[1/4] 加载 senseonnx native 库...", "SenseOnnx");
            try
            {
                using (AndroidJavaClass systemClass = new AndroidJavaClass("java.lang.System"))
                {
                    systemClass.CallStatic("loadLibrary", "senseonnx");
                    LoggerManager.Debug("[1/4] ✅ senseonnx 库加载成功", "SenseOnnx");
                }
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"加载 senseonnx 库失败（可能已加载）: {e.Message}", "SenseOnnx");
            }

            // 步骤 2: 获取 Unity Activity
            LoggerManager.Debug("[2/4] 获取 Unity Activity...", "SenseOnnx");
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            if (unityActivity == null)
            {
                LoggerManager.Error("[2/4] 无法获取 Unity Activity", "SenseOnnx");
                yield break;
            }
            LoggerManager.Debug("[2/4] ✅ Unity Activity 获取成功", "SenseOnnx");

            // 步骤 3: 创建 Onnx TTS 检测器实例
            LoggerManager.Debug("[3/4] 创建 Onnx TTS 检测器实例...", "SenseOnnx");

            // 尝试多种构造函数
            try
            {
                // 尝试 1: 使用 Application 参数
                AndroidJavaObject application = unityActivity.Call<AndroidJavaObject>("getApplication");
                onnxTtsDetector = new AndroidJavaObject("com.senseflow.senseonnx.SenseOnnxTtsDetector", application);
                LoggerManager.Debug("[3/4] ✅ Onnx TTS 检测器创建成功 (Application)", "SenseOnnx");
            }
            catch (System.Exception e1)
            {
                LoggerManager.Warning($"构造函数 (Application) 失败: {e1.Message}", "SenseOnnx");

                // 尝试 2: 使用 Activity 参数
                try
                {
                    onnxTtsDetector = new AndroidJavaObject("com.senseflow.senseonnx.SenseOnnxTtsDetector", unityActivity);
                    LoggerManager.Debug("[3/4] ✅ Onnx TTS 检测器创建成功 (Activity)", "SenseOnnx");
                }
                catch (System.Exception e2)
                {
                    LoggerManager.Error($"所有构造方法都失败", "SenseOnnx");
                    LoggerManager.Error($"  - Application: {e1.Message}", "SenseOnnx");
                    LoggerManager.Error($"  - Activity: {e2.Message}", "SenseOnnx");
                    yield break;
                }
            }

            if (onnxTtsDetector == null)
            {
                LoggerManager.Error("[3/4] Onnx TTS 检测器创建失败", "SenseOnnx");
                yield break;
            }

            // 步骤 4: 初始化并启动
            LoggerManager.Debug("[4/4] 初始化并启动 Onnx TTS...", "SenseOnnx");
            onnxTtsDetector.Call("initialize");
            onnxTtsDetector.Call("start");
            isOnnxTTSReady = true;
            LoggerManager.Info("✅ Onnx TTS 初始化完成", "SenseOnnx");
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"Onnx TTS 初始化失败: {e.Message}", "SenseOnnx");
        }
#endif
        yield return null;
    }

    /// <summary>
    /// 初始化 Onnx STT
    /// </summary>
    private IEnumerator InitializeOnnxSTT()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            LoggerManager.Debug("开始初始化 Onnx STT...", "SenseOnnx");

            // 步骤 1: 加载 senseonnx native 库（如果还没加载）
            LoggerManager.Debug("[1/4] 确保 senseonnx native 库已加载...", "SenseOnnx");
            try
            {
                using (AndroidJavaClass systemClass = new AndroidJavaClass("java.lang.System"))
                {
                    systemClass.CallStatic("loadLibrary", "senseonnx");
                    LoggerManager.Debug("[1/4] ✅ senseonnx 库加载成功", "SenseOnnx");
                }
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"加载 senseonnx 库失败（可能已加载）: {e.Message}", "SenseOnnx");
            }

            // 步骤 2: 获取 Unity Activity（如果还没有）
            LoggerManager.Debug("[2/4] 获取 Unity Activity...", "SenseOnnx");
            if (unityActivity == null)
            {
                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                {
                    unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                }
            }

            if (unityActivity == null)
            {
                LoggerManager.Error("[2/4] 无法获取 Unity Activity", "SenseOnnx");
                yield break;
            }
            LoggerManager.Debug("[2/4] ✅ Unity Activity 获取成功", "SenseOnnx");

            // 步骤 3: 创建 Onnx STT 检测器实例
            LoggerManager.Debug("[3/4] 创建 Onnx STT 检测器实例...", "SenseOnnx");

            // 尝试多种构造函数
            try
            {
                // 尝试 1: 使用 Application 参数
                AndroidJavaObject application = unityActivity.Call<AndroidJavaObject>("getApplication");
                onnxSttDetector = new AndroidJavaObject("com.senseflow.senseonnx.SenseOnnxSttDetector", application);
                LoggerManager.Debug("[3/4] ✅ Onnx STT 检测器创建成功 (Application)", "SenseOnnx");
            }
            catch (System.Exception e1)
            {
                LoggerManager.Warning($"构造函数 (Application) 失败: {e1.Message}", "SenseOnnx");

                // 尝试 2: 使用 Activity 参数
                try
                {
                    onnxSttDetector = new AndroidJavaObject("com.senseflow.senseonnx.SenseOnnxSttDetector", unityActivity);
                    LoggerManager.Debug("[3/4] ✅ Onnx STT 检测器创建成功 (Activity)", "SenseOnnx");
                }
                catch (System.Exception e2)
                {
                    LoggerManager.Error($"所有构造方法都失败", "SenseOnnx");
                    LoggerManager.Error($"  - Application: {e1.Message}", "SenseOnnx");
                    LoggerManager.Error($"  - Activity: {e2.Message}", "SenseOnnx");
                    yield break;
                }
            }

            if (onnxSttDetector == null)
            {
                LoggerManager.Error("[3/4] Onnx STT 检测器创建失败", "SenseOnnx");
                yield break;
            }

            // 步骤 4: 初始化并启动
            LoggerManager.Debug("[4/4] 初始化并启动 Onnx STT...", "SenseOnnx");
            onnxSttDetector.Call("initialize");
            onnxSttDetector.Call("start");
            isOnnxSTTReady = true;
            LoggerManager.Info("✅ Onnx STT 初始化完成", "SenseOnnx");
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"Onnx STT 初始化失败: {e.Message}", "SenseOnnx");
        }
#endif
        yield return null;
    }

    /// <summary>
    /// 设置数据回调监听器
    /// </summary>
    private void SetupDataCallbackListeners()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 设置 Onnx TTS 回调监听器
        if (isOnnxTTSReady && onnxTtsDetector != null)
        {
            onnxTtsDataCallback = new OnnxTtsDataCallback(this);
            // 根据你的 Android SDK，设置监听器的方法名可能不同
            // 这里假设有 setDataCallbackListener 方法
            try
            {
                onnxTtsDetector.Call("setDataCallbackListener", new OnnxTtsCallbackProxy(onnxTtsDataCallback));
                LoggerManager.Debug("✅ Onnx TTS 回调监听器已设置", "SenseOnnx");
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"设置 Onnx TTS 回调失败: {e.Message}", "SenseOnnx");
            }
        }

        // 设置 Onnx STT 回调监听器
        if (isOnnxSTTReady && onnxSttDetector != null)
        {
            onnxSttDataCallback = new OnnxSttDataCallback(this);
            // 根据你的 Android SDK，设置监听器的方法名可能不同
            try
            {
                onnxSttDetector.Call("setDataCallbackListener", new OnnxSttCallbackProxy(onnxSttDataCallback));
                LoggerManager.Debug("✅ Onnx STT 回调监听器已设置", "SenseOnnx");
            }
            catch (System.Exception e)
            {
                LoggerManager.Warning($"设置 Onnx STT 回调失败: {e.Message}", "SenseOnnx");
            }
        }
#endif
    }

    #endregion

    #region 公共 API - Onnx TTS 接口

    /// <summary>
    /// Onnx TTS 文字转语音
    /// </summary>
    public void OnnxTtsGenerate(string text)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isOnnxTTSReady || onnxTtsDetector == null)
        {
            LoggerManager.Warning("Onnx TTS 未就绪", "SenseOnnx");
            return;
        }

        try
        {
            LoggerManager.Debug($"Onnx TTS 生成: {text}", "SenseOnnx");
            onnxTtsDetector.Call("generate", text);
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"Onnx TTS 生成失败: {e.Message}", "SenseOnnx");
        }
#else
        LoggerManager.Warning($"[模拟] Onnx TTS 生成: {text}", "SenseOnnx");
#endif
    }

    /// <summary>
    /// 检查 Onnx TTS 是否就绪
    /// </summary>
    public bool IsOnnxTTSReady()
    {
        return isOnnxTTSReady;
    }

    #endregion

    #region 公共 API - Onnx STT 接口

    /// <summary>
    /// Onnx STT 开始识别
    /// </summary>
    public void OnnxSttStartRecognition()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isOnnxSTTReady || onnxSttDetector == null)
        {
            LoggerManager.Warning("Onnx STT 未就绪", "SenseOnnx");
            return;
        }

        try
        {
            LoggerManager.Debug("Onnx STT 开始识别", "SenseOnnx");
            onnxSttDetector.Call("startRecognition");
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"Onnx STT 开始识别失败: {e.Message}", "SenseOnnx");
        }
#else
        LoggerManager.Warning("[模拟] Onnx STT 开始识别", "SenseOnnx");
#endif
    }

    /// <summary>
    /// Onnx STT 停止识别
    /// </summary>
    public void OnnxSttStopRecognition()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isOnnxSTTReady || onnxSttDetector == null)
        {
            LoggerManager.Warning("Onnx STT 未就绪", "SenseOnnx");
            return;
        }

        try
        {
            LoggerManager.Debug("Onnx STT 停止识别", "SenseOnnx");
            onnxSttDetector.Call("stopRecognition");
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"Onnx STT 停止识别失败: {e.Message}", "SenseOnnx");
        }
#else
        LoggerManager.Warning("[模拟] Onnx STT 停止识别", "SenseOnnx");
#endif
    }

    /// <summary>
    /// 检查 Onnx STT 是否就绪
    /// </summary>
    public bool IsOnnxSTTReady()
    {
        return isOnnxSTTReady;
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
        if (IsOnnxTTSReady())
        {
            OnnxTtsGenerate(message);
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

        // 停止 Onnx STT
        if (IsOnnxSTTReady())
        {
            OnnxSttStopRecognition();
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
        sb.AppendLine("--- Onnx TTS ---");
        sb.AppendLine($"已启用: {enableOnnxTTS}");
        sb.AppendLine($"已就绪: {IsOnnxTTSReady()}");
        sb.AppendLine();
        sb.AppendLine("--- STT ---");
        sb.AppendLine($"已启用: {enableSTT}");
        sb.AppendLine($"已就绪: {IsSTTReady()}");
        sb.AppendLine();
        sb.AppendLine("--- Onnx STT ---");
        sb.AppendLine($"已启用: {enableSTT}");
        sb.AppendLine($"已就绪: {IsOnnxSTTReady()}");

        return sb.ToString();
    }

    #endregion

    #region 公共 API - STT 接口（预留）

    // TODO: 添加 STT 相关接口

    #endregion
}

#region Onnx TTS 回调实现

/// <summary>
/// Onnx TTS 数据回调实现
/// </summary>
public class OnnxTtsDataCallback : DataCallbackListener<float[]>
{
    private SenseOnnxManager manager;

    public OnnxTtsDataCallback(SenseOnnxManager manager)
    {
        this.manager = manager;
    }

    public void OnDataChunkCallback(float[] data)
    {
        LoggerManager.Debug($"Onnx TTS 数据块回调: {data?.Length ?? 0} 个采样", "SenseOnnx");
        // 在这里处理 TTS 流式数据块
        // 例如：播放音频、保存到缓冲区等
    }

    public void OnDataFinishCallback(float[] data)
    {
        LoggerManager.Debug($"Onnx TTS 数据完成回调: {data?.Length ?? 0} 个采样", "SenseOnnx");
        // 在这里处理 TTS 最终数据
        // 例如：播放完整音频、通知完成等
    }
}

/// <summary>
/// Onnx TTS Android 回调代理
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class OnnxTtsCallbackProxy : AndroidJavaProxy
{
    private DataCallbackListener<float[]> callback;

    public OnnxTtsCallbackProxy(DataCallbackListener<float[]> callback)
        : base("com.senseflow.senseonnx.DataCallbackListener")
    {
        this.callback = callback;
    }

    // Android 回调方法
    public void onDataChunkCallback(float[] data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataChunkCallback(data);
        });
    }

    public void onDataFinishCallback(float[] data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataFinishCallback(data);
        });
    }
}
#endif

#endregion

#region Onnx STT 回调实现

/// <summary>
/// Onnx STT 数据回调实现
/// </summary>
public class OnnxSttDataCallback : DataCallbackListener<string>
{
    private SenseOnnxManager manager;

    public OnnxSttDataCallback(SenseOnnxManager manager)
    {
        this.manager = manager;
    }

    public void OnDataChunkCallback(string data)
    {
        LoggerManager.Debug($"Onnx STT 数据块回调: {data}", "SenseOnnx");
        // 在这里处理 STT 流式识别结果
        // 例如：更新 UI 显示中间结果
    }

    public void OnDataFinishCallback(string data)
    {
        LoggerManager.Debug($"Onnx STT 数据完成回调: {data}", "SenseOnnx");

        // 在这里处理 STT 最终识别结果
        // 例如：将识别结果传给 TTS 进行语音合成
        if (manager != null && manager.IsOnnxTTSReady())
        {
            manager.OnnxTtsGenerate(data);
        }
    }
}

/// <summary>
/// Onnx STT Android 回调代理
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class OnnxSttCallbackProxy : AndroidJavaProxy
{
    private DataCallbackListener<string> callback;

    public OnnxSttCallbackProxy(DataCallbackListener<string> callback)
        : base("com.senseflow.senseonnx.DataCallbackListener")
    {
        this.callback = callback;
    }

    // Android 回调方法
    public void onDataChunkCallback(string data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataChunkCallback(data);
        });
    }

    public void onDataFinishCallback(string data)
    {
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            callback.OnDataFinishCallback(data);
        });
    }
}
#endif

#endregion
