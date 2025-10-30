using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RKFace SDK 测试示例
/// 演示如何使用 RKFaceManager 进行人脸检测和识别
/// </summary>
public class RKFaceExample : MonoBehaviour
{
    [Header("References")]
    [Tooltip("RKFace 管理器引用")]
    public RKFaceManager rkfaceManager;

    [Tooltip("显示结果的 UI Text")]
    public Text resultText;

    [Tooltip("显示状态的 UI Text")]
    public Text statusText;

    [Header("Camera Settings")]
    [Tooltip("用于捕获图像的 WebCamTexture")]
    private WebCamTexture webCamTexture;

    [Tooltip("摄像头设备名称（留空使用默认摄像头）")]
    public string cameraDeviceName = "";

    [Tooltip("摄像头分辨率宽度")]
    public int cameraWidth = 1280;

    [Tooltip("摄像头分辨率高度")]
    public int cameraHeight = 720;

    [Tooltip("是否自动开始捕获")]
    public bool startCameraOnStart = true;

    void Start()
    {
        // 查找 RKFaceManager（如果未指定）
        if (rkfaceManager == null)
        {
            rkfaceManager = FindObjectOfType<RKFaceManager>();
            if (rkfaceManager == null)
            {
                Debug.LogError("[RKFaceExample] 场景中未找到 RKFaceManager！");
                UpdateStatus("错误：未找到 RKFaceManager");
                return;
            }
        }

        // 初始化摄像头
        if (startCameraOnStart)
        {
            StartCamera();
        }

        UpdateStatus("就绪");
    }

    /// <summary>
    /// 启动摄像头
    /// </summary>
    public void StartCamera()
    {
        try
        {
            if (string.IsNullOrEmpty(cameraDeviceName))
            {
                webCamTexture = new WebCamTexture(cameraWidth, cameraHeight);
            }
            else
            {
                webCamTexture = new WebCamTexture(cameraDeviceName, cameraWidth, cameraHeight);
            }

            webCamTexture.Play();
            UpdateStatus("摄像头已启动");
            Debug.Log("[RKFaceExample] 摄像头已启动");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[RKFaceExample] 启动摄像头失败: {e.Message}");
            UpdateStatus($"摄像头启动失败: {e.Message}");
        }
    }

    /// <summary>
    /// 停止摄像头
    /// </summary>
    public void StopCamera()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
            UpdateStatus("摄像头已停止");
            Debug.Log("[RKFaceExample] 摄像头已停止");
        }
    }

    /// <summary>
    /// 捕获当前帧并进行人脸检测
    /// </summary>
    public void DetectFaceFromCamera()
    {
        if (!rkfaceManager.IsInitialized)
        {
            UpdateStatus("RKFace SDK 未初始化");
            UpdateResult("请先初始化 SDK");
            return;
        }

        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            UpdateStatus("摄像头未运行");
            UpdateResult("请先启动摄像头");
            return;
        }

        UpdateStatus("正在检测人脸...");

        // 从 WebCamTexture 获取图像数据
        Texture2D snapshot = new Texture2D(webCamTexture.width, webCamTexture.height);
        snapshot.SetPixels(webCamTexture.GetPixels());
        snapshot.Apply();

        // 转换为字节数组（JPEG 格式）
        byte[] imageBytes = snapshot.EncodeToJPG(75);
        Destroy(snapshot); // 释放临时纹理

        // 调用人脸检测
        string result = rkfaceManager.DetectFace(imageBytes);

        if (!string.IsNullOrEmpty(result))
        {
            UpdateStatus("检测完成");
            UpdateResult($"检测结果:\n{result}");
        }
        else
        {
            UpdateStatus("检测失败");
            UpdateResult("未检测到人脸或发生错误");
        }
    }

    /// <summary>
    /// 捕获当前帧并进行人脸识别
    /// </summary>
    public void RecognizeFaceFromCamera()
    {
        if (!rkfaceManager.IsInitialized)
        {
            UpdateStatus("RKFace SDK 未初始化");
            return;
        }

        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            UpdateStatus("摄像头未运行");
            return;
        }

        UpdateStatus("正在识别人脸...");

        Texture2D snapshot = new Texture2D(webCamTexture.width, webCamTexture.height);
        snapshot.SetPixels(webCamTexture.GetPixels());
        snapshot.Apply();

        byte[] imageBytes = snapshot.EncodeToJPG(75);
        Destroy(snapshot);

        string result = rkfaceManager.RecognizeFace(imageBytes);

        if (!string.IsNullOrEmpty(result))
        {
            UpdateStatus("识别完成");
            UpdateResult($"识别结果:\n{result}");
        }
        else
        {
            UpdateStatus("识别失败");
            UpdateResult("识别失败或发生错误");
        }
    }

    /// <summary>
    /// 提取人脸特征
    /// </summary>
    public void ExtractFeaturesFromCamera()
    {
        if (!rkfaceManager.IsInitialized)
        {
            UpdateStatus("RKFace SDK 未初始化");
            return;
        }

        if (webCamTexture == null || !webCamTexture.isPlaying)
        {
            UpdateStatus("摄像头未运行");
            return;
        }

        UpdateStatus("正在提取特征...");

        Texture2D snapshot = new Texture2D(webCamTexture.width, webCamTexture.height);
        snapshot.SetPixels(webCamTexture.GetPixels());
        snapshot.Apply();

        byte[] imageBytes = snapshot.EncodeToJPG(75);
        Destroy(snapshot);

        float[] features = rkfaceManager.ExtractFeatures(imageBytes);

        if (features != null && features.Length > 0)
        {
            UpdateStatus("特征提取完成");
            int previewCount = Mathf.Min(5, features.Length);
            float[] preview = new float[previewCount];
            System.Array.Copy(features, 0, preview, 0, previewCount);
            UpdateResult($"特征向量维度: {features.Length}\n前5个值: {string.Join(", ", System.Array.ConvertAll(preview, f => f.ToString("F4")))}...");
        }
        else
        {
            UpdateStatus("特征提取失败");
            UpdateResult("无法提取特征");
        }
    }

    /// <summary>
    /// 获取并显示 SDK 版本
    /// </summary>
    public void ShowSDKVersion()
    {
        string version = rkfaceManager.GetSDKVersion();
        UpdateResult($"RKFace SDK 版本: {version}");
        UpdateStatus("已获取版本信息");
    }

    /// <summary>
    /// 更新状态文本
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = $"状态: {message}";
        }
        Debug.Log($"[RKFaceExample] 状态: {message}");
    }

    /// <summary>
    /// 更新结果文本
    /// </summary>
    private void UpdateResult(string message)
    {
        if (resultText != null)
        {
            resultText.text = message;
        }
        Debug.Log($"[RKFaceExample] 结果: {message}");
    }

    void OnDestroy()
    {
        StopCamera();
    }

    // Unity Editor 中的测试按钮
#if UNITY_EDITOR
    [ContextMenu("测试: 检测人脸")]
    void TestDetectFace()
    {
        DetectFaceFromCamera();
    }

    [ContextMenu("测试: 识别人脸")]
    void TestRecognizeFace()
    {
        RecognizeFaceFromCamera();
    }

    [ContextMenu("测试: 提取特征")]
    void TestExtractFeatures()
    {
        ExtractFeaturesFromCamera();
    }

    [ContextMenu("测试: 显示版本")]
    void TestShowVersion()
    {
        ShowSDKVersion();
    }
#endif
}
