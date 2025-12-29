using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;

/// <summary>
/// RKLLM 管理器
/// 用于在 Unity 中调用 Android 原生的 RKLLM 功能
/// </summary>
[DefaultExecutionOrder(100)]  // 延后执行顺序，等待 SDKLoader 启用
public class RKLLMManager : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("图片宽度")]
    public int imageWidth = 640;

    [Tooltip("图片高度")]
    public int imageHeight = 480;

    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = false;

    [Header("引用")]
    [Tooltip("摄像头捕获组件")]
    public CameraCapture cameraCapture;

    // Android JNI 相关
    private AndroidJavaObject rkllmDetector;
    private AndroidJavaObject unityActivity;
    private bool isInitialized = false;

    // 事件
    public event Action<string> OnLLMResult;
    public event Action<string> OnLLMError;
    public event Action OnLLMComplete;  // 对话完成事件（callState == 2）

    // SenseOnnxManager 兼容事件
    public event Action<string> OnResponseReceived;  // 完整响应接收完成
    public event Action<string, bool> OnStreamingUpdate;  // 流式更新（chunk, isComplete）
    public event Action<string> OnError;  // 错误事件

    // 单例
    private static RKLLMManager instance;
    public static RKLLMManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        LoggerManager.Debug("Awake() 被调用", "LLM");

        if (instance != null && instance != this)
        {
            LoggerManager.Debug("检测到重复实例，销毁当前对象", "LLM");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        LoggerManager.Debug("单例设置完成", "LLM");

        // 延迟加载模式：初始时禁用，等待 SDKLoader 启用
        enabled = false;
        LoggerManager.Debug("已禁用，等待 SDKLoader 延迟加载", "LLM");
    }

    void OnEnable()
    {
        LoggerManager.Debug("OnEnable() 被调用 - 开始初始化", "LLM");

        // 被 SDKLoader 启用时才开始初始化流程
#if UNITY_ANDROID && !UNITY_EDITOR
        LoggerManager.Debug("检测到 Android 平台，准备初始化...", "LLM");

        // 请求存储权限（SDK需要访问模型文件）
        RequestStoragePermissions();

        // 延迟初始化，等待权限授予
        StartCoroutine(InitializeAfterPermissions());
#else
        if (enableDebugLog)
            LoggerManager.Warning("当前平台不支持 RKLLM (仅支持 Android)", "LLM");
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
            LoggerManager.Debug("请求读取存储权限...", "LLM");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageRead);
        }

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite))
        {
            LoggerManager.Debug("请求写入存储权限...", "LLM");
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
            if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageRead))
            {
                LoggerManager.Debug("存储权限已授予，开始初始化", "LLM");
                InitializeRKLLM();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        // 超时或用户拒绝权限
        LoggerManager.Warning("未获得存储权限，尝试继续初始化（可能失败）", "LLM");
        InitializeRKLLM();
#endif
        yield return null;
    }

    /// <summary>
    /// 初始化 RKLLM
    /// </summary>
    private void InitializeRKLLM()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (enableDebugLog)
                LoggerManager.Debug("开始初始化 RKLLM...", "LLM");

            // 步骤 1: 获取 Unity Activity
            if (enableDebugLog)
                LoggerManager.Debug("[1/4] 获取 Unity Activity...", "LLM");

            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            if (unityActivity == null)
            {
                LoggerManager.Error("无法获取 Unity Activity", "LLM");
                return;
            }

            if (enableDebugLog)
                LoggerManager.Debug("[1/4] ✅ Unity Activity 获取成功", "LLM");

            // 步骤 2: 创建 SenseRKLlmDetector 实例
            if (enableDebugLog)
                LoggerManager.Debug("[2/4] 创建 SenseRKLlmDetector 实例...", "LLM");

            AndroidJavaObject application = unityActivity.Call<AndroidJavaObject>("getApplication");
            // 使用新的包名 com.sensetime.rkllm
            rkllmDetector = new AndroidJavaObject("com.sensetime.rkllm.SenseRKLlmDetector", application);

            if (enableDebugLog)
                LoggerManager.Debug("[2/4] ✅ SenseRKLlmDetector 实例创建成功", "LLM");

            // 步骤 3: 设置结果监听器
            if (enableDebugLog)
                LoggerManager.Debug("[3/4] 设置结果监听器...", "LLM");

            rkllmDetector.Call("setOnResultListener", new RKLLMResultListener(this));

            if (enableDebugLog)
                LoggerManager.Debug("[3/4] ✅ 结果监听器设置成功", "LLM");

            // 步骤 4: 初始化并启动检测器
            if (enableDebugLog)
                LoggerManager.Debug("[4/4] 初始化并启动检测器...", "LLM");

            rkllmDetector.Call("initialize");
            rkllmDetector.Call("start");

            isInitialized = true;

            if (enableDebugLog)
                LoggerManager.Info("✅ RKLLM 初始化完成", "LLM");
        }
        catch (Exception e)
        {
            LoggerManager.Error($"初始化失败 - {e.Message}\n{e.StackTrace}", "LLM");
            string errorMsg = $"初始化失败: {e.Message}";
            OnLLMError?.Invoke(errorMsg);
            OnError?.Invoke(errorMsg);  // SenseOnnxManager 兼容事件
            isInitialized = false;
        }
#endif
    }

    /// <summary>
    /// 发送消息（SenseOnnxManager 兼容方法）
    /// </summary>
    public void SendMessage(string message)
    {
        Chat(message, null);
    }

    /// <summary>
    /// 发送聊天消息（带图片）
    /// </summary>
    /// <param name="message">消息内容</param>
    /// <param name="texture">图片纹理（可选）</param>
    public void Chat(string message, Texture2D texture = null)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized)
        {
            LoggerManager.Error("RKLLM 未初始化", "LLM");
            OnLLMError?.Invoke("RKLLM 未初始化");
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            LoggerManager.Warning("消息不能为空", "LLM");
            return;
        }

        try
        {
            if (enableDebugLog)
                LoggerManager.Debug($"发送消息 - {message}", "LLM");

            if (texture != null)
            {
                // 将 Texture2D 转换为 Android Bitmap
                AndroidJavaObject bitmap = TextureToBitmap(texture);

                if (bitmap != null)
                {
                    if (enableDebugLog)
                        LoggerManager.Debug("调用 chat 方法（带图片）", "LLM");
                    rkllmDetector.Call("chat", message, bitmap);
                    bitmap.Dispose();
                }
                else
                {
                    LoggerManager.Error("图片转换失败", "LLM");
                    OnLLMError?.Invoke("图片转换失败");
                }
            }
            else
            {
               rkllmDetector.Call("chat", message, null);
            }
        }
        catch (Exception e)
        {
            LoggerManager.Error($"发送消息失败 - {e.Message}", "LLM");
            string errorMsg = $"发送消息失败: {e.Message}";
            OnLLMError?.Invoke(errorMsg);
            OnError?.Invoke(errorMsg);  // SenseOnnxManager 兼容事件
        }
#else
        LoggerManager.Warning($"[模拟] 发送消息 - {message}", "LLM");
        // 编辑器模式下的模拟响应
        StartCoroutine(SimulateResponse(message));
#endif
    }

    /// <summary>
    /// 使用当前摄像头画面发送消息
    /// </summary>
    /// <param name="message">消息内容</param>
    public void ChatWithCurrentCamera(string message)
    {
        if (cameraCapture == null)
        {
            LoggerManager.Error("CameraCapture 未设置", "LLM");
            OnLLMError?.Invoke("CameraCapture 未设置");
            return;
        }

        Texture2D currentFrame = cameraCapture.GetCurrentFrame();
        if (currentFrame == null)
        {
            LoggerManager.Warning("无法获取当前摄像头画面", "LLM");
            OnLLMError?.Invoke("无法获取当前摄像头画面");
            return;
        }

        Chat(message, currentFrame);
    }

    /// <summary>
    /// 将 Unity Texture2D 转换为 Android Bitmap
    /// </summary>
    private AndroidJavaObject TextureToBitmap(Texture2D texture)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            // 确保纹理可读
            if (!texture.isReadable)
            {
                LoggerManager.Error("纹理不可读，请在 Import Settings 中启用 Read/Write", "LLM");
                return null;
            }

            // 获取像素数据
            Color32[] pixels = texture.GetPixels32();
            int width = texture.width;
            int height = texture.height;

            // 创建 Android Bitmap
            using (AndroidJavaClass bitmapConfig = new AndroidJavaClass("android.graphics.Bitmap$Config"))
            using (AndroidJavaObject argb8888 = bitmapConfig.GetStatic<AndroidJavaObject>("ARGB_8888"))
            using (AndroidJavaClass bitmapClass = new AndroidJavaClass("android.graphics.Bitmap"))
            {
                AndroidJavaObject bitmap = bitmapClass.CallStatic<AndroidJavaObject>(
                    "createBitmap", width, height, argb8888
                );

                // 转换像素数据
                int[] androidPixels = new int[pixels.Length];
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color32 pixel = pixels[i];
                    androidPixels[i] = (pixel.a << 24) | (pixel.r << 16) | (pixel.g << 8) | pixel.b;
                }

                // 设置像素到 Bitmap
                using (AndroidJavaClass intBufferClass = new AndroidJavaClass("java.nio.IntBuffer"))
                using (AndroidJavaObject buffer = intBufferClass.CallStatic<AndroidJavaObject>("wrap", androidPixels))
                {
                    bitmap.Call("copyPixelsFromBuffer", buffer);
                }

                return bitmap;
            }
        }
        catch (Exception e)
        {
            LoggerManager.Error($"Texture2D 转 Bitmap 失败 - {e.Message}", "LLM");
            return null;
        }
#else
        return null;
#endif
    }

    /// <summary>
    /// 编辑器模式下的模拟响应
    /// </summary>
    private IEnumerator SimulateResponse(string message)
    {
        yield return new WaitForSeconds(1f);
        string response = $"[模拟响应] 收到消息: {message}";
        OnLLMResult?.Invoke(response);
    }

    /// <summary>
    /// 处理来自 Android 的结果回调
    /// </summary>
    internal void HandleResult(string result, int callState)
    {
        // callState: 0 = 开始, 1 = 进行中, 2 = 结束
        if (callState == 2)
        {
            if (enableDebugLog)
                LoggerManager.Debug("对话结束", "LLM");

            // 触发对话完成事件
            OnLLMComplete?.Invoke();

            // 触发 SenseOnnxManager 兼容事件（流式更新完成）
            if (!string.IsNullOrEmpty(result))
            {
                OnStreamingUpdate?.Invoke(result, true);
                OnResponseReceived?.Invoke(result);
            }
            return;
        }

        if (!string.IsNullOrEmpty(result))
        {
            if (enableDebugLog)
                LoggerManager.Debug($"收到结果 - {result}", "LLM");

            OnLLMResult?.Invoke(result);

            // 触发 SenseOnnxManager 兼容事件（流式更新中）
            OnStreamingUpdate?.Invoke(result, false);
        }
    }

    void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (rkllmDetector != null)
        {
            try
            {
                rkllmDetector.Call("stop");
                rkllmDetector.Dispose();
            }
            catch (Exception e)
            {
                LoggerManager.Error($"销毁时出错 - {e.Message}", "LLM");
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
    /// 设置图片尺寸
    /// </summary>
    public void SetImageSize(int width, int height)
    {
        imageWidth = width;
        imageHeight = height;

#if UNITY_ANDROID && !UNITY_EDITOR
        if (isInitialized)
        {
            // 使用新的包名 com.sensetime.rkllm
            using (AndroidJavaClass modelConfig = new AndroidJavaClass("com.sensetime.rkllm.ModelConfig"))
            {
                modelConfig.CallStatic("setImageSize", width, height);
            }
        }
#endif
    }

    #endregion
}

/// <summary>
/// RKLLM 结果监听器（Android 回调）
/// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
public class RKLLMResultListener : AndroidJavaProxy
{
    private RKLLMManager manager;

    // 使用新的包名 com.sensetime.rkllm
    public RKLLMResultListener(RKLLMManager manager)
        : base("com.sensetime.rkllm.OnResultListener")
    {
        this.manager = manager;
    }

    // 对应 Android 的 onResult(String result, Object any, int callState)
    public void onResult(string result, AndroidJavaObject any, int callState)
    {
        // 切换到主线程处理
        UnityMainThreadDispatcher.Instance().Enqueue(() =>
        {
            manager.HandleResult(result, callState);
        });
    }
}
#endif
