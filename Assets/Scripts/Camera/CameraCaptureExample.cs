using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// CameraCapture 使用示例
/// 演示如何使用摄像头捕获并处理图片
/// </summary>
public class CameraCaptureExample : MonoBehaviour
{
    [Header("组件引用")]
    public CameraCapture cameraCapture;

    [Header("UI 显示")]
    public Text statusText;
    public Text infoText;

    void Start()
    {
        // 如果未指定，自动查找 CameraCapture
        if (cameraCapture == null)
        {
            cameraCapture = GetComponent<CameraCapture>();
            if (cameraCapture == null)
            {
                cameraCapture = FindObjectOfType<CameraCapture>();
            }
        }

        if (cameraCapture == null)
        {
            LoggerManager.Error("未找到 CameraCapture 组件", "Camera");
            return;
        }

        // 订阅事件
        SubscribeToEvents();

        UpdateStatus("摄像头已就绪");
    }

    void Update()
    {
        // 更新信息显示
        UpdateInfo();

        // 示例：按 C 键手动捕获一张图片
        if (Input.GetKeyDown(KeyCode.C))
        {
            ManualCapture();
        }

        // 示例：按 S 键保存当前图片
        if (Input.GetKeyDown(KeyCode.S))
        {
            SaveCurrentImage();
        }
    }

    #region 事件订阅

    /// <summary>
    /// 订阅摄像头捕获事件
    /// </summary>
    void SubscribeToEvents()
    {
        // 方式1：接收 Texture2D
        cameraCapture.OnImageCaptured += OnImageCaptured;

        // 方式2：接收字节数组（适合发送到服务器或SDK）
        cameraCapture.OnImageBytesCaptured += OnImageBytesCaptured;

        // 方式3：接收原始像素数据
        cameraCapture.OnPixelsCaptured += OnPixelsCaptured;
    }

    void OnDestroy()
    {
        if (cameraCapture != null)
        {
            cameraCapture.OnImageCaptured -= OnImageCaptured;
            cameraCapture.OnImageBytesCaptured -= OnImageBytesCaptured;
            cameraCapture.OnPixelsCaptured -= OnPixelsCaptured;
        }
    }

    #endregion

    #region 事件处理

    /// <summary>
    /// 当捕获到新图片（Texture2D）时调用
    /// </summary>
    void OnImageCaptured(Texture2D image)
    {
        // LoggerManager.Debug($"收到新图片: {image.width}x{image.height}", "Camera");

        // 在这里可以对图片进行处理
        // 例如：应用滤镜、人脸检测、特征提取等
    }

    /// <summary>
    /// 当捕获到新图片（字节数组）时调用
    /// </summary>
    void OnImageBytesCaptured(byte[] imageBytes)
    {
        // LoggerManager.Debug($"收到新图片字节数组: {imageBytes.Length / 1024}KB", "Camera");

        // 示例：发送到人脸识别 SDK
        // ProcessWithRKFace(imageBytes);
    }

    /// <summary>
    /// 当捕获到新像素数据时调用
    /// </summary>
    void OnPixelsCaptured(Color32[] pixels)
    {
        // 这是最快的方式，直接处理像素数据
        // 适合需要高性能的图像处理
    }

    #endregion

    #region 示例：与 RKFace 集成

    /// <summary>
    /// 示例：将捕获的图片发送给 RKFace SDK 进行人脸检测
    /// </summary>
    void ProcessWithRKFace(byte[] imageBytes)
    {
        // 取消注释以下代码以启用 RKFace 集成
        /*
        RKFaceManager rkFaceManager = FindObjectOfType<RKFaceManager>();
        if (rkFaceManager != null)
        {
            // 人脸检测
            string detectResult = rkFaceManager.DetectFace(imageBytes);
            Debug.Log($"人脸检测结果: {detectResult}");

            // 人脸识别
            string recognizeResult = rkFaceManager.RecognizeFace(imageBytes);
            Debug.Log($"人脸识别结果: {recognizeResult}");

            // 特征提取
            float[] features = rkFaceManager.ExtractFeatures(imageBytes);
            if (features != null)
            {
                Debug.Log($"提取到 {features.Length} 维特征向量");
            }
        }
        */
    }

    #endregion

    #region UI 按钮方法

    /// <summary>
    /// 手动捕获一张图片
    /// </summary>
    public void ManualCapture()
    {
        if (cameraCapture == null)
            return;

        Texture2D image = cameraCapture.CaptureImage();
        if (image != null)
        {
            LoggerManager.Debug($"手动捕获图片成功: {image.width}x{image.height}", "Camera");
            UpdateStatus("已捕获图片");

            // 处理完后记得销毁
            Destroy(image);
        }
    }

    /// <summary>
    /// 保存当前图片到本地
    /// </summary>
    public void SaveCurrentImage()
    {
        if (cameraCapture == null)
            return;

        byte[] imageBytes = cameraCapture.CaptureImageAsJPG(90);
        if (imageBytes != null)
        {
            string filename = $"capture_{System.DateTime.Now:yyyyMMdd_HHmmss}.jpg";
            string path = System.IO.Path.Combine(Application.persistentDataPath, filename);

            try
            {
                System.IO.File.WriteAllBytes(path, imageBytes);
                LoggerManager.Info($"图片已保存到: {path}", "Camera");
                UpdateStatus($"图片已保存: {filename}");
            }
            catch (System.Exception e)
            {
                LoggerManager.Error($"保存图片失败: {e.Message}", "Camera");
                UpdateStatus("保存失败");
            }
        }
    }

    /// <summary>
    /// 开始摄像头
    /// </summary>
    public void StartCamera()
    {
        if (cameraCapture != null)
        {
            cameraCapture.StartCamera();
            UpdateStatus("摄像头已启动");
        }
    }

    /// <summary>
    /// 停止摄像头
    /// </summary>
    public void StopCamera()
    {
        if (cameraCapture != null)
        {
            cameraCapture.StopCamera();
            UpdateStatus("摄像头已停止");
        }
    }

    /// <summary>
    /// 暂停摄像头
    /// </summary>
    public void PauseCamera()
    {
        if (cameraCapture != null)
        {
            cameraCapture.PauseCamera();
            UpdateStatus("摄像头已暂停");
        }
    }

    /// <summary>
    /// 恢复摄像头
    /// </summary>
    public void ResumeCamera()
    {
        if (cameraCapture != null)
        {
            cameraCapture.ResumeCamera();
            UpdateStatus("摄像头已恢复");
        }
    }

    #endregion

    #region UI 更新

    /// <summary>
    /// 更新状态文本
    /// </summary>
    void UpdateStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
        LoggerManager.Debug($"{message}", "Camera");
    }

    /// <summary>
    /// 更新信息显示
    /// </summary>
    void UpdateInfo()
    {
        if (infoText == null || cameraCapture == null)
            return;

        if (cameraCapture.IsRunning())
        {
            Vector2Int resolution = cameraCapture.GetResolution();
            infoText.text = $"摄像头运行中\n分辨率: {resolution.x}x{resolution.y}\n按 C 键捕获\n按 S 键保存";
        }
        else
        {
            infoText.text = "摄像头未运行";
        }
    }

    #endregion

    #region 高级示例

    /// <summary>
    /// 示例：定时捕获并处理
    /// </summary>
    private float processTimer = 0f;
    private float processInterval = 1f; // 每秒处理一次

    void ProcessTimedCapture()
    {
        processTimer += Time.deltaTime;
        if (processTimer >= processInterval)
        {
            processTimer = 0f;

            // 获取最新的图片数据
            byte[] imageBytes = cameraCapture.GetLatestImageBytes();
            if (imageBytes != null)
            {
                // 处理图片
                ProcessImage(imageBytes);
            }
        }
    }

    /// <summary>
    /// 处理图片
    /// </summary>
    void ProcessImage(byte[] imageBytes)
    {
        // 在这里添加你的图片处理逻辑
        // 例如：人脸检测、OCR、物体识别等
        LoggerManager.Debug($"处理图片，大小: {imageBytes.Length / 1024}KB", "Camera");
    }

    #endregion
}
