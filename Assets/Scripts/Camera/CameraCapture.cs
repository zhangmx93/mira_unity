using UnityEngine;
using UnityEngine.UI;
using System;

/// <summary>
/// 摄像头捕获管理器
/// 用于打开摄像头并持续获取图片数据
/// </summary>
public class CameraCapture : MonoBehaviour
{
    [Header("摄像头设置")]
    [Tooltip("目标摄像头设备名称，留空则使用默认摄像头")]
    public string targetCameraName = "";

    [Tooltip("摄像头分辨率宽度")]
    public int requestedWidth = 1080;

    [Tooltip("摄像头分辨率高度")]
    public int requestedHeight = 1920;

    [Tooltip("目标帧率")]
    public int requestedFPS = 30;

    [Header("显示设置")]
    [Tooltip("用于显示摄像头画面的RawImage组件")]
    public RawImage displayImage;

    [Tooltip("是否镜像显示")]
    public bool mirrorDisplay = false;

    [Tooltip("是否自动调整显示尺寸")]
    public bool autoAdjustSize = true;

    [Tooltip("显示尺寸缩放比例（相对于摄像头分辨率）")]
    [Range(0.05f, 2.0f)]
    public float displayScale = 0.1f;

    [Header("捕获设置")]
    [Tooltip("图片捕获间隔（秒），0表示每帧捕获")]
    public float captureInterval = 0.1f;

    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    // WebCamTexture 对象
    private WebCamTexture webCamTexture;

    // 捕获计时器
    private float captureTimer = 0f;

    // 摄像头是否正在运行
    private bool isRunning = false;

    // 最新捕获的图片数据
    private Texture2D latestCapturedImage;
    private byte[] latestImageBytes;
    private Color32[] latestPixels;

    // 事件：当新图片捕获时触发
    public event Action<Texture2D> OnImageCaptured;
    public event Action<byte[]> OnImageBytesCaptured;
    public event Action<Color32[]> OnPixelsCaptured;

    #region Unity 生命周期

    void Start()
    {
        // 如果有PermissionManager，等待它完成权限请求
        // 否则自己请求权限
        PermissionManager permissionManager = FindObjectOfType<PermissionManager>();
        if (permissionManager != null)
        {
            StartCoroutine(WaitForPermissionManager());
        }
        else
        {
            StartCoroutine(RequestCameraPermissionAndStart());
        }
    }

    /// <summary>
    /// 等待PermissionManager完成权限请求
    /// </summary>
    private System.Collections.IEnumerator WaitForPermissionManager()
    {
        PermissionManager permissionManager = PermissionManager.Instance;

        // 等待PermissionManager完成所有权限请求
        while (permissionManager != null && !permissionManager.AreAllPermissionsGranted())
        {
            yield return new WaitForSeconds(0.1f);
        }

        // 权限请求完成后，直接启动摄像头
        StartCamera();
    }

    void Update()
    {
        if (!isRunning || webCamTexture == null)
            return;

        // 更新显示
        UpdateDisplay();

        // 定时捕获图片
        if (captureInterval > 0)
        {
            captureTimer += Time.deltaTime;
            if (captureTimer >= captureInterval)
            {
                captureTimer = 0f;
                CaptureFrame();
            }
        }
        else
        {
            // 每帧捕获
            CaptureFrame();
        }
    }

    void OnDestroy()
    {
        StopCamera();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            PauseCamera();
        }
        else
        {
            ResumeCamera();
        }
    }

    #endregion

    #region 摄像头控制

    /// <summary>
    /// 请求摄像头权限并启动
    /// </summary>
    private System.Collections.IEnumerator RequestCameraPermissionAndStart()
    {
        #if UNITY_ANDROID || UNITY_IOS
        // Android/iOS: 等待一小段时间，避免与麦克风权限请求冲突
        // 如果场景中同时有CameraCapture和MicrophoneCapture，让它们依次请求权限
        yield return new WaitForSeconds(1.5f);

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.Log("CameraCapture: 请求摄像头权限...");
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        }

        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogError("CameraCapture: 摄像头权限被拒绝");
            yield break;
        }

        Debug.Log("CameraCapture: 摄像头权限已授予");
        #else
        // macOS/Windows: 直接访问设备触发权限弹窗
        int deviceCount = WebCamTexture.devices.Length;
        yield return new WaitForSeconds(0.5f);
        #endif

        // 启动摄像头
        StartCamera();
    }

    /// <summary>
    /// 启动摄像头
    /// </summary>
    public void StartCamera()
    {
        if (isRunning)
        {
            if (enableDebugLog)
                Debug.LogWarning("CameraCapture: 摄像头已在运行中");
            return;
        }

        // 检查是否有可用的摄像头
        if (WebCamTexture.devices.Length == 0)
        {
            Debug.LogError("CameraCapture: 未找到可用的摄像头设备");
            return;
        }

        // 选择摄像头
        WebCamDevice selectedDevice;
        if (!string.IsNullOrEmpty(targetCameraName))
        {
            // 使用指定的摄像头
            selectedDevice = Array.Find(WebCamTexture.devices, device => device.name == targetCameraName);
            if (string.IsNullOrEmpty(selectedDevice.name))
            {
                Debug.LogWarning($"CameraCapture: 未找到名为 '{targetCameraName}' 的摄像头，使用默认摄像头");
                selectedDevice = WebCamTexture.devices[0];
            }
        }
        else
        {
            // 使用默认摄像头（通常是后置摄像头）
            selectedDevice = WebCamTexture.devices[0];
        }

        // 创建 WebCamTexture
        webCamTexture = new WebCamTexture(selectedDevice.name, requestedWidth, requestedHeight, requestedFPS);

        // 启动摄像头
        webCamTexture.Play();
        isRunning = true;

        // 设置显示
        if (displayImage != null)
        {
            displayImage.texture = webCamTexture;

            // 调整显示方向
            if (mirrorDisplay)
            {
                displayImage.transform.localScale = new Vector3(-1, 1, 1);
            }
        }
        else
        {
            Debug.LogWarning("CameraCapture: displayImage 未设置，摄像头画面不会显示");
        }

        // 延迟检查摄像头是否真正启动并调整尺寸
        StartCoroutine(CheckCameraStatusAfterDelay());
    }

    /// <summary>
    /// 延迟检查摄像头状态
    /// </summary>
    private System.Collections.IEnumerator CheckCameraStatusAfterDelay()
    {
        yield return new WaitForSeconds(1f);

        if (webCamTexture != null)
        {
            if (webCamTexture.width <= 16)
            {
                Debug.LogWarning("CameraCapture: 摄像头分辨率异常，可能还在初始化中");
            }
            else
            {
                // 摄像头已正常初始化，根据实际分辨率调整 displayImage 尺寸
                AdjustDisplayImageSize();
            }

            if (!webCamTexture.isPlaying)
            {
                Debug.LogError("CameraCapture: 摄像头未在播放！可能没有摄像头权限或设备被占用");
            }
        }
    }

    /// <summary>
    /// 根据摄像头实际分辨率调整 displayImage 尺寸（可配置缩放比例）
    /// </summary>
    private void AdjustDisplayImageSize()
    {
        if (displayImage == null || webCamTexture == null)
            return;

        if (!autoAdjustSize)
            return;

        float targetWidth = webCamTexture.width * displayScale;
        float targetHeight = webCamTexture.height * displayScale;

        // 设置 RawImage 的尺寸
        displayImage.rectTransform.sizeDelta = new Vector2(targetWidth, targetHeight);
    }

    /// <summary>
    /// 停止摄像头
    /// </summary>
    public void StopCamera()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            isRunning = false;
        }

        if (latestCapturedImage != null)
        {
            Destroy(latestCapturedImage);
            latestCapturedImage = null;
        }
    }

    /// <summary>
    /// 暂停摄像头
    /// </summary>
    public void PauseCamera()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Pause();
        }
    }

    /// <summary>
    /// 恢复摄像头
    /// </summary>
    public void ResumeCamera()
    {
        if (webCamTexture != null && !webCamTexture.isPlaying)
        {
            webCamTexture.Play();
        }
    }

    #endregion

    #region 图片捕获

    /// <summary>
    /// 捕获当前帧
    /// </summary>
    private void CaptureFrame()
    {
        if (webCamTexture == null || !webCamTexture.didUpdateThisFrame)
            return;

        try
        {
            // 获取像素数据
            Color32[] pixels = webCamTexture.GetPixels32();
            latestPixels = pixels;

            // 触发像素数据事件
            OnPixelsCaptured?.Invoke(pixels);

            // 创建 Texture2D（如果需要）
            if (OnImageCaptured != null || OnImageBytesCaptured != null)
            {
                if (latestCapturedImage == null ||
                    latestCapturedImage.width != webCamTexture.width ||
                    latestCapturedImage.height != webCamTexture.height)
                {
                    if (latestCapturedImage != null)
                        Destroy(latestCapturedImage);

                    latestCapturedImage = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
                }

                latestCapturedImage.SetPixels32(pixels);
                latestCapturedImage.Apply();

                // 触发 Texture2D 事件
                OnImageCaptured?.Invoke(latestCapturedImage);

                // 转换为字节数组并触发事件
                if (OnImageBytesCaptured != null)
                {
                    latestImageBytes = latestCapturedImage.EncodeToJPG(90);
                    OnImageBytesCaptured?.Invoke(latestImageBytes);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"CameraCapture: 捕获帧时出错: {e.Message}");
        }
    }

    /// <summary>
    /// 手动捕获一张图片（立即捕获）
    /// </summary>
    /// <returns>捕获的图片 Texture2D</returns>
    public Texture2D CaptureImage()
    {
        if (webCamTexture == null || !isRunning)
        {
            Debug.LogError("CameraCapture: 摄像头未运行，无法捕获图片");
            return null;
        }

        Texture2D snapshot = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
        snapshot.SetPixels32(webCamTexture.GetPixels32());
        snapshot.Apply();

        return snapshot;
    }

    /// <summary>
    /// 手动捕获一张图片并转换为字节数组
    /// </summary>
    /// <param name="quality">JPG 质量 (1-100)</param>
    /// <returns>JPG 格式的字节数组</returns>
    public byte[] CaptureImageAsJPG(int quality = 90)
    {
        Texture2D image = CaptureImage();
        if (image == null)
            return null;

        byte[] bytes = image.EncodeToJPG(quality);
        Destroy(image);

        return bytes;
    }

    /// <summary>
    /// 手动捕获一张图片并转换为 PNG 字节数组
    /// </summary>
    /// <returns>PNG 格式的字节数组</returns>
    public byte[] CaptureImageAsPNG()
    {
        Texture2D image = CaptureImage();
        if (image == null)
            return null;

        byte[] bytes = image.EncodeToPNG();
        Destroy(image);

        return bytes;
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 更新显示画面
    /// </summary>
    private void UpdateDisplay()
    {
        // 显示已在 StartCamera 中设置，这里只需要确保 texture 还在
        if (displayImage != null && displayImage.texture != webCamTexture)
        {
            displayImage.texture = webCamTexture;
        }
    }

    /// <summary>
    /// 获取当前摄像头状态
    /// </summary>
    public bool IsRunning()
    {
        return isRunning && webCamTexture != null && webCamTexture.isPlaying;
    }

    /// <summary>
    /// 获取摄像头实际分辨率
    /// </summary>
    public Vector2Int GetResolution()
    {
        if (webCamTexture == null)
            return Vector2Int.zero;

        return new Vector2Int(webCamTexture.width, webCamTexture.height);
    }

    /// <summary>
    /// 设置显示缩放比例并立即更新尺寸
    /// </summary>
    /// <param name="scale">缩放比例（相对于摄像头分辨率）</param>
    public void SetDisplayScale(float scale)
    {
        displayScale = Mathf.Clamp(scale, 0.05f, 2.0f);
        if (autoAdjustSize && webCamTexture != null && webCamTexture.width > 16)
        {
            AdjustDisplayImageSize();
        }
    }

    /// <summary>
    /// 手动调整显示尺寸（不使用自动调整）
    /// </summary>
    /// <param name="width">宽度</param>
    /// <param name="height">高度</param>
    public void SetDisplaySize(float width, float height)
    {
        if (displayImage == null)
            return;

        autoAdjustSize = false;
        displayImage.rectTransform.sizeDelta = new Vector2(width, height);
    }

    /// <summary>
    /// 获取最新捕获的图片数据（Texture2D）
    /// </summary>
    public Texture2D GetLatestCapturedImage()
    {
        return latestCapturedImage;
    }

    /// <summary>
    /// 获取当前帧（实时捕获）
    /// </summary>
    public Texture2D GetCurrentFrame()
    {
        if (webCamTexture == null || !isRunning)
        {
            Debug.LogWarning("CameraCapture: 摄像头未运行，无法获取当前帧");
            return null;
        }

        // 创建新的 Texture2D 包含当前帧
        Texture2D currentFrame = new Texture2D(webCamTexture.width, webCamTexture.height, TextureFormat.RGB24, false);
        currentFrame.SetPixels32(webCamTexture.GetPixels32());
        currentFrame.Apply();

        return currentFrame;
    }

    /// <summary>
    /// 获取最新捕获的图片数据（字节数组）
    /// </summary>
    public byte[] GetLatestImageBytes()
    {
        return latestImageBytes;
    }

    /// <summary>
    /// 获取最新捕获的像素数据
    /// </summary>
    public Color32[] GetLatestPixels()
    {
        return latestPixels;
    }

    /// <summary>
    /// 获取 WebCamTexture 对象
    /// </summary>
    public WebCamTexture GetWebCamTexture()
    {
        return webCamTexture;
    }

    /// <summary>
    /// 列出所有可用的摄像头设备
    /// </summary>
    public static WebCamDevice[] GetAvailableCameras()
    {
        return WebCamTexture.devices;
    }

    #endregion
}
