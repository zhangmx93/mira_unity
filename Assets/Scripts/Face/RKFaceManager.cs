using UnityEngine;
using System;
using System.Collections;

/// <summary>
/// SenseFlow RKFace SDK 使用示例
/// 此脚本演示如何在 Unity 中调用 Android 原生的 RKFace SDK
/// </summary>
[DefaultExecutionOrder(100)]  // 延后执行顺序，等待 SDKLoader 启用
public class RKFaceManager : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject rkfaceInstance;
    private AndroidJavaClass unityPlayer;
    private AndroidJavaObject currentActivity;
#endif

    [Header("RKFace Settings")]
    [Tooltip("RKFace 模型路径（相对于 StreamingAssets）")]
    public string modelPath = "rkface_model";

    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    private bool isInitialized = false;

    void Awake()
    {
        LoggerManager.Debug("Awake() 被调用", "Face");

        // 延迟加载模式：初始时禁用，等待 SDKLoader 启用
        enabled = false;
        LoggerManager.Debug("已禁用，等待 SDKLoader 延迟加载", "Face");
    }

    void OnEnable()
    {
        LoggerManager.Debug("OnEnable() 被调用 - 开始初始化", "Face");

        // 被 SDKLoader 启用时才开始初始化流程
#if UNITY_ANDROID && !UNITY_EDITOR
        LoggerManager.Debug("检测到 Android 平台，准备初始化...", "Face");

        // 请求存储权限（SDK需要访问模型文件）
        RequestStoragePermissions();

        // 延迟初始化，等待权限授予
        StartCoroutine(InitializeAfterPermissions());
#else
        if (enableDebugLog)
            LoggerManager.Warning("当前平台不支持 RKFace (仅支持 Android)", "Face");
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
            LoggerManager.Debug("请求读取存储权限...", "Face");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageRead);
        }

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite))
        {
            LoggerManager.Debug("请求写入存储权限...", "Face");
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
                LoggerManager.Debug("存储权限已授予，开始初始化", "Face");
                InitializeRKFace();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        // 超时或用户拒绝权限
        LoggerManager.Warning("未获得存储权限，尝试继续初始化（可能失败）", "Face");
        InitializeRKFace();
#endif
        yield return null;
    }

    /// <summary>
    /// 初始化 RKFace SDK
    /// </summary>
    private void InitializeRKFace()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (enableDebugLog)
                LoggerManager.Debug("开始初始化 RKFace SDK...", "Face");

            // 步骤 1: 获取当前 Activity
            if (enableDebugLog)
                LoggerManager.Debug("[1/3] 获取 Unity Activity...", "Face");

            unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (currentActivity == null)
            {
                LoggerManager.Error("无法获取 Unity Activity", "Face");
                return;
            }

            if (enableDebugLog)
                LoggerManager.Debug("[1/3] ✅ Unity Activity 获取成功", "Face");

            // 步骤 2: 创建 RKFace 实例
            if (enableDebugLog)
                LoggerManager.Debug("[2/3] 创建 RKFaceSDK 实例...", "Face");

            AndroidJavaClass rkfaceClass = new AndroidJavaClass("com.senseflow.rkface.RKFaceSDK");
            rkfaceInstance = rkfaceClass.CallStatic<AndroidJavaObject>("getInstance");

            if (rkfaceInstance == null)
            {
                LoggerManager.Error("无法获取 RKFace SDK 实例", "Face");
                return;
            }

            if (enableDebugLog)
                LoggerManager.Debug("[2/3] ✅ RKFaceSDK 实例创建成功", "Face");

            // 步骤 3: 初始化 SDK
            if (enableDebugLog)
                LoggerManager.Debug("[3/3] 初始化 SDK...", "Face");

            bool success = rkfaceInstance.Call<bool>("init", currentActivity, modelPath);

            if (success)
            {
                isInitialized = true;
                if (enableDebugLog)
                    LoggerManager.Info("✅ RKFace SDK 初始化成功！", "Face");
            }
            else
            {
                LoggerManager.Error("RKFace SDK 初始化失败！", "Face");
                isInitialized = false;
            }
        }
        catch (Exception e)
        {
            LoggerManager.Error($"初始化异常 - {e.Message}\n{e.StackTrace}", "Face");
            isInitialized = false;
        }
#endif
    }

    /// <summary>
    /// 人脸检测
    /// </summary>
    /// <param name="imageBytes">图像字节数据（如相机捕获的图像）</param>
    /// <returns>检测结果 JSON 字符串</returns>
    public string DetectFace(byte[] imageBytes)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized)
        {
            LoggerManager.Warning("SDK 未初始化，请先调用 InitializeRKFace()", "Face");
            return null;
        }

        try
        {
            // 调用人脸检测方法
            // 注意：实际的方法名和参数需要根据 RKFace SDK 的文档进行调整
            string result = rkfaceInstance.Call<string>("detectFace", imageBytes);
            LoggerManager.Debug($"人脸检测结果: {result}", "Face");
            return result;
        }
        catch (Exception e)
        {
            LoggerManager.Error($"人脸检测异常: {e.Message}", "Face");
            return null;
        }
#else
        LoggerManager.Warning("仅在 Android 设备上可用！", "Face");
        return null;
#endif
    }

    /// <summary>
    /// 人脸识别
    /// </summary>
    /// <param name="imageBytes">图像字节数据</param>
    /// <returns>识别结果</returns>
    public string RecognizeFace(byte[] imageBytes)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized)
        {
            LoggerManager.Warning("SDK 未初始化！", "Face");
            return null;
        }

        try
        {
            string result = rkfaceInstance.Call<string>("recognizeFace", imageBytes);
            LoggerManager.Debug($"人脸识别结果: {result}", "Face");
            return result;
        }
        catch (Exception e)
        {
            LoggerManager.Error($"人脸识别异常: {e.Message}", "Face");
            return null;
        }
#else
        LoggerManager.Warning("仅在 Android 设备上可用！", "Face");
        return null;
#endif
    }

    /// <summary>
    /// 人脸特征提取
    /// </summary>
    /// <param name="imageBytes">图像字节数据</param>
    /// <returns>人脸特征向量</returns>
    public float[] ExtractFeatures(byte[] imageBytes)
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (!isInitialized)
        {
            LoggerManager.Warning("SDK 未初始化！", "Face");
            return null;
        }

        try
        {
            // 获取特征向量
            AndroidJavaObject featureArray = rkfaceInstance.Call<AndroidJavaObject>("extractFeatures", imageBytes);

            if (featureArray != null)
            {
                // 转换为 float[]
                float[] features = AndroidJNIHelper.ConvertFromJNIArray<float[]>(featureArray.GetRawObject());
                LoggerManager.Debug($"提取特征维度: {features.Length}", "Face");
                return features;
            }
        }
        catch (Exception e)
        {
            LoggerManager.Error($"特征提取异常: {e.Message}", "Face");
        }
#else
        LoggerManager.Warning("仅在 Android 设备上可用！", "Face");
#endif
        return null;
    }

    /// <summary>
    /// 获取 SDK 版本信息
    /// </summary>
    public string GetSDKVersion()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (rkfaceInstance != null)
            {
                string version = rkfaceInstance.Call<string>("getVersion");
                LoggerManager.Debug($"SDK 版本: {version}", "Face");
                return version;
            }
        }
        catch (Exception e)
        {
            LoggerManager.Error($"获取版本异常: {e.Message}", "Face");
        }
#endif
        return "N/A";
    }

    /// <summary>
    /// 释放 SDK 资源
    /// </summary>
    public void Release()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            if (rkfaceInstance != null)
            {
                rkfaceInstance.Call("release");
                LoggerManager.Debug("SDK 资源已释放", "Face");
                isInitialized = false;
            }
        }
        catch (Exception e)
        {
            LoggerManager.Error($"释放资源异常: {e.Message}", "Face");
        }
#endif
    }

    void OnDestroy()
    {
        // 应用退出时释放资源
        Release();
    }

    void OnApplicationQuit()
    {
        // 应用退出时释放资源
        Release();
    }

    // 公共属性：检查初始化状态
    public bool IsInitialized => isInitialized;
}
