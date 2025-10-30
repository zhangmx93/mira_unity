using UnityEngine;
using System;

/// <summary>
/// SenseFlow RKFace SDK 使用示例
/// 此脚本演示如何在 Unity 中调用 Android 原生的 RKFace SDK
/// </summary>
public class RKFaceManager : MonoBehaviour
{
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject rkfaceInstance;
    private AndroidJavaClass unityPlayer;
    private AndroidJavaObject currentActivity;
#endif

    [Header("RKFace Settings")]
    [Tooltip("是否在启动时初始化 RKFace SDK")]
    public bool initializeOnStart = true;

    [Tooltip("RKFace 模型路径（相对于 StreamingAssets）")]
    public string modelPath = "rkface_model";

    private bool isInitialized = false;

    void Start()
    {
        if (initializeOnStart)
        {
            InitializeRKFace();
        }
    }

    /// <summary>
    /// 初始化 RKFace SDK
    /// </summary>
    public void InitializeRKFace()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            Debug.Log("[RKFace] 开始初始化 RKFace SDK...");

            // 获取当前 Activity
            unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

            // 创建 RKFace 实例
            // 注意：实际的类名需要根据 RKFace SDK 的文档进行调整
            AndroidJavaClass rkfaceClass = new AndroidJavaClass("com.senseflow.rkface.RKFaceSDK");
            rkfaceInstance = rkfaceClass.CallStatic<AndroidJavaObject>("getInstance");

            if (rkfaceInstance != null)
            {
                // 初始化 SDK
                bool success = rkfaceInstance.Call<bool>("init", currentActivity, modelPath);

                if (success)
                {
                    isInitialized = true;
                    Debug.Log("[RKFace] RKFace SDK 初始化成功！");
                }
                else
                {
                    Debug.LogError("[RKFace] RKFace SDK 初始化失败！");
                }
            }
            else
            {
                Debug.LogError("[RKFace] 无法获取 RKFace SDK 实例！");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[RKFace] 初始化异常: {e.Message}\n{e.StackTrace}");
        }
#else
        Debug.LogWarning("[RKFace] RKFace SDK 仅在 Android 设备上可用！");
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
