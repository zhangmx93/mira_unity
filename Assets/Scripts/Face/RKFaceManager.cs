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
        Debug.Log("RKFaceManager: Awake() 被调用");

        // 延迟加载模式：初始时禁用，等待 SDKLoader 启用
        enabled = false;
        Debug.Log("RKFaceManager: 已禁用，等待 SDKLoader 延迟加载");
    }

    void OnEnable()
    {
        Debug.Log("RKFaceManager: OnEnable() 被调用 - 开始初始化");

        // 被 SDKLoader 启用时才开始初始化流程
#if UNITY_ANDROID && !UNITY_EDITOR
        Debug.Log("RKFaceManager: 检测到 Android 平台，准备初始化...");

        // 请求存储权限（SDK需要访问模型文件）
        RequestStoragePermissions();

        // 延迟初始化，等待权限授予
        StartCoroutine(InitializeAfterPermissions());
#else
        if (enableDebugLog)
            Debug.LogWarning("RKFaceManager: 当前平台不支持 RKFace (仅支持 Android)");
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
            Debug.Log("RKFaceManager: 请求读取存储权限...");
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.ExternalStorageRead);
        }

        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.ExternalStorageWrite))
        {
            Debug.Log("RKFaceManager: 请求写入存储权限...");
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
                Debug.Log("RKFaceManager: 存储权限已授予，开始初始化");
                InitializeRKFace();
                yield break;
            }

            yield return new WaitForSeconds(0.5f);
            elapsed += 0.5f;
        }

        // 超时或用户拒绝权限
        Debug.LogWarning("RKFaceManager: 未获得存储权限，尝试继续初始化（可能失败）");
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
                Debug.Log("RKFaceManager: 开始初始化 RKFace SDK...");

            // 步骤 1: 获取当前 Activity
            if (enableDebugLog)
                Debug.Log("RKFaceManager: [1/3] 获取 Unity Activity...");

            unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            if (currentActivity == null)
            {
                Debug.LogError("RKFaceManager: 无法获取 Unity Activity");
                return;
            }

            if (enableDebugLog)
                Debug.Log("RKFaceManager: [1/3] ✅ Unity Activity 获取成功");

            // 步骤 2: 创建 RKFace 实例
            if (enableDebugLog)
                Debug.Log("RKFaceManager: [2/3] 创建 RKFaceSDK 实例...");

            AndroidJavaClass rkfaceClass = new AndroidJavaClass("com.senseflow.rkface.RKFaceSDK");
            rkfaceInstance = rkfaceClass.CallStatic<AndroidJavaObject>("getInstance");

            if (rkfaceInstance == null)
            {
                Debug.LogError("RKFaceManager: 无法获取 RKFace SDK 实例");
                return;
            }

            if (enableDebugLog)
                Debug.Log("RKFaceManager: [2/3] ✅ RKFaceSDK 实例创建成功");

            // 步骤 3: 初始化 SDK
            if (enableDebugLog)
                Debug.Log("RKFaceManager: [3/3] 初始化 SDK...");

            bool success = rkfaceInstance.Call<bool>("init", currentActivity, modelPath);

            if (success)
            {
                isInitialized = true;
                if (enableDebugLog)
                    Debug.Log("RKFaceManager: ✅ RKFace SDK 初始化成功！");
            }
            else
            {
                Debug.LogError("RKFaceManager: RKFace SDK 初始化失败！");
                isInitialized = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"RKFaceManager: 初始化异常 - {e.Message}\n{e.StackTrace}");
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
            Debug.LogWarning("[RKFace] SDK 未初始化，请先调用 InitializeRKFace()");
            return null;
        }

        try
        {
            // 调用人脸检测方法
            // 注意：实际的方法名和参数需要根据 RKFace SDK 的文档进行调整
            string result = rkfaceInstance.Call<string>("detectFace", imageBytes);
            Debug.Log($"[RKFace] 人脸检测结果: {result}");
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RKFace] 人脸检测异常: {e.Message}");
            return null;
        }
#else
        Debug.LogWarning("[RKFace] 仅在 Android 设备上可用！");
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
            Debug.LogWarning("[RKFace] SDK 未初始化！");
            return null;
        }

        try
        {
            string result = rkfaceInstance.Call<string>("recognizeFace", imageBytes);
            Debug.Log($"[RKFace] 人脸识别结果: {result}");
            return result;
        }
        catch (Exception e)
        {
            Debug.LogError($"[RKFace] 人脸识别异常: {e.Message}");
            return null;
        }
#else
        Debug.LogWarning("[RKFace] 仅在 Android 设备上可用！");
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
            Debug.LogWarning("[RKFace] SDK 未初始化！");
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
                Debug.Log($"[RKFace] 提取特征维度: {features.Length}");
                return features;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RKFace] 特征提取异常: {e.Message}");
        }
#else
        Debug.LogWarning("[RKFace] 仅在 Android 设备上可用！");
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
                Debug.Log($"[RKFace] SDK 版本: {version}");
                return version;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RKFace] 获取版本异常: {e.Message}");
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
                Debug.Log("[RKFace] SDK 资源已释放");
                isInitialized = false;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RKFace] 释放资源异常: {e.Message}");
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
