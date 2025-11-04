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
    public bool enableDebugLog = true;

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

    // 单例
    private static RKLLMManager instance;
    public static RKLLMManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        Debug.Log("RKLLMManager: Awake() 被调用");

        if (instance != null && instance != this)
        {
            Debug.Log("RKLLMManager: 检测到重复实例，销毁当前对象");
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);

        Debug.Log("RKLLMManager: 单例设置完成");

        // 延迟加载模式：初始时禁用，等待 SDKLoader 启用
        enabled = false;
        Debug.Log("RKLLMManager: 已禁用，等待 SDKLoader 延迟加载");
    }

    void OnEnable()
    {
        Debug.Log("RKLLMManager: OnEnable() 被调用 - 开始初始化");

        // 被 SDKLoader 启用时才开始初始化流程
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("RKLLMManager: 检测到 Android 平台，准备初始化...");

        // 请求存储权限（SDK需要访问模型文件）
        RequestStoragePermissions();

        // 延迟初始化，等待权限授予
        StartCoroutine(InitializeAfterPermissions());
#else
        if (enableDebugLog)
            Debug.LogWarning("RKLLMManager: 当前平台不支持 RKLLM (仅支持 Android)");
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
            Debug.Log("RKLLMManager: 请求读取存储权限...");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageRead);
        }

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite))
        {
            Debug.Log("RKLLMManager: 请求写入存储权限...");
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
                Debug.Log("RKLLMManager: 存储权限已授予，开始初始化");
                InitializeRKLLM();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        // 超时或用户拒绝权限
        Debug.LogWarning("RKLLMManager: 未获得存储权限，尝试继续初始化（可能失败）");
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
                Debug.Log("RKLLMManager: 开始初始化 RKLLM...");

            // 步骤 1: 获取 Unity Activity
            if (enableDebugLog)
                Debug.Log("RKLLMManager: [1/6] 获取 Unity Activity...");

            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            if (unityActivity == null)
            {
                Debug.LogError("RKLLMManager: 无法获取 Unity Activity");
                return;
            }

            if (enableDebugLog)
                Debug.Log("RKLLMManager: [1/4] ✅ Unity Activity 获取成功");

            // 步骤 2: 创建 SenseRKLlmDetector 实例
            if (enableDebugLog)
                Debug.Log("RKLLMManager: [2/4] 创建 SenseRKLlmDetector 实例...");

            AndroidJavaObject application = unityActivity.Call<AndroidJavaObject>("getApplication");
            rkllmDetector = new AndroidJavaObject("com.senseflow.rkllm.SenseRKLlmDetector", application);

            if (enableDebugLog)
                Debug.Log("RKLLMManager: [2/4] ✅ SenseRKLlmDetector 实例创建成功");

            // 步骤 3: 设置结果监听器
            if (enableDebugLog)
                Debug.Log("RKLLMManager: [3/4] 设置结果监听器...");

            rkllmDetector.Call("setOnResultListener", new RKLLMResultListener(this));

            if (enableDebugLog)
                Debug.Log("RKLLMManager: [3/4] ✅ 结果监听器设置成功");

            // 步骤 4: 初始化并启动检测器
            if (enableDebugLog)
                Debug.Log("RKLLMManager: [4/4] 初始化并启动检测器...");

            rkllmDetector.Call("initialize");
            rkllmDetector.Call("start");

            isInitialized = true;

            if (enableDebugLog)
                Debug.Log("RKLLMManager: ✅ RKLLM 初始化完成");
        }
        catch (Exception e)
        {
            Debug.LogError($"RKLLMManager: 初始化失败 - {e.Message}\n{e.StackTrace}");
            OnLLMError?.Invoke($"初始化失败: {e.Message}");
            isInitialized = false;
        }
#endif
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
            Debug.LogError("RKLLMManager: RKLLM 未初始化");
            OnLLMError?.Invoke("RKLLM 未初始化");
            return;
        }

        if (string.IsNullOrEmpty(message))
        {
            Debug.LogWarning("RKLLMManager: 消息不能为空");
            return;
        }

        try
        {
            if (enableDebugLog)
                Debug.Log($"RKLLMManager: 发送消息 - {message}");

            if (texture != null)
            {
                // 将 Texture2D 转换为 Android Bitmap
                AndroidJavaObject bitmap = TextureToBitmap(texture);

                if (bitmap != null)
                {
                    if (enableDebugLog)
                        Debug.Log("RKLLMManager: 调用 chat 方法（带图片）");
                    rkllmDetector.Call("chat", message, bitmap);
                    bitmap.Dispose();
                }
                else
                {
                    Debug.LogError("RKLLMManager: 图片转换失败");
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
            Debug.LogError($"RKLLMManager: 发送消息失败 - {e.Message}");
            OnLLMError?.Invoke($"发送消息失败: {e.Message}");
        }
#else
        Debug.LogWarning($"RKLLMManager: [模拟] 发送消息 - {message}");
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
            Debug.LogError("RKLLMManager: CameraCapture 未设置");
            OnLLMError?.Invoke("CameraCapture 未设置");
            return;
        }

        Texture2D currentFrame = cameraCapture.GetCurrentFrame();
        if (currentFrame == null)
        {
            Debug.LogWarning("RKLLMManager: 无法获取当前摄像头画面");
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
                Debug.LogError("RKLLMManager: 纹理不可读，请在 Import Settings 中启用 Read/Write");
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
            Debug.LogError($"RKLLMManager: Texture2D 转 Bitmap 失败 - {e.Message}");
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
                Debug.Log("RKLLMManager: 对话结束");

            // 触发对话完成事件
            OnLLMComplete?.Invoke();
            return;
        }

        if (!string.IsNullOrEmpty(result))
        {
            if (enableDebugLog)
                Debug.Log($"RKLLMManager: 收到结果 - {result}");

            OnLLMResult?.Invoke(result);
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
                Debug.LogError($"RKLLMManager: 销毁时出错 - {e.Message}");
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
            using (AndroidJavaClass modelConfig = new AndroidJavaClass("com.senseflow.rkllm.ModelConfig"))
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

    public RKLLMResultListener(RKLLMManager manager)
        : base("com.senseflow.rkllm.OnResultListener")
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
