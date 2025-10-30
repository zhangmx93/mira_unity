using UnityEngine;
using System.Collections;

/// <summary>
/// 权限管理器
/// 统一管理和请求所有权限（摄像头、麦克风等）
/// 确保在Android/iOS上依次请求，避免冲突
/// </summary>
public class PermissionManager : MonoBehaviour
{
    [Header("权限设置")]
    [Tooltip("是否在启动时自动请求所有权限")]
    public bool requestOnStart = true;

    [Tooltip("是否请求摄像头权限")]
    public bool requestCamera = true;

    [Tooltip("是否请求麦克风权限")]
    public bool requestMicrophone = true;

    [Header("调试")]
    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    // 权限状态
    private bool cameraGranted = false;
    private bool microphoneGranted = false;
    private bool allPermissionsGranted = false;

    // 单例
    private static PermissionManager instance;
    public static PermissionManager Instance
    {
        get { return instance; }
    }

    void Awake()
    {
        // 单例模式
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        if (requestOnStart)
        {
            StartCoroutine(RequestAllPermissions());
        }
    }

    /// <summary>
    /// 请求所有权限（依次进行）
    /// </summary>
    public IEnumerator RequestAllPermissions()
    {
        if (enableDebugLog)
            Debug.Log("PermissionManager: 开始请求权限...");

        #if UNITY_ANDROID || UNITY_IOS
        // Android/iOS 需要运行时请求权限

        // 1. 请求麦克风权限
        if (requestMicrophone)
        {
            yield return StartCoroutine(RequestMicrophonePermission());
        }

        // 2. 等待一小段时间，确保上一个权限对话框完全关闭
        if (enableDebugLog)
            Debug.Log("PermissionManager: 等待 0.5 秒后请求摄像头权限...");
        yield return new WaitForSeconds(0.5f);

        // 3. 请求摄像头权限
        if (enableDebugLog)
            Debug.Log($"PermissionManager: requestCamera = {requestCamera}");

        if (requestCamera)
        {
            if (enableDebugLog)
                Debug.Log("PermissionManager: 开始调用 RequestCameraPermission()...");
            yield return StartCoroutine(RequestCameraPermission());
            if (enableDebugLog)
                Debug.Log("PermissionManager: RequestCameraPermission() 完成");
        }
        else
        {
            if (enableDebugLog)
                Debug.Log("PermissionManager: requestCamera = false, 跳过摄像头权限请求");
        }

        // 检查所有权限是否都已授予
        CheckAllPermissions();

        if (allPermissionsGranted)
        {
            if (enableDebugLog)
                Debug.Log("PermissionManager: ✅ 所有权限已授予");
        }
        else
        {
            Debug.LogWarning("PermissionManager: ⚠️ 部分权限未授予");
            if (!microphoneGranted && requestMicrophone)
                Debug.LogWarning("  - 麦克风权限未授予");
            if (!cameraGranted && requestCamera)
                Debug.LogWarning("  - 摄像头权限未授予");
        }

        #else
        // macOS/Windows 等平台
        // 权限通过首次访问设备时的系统弹窗获得
        if (enableDebugLog)
            Debug.Log("PermissionManager: 当前平台无需运行时权限请求");

        // 直接标记为已授予（实际权限在访问设备时处理）
        cameraGranted = requestCamera;
        microphoneGranted = requestMicrophone;
        allPermissionsGranted = true;

        yield return null;
        #endif
    }

    /// <summary>
    /// 请求麦克风权限
    /// </summary>
    private IEnumerator RequestMicrophonePermission()
    {
        #if UNITY_ANDROID || UNITY_IOS
        if (enableDebugLog)
            Debug.Log("PermissionManager: 请求麦克风权限...");

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }

        microphoneGranted = Application.HasUserAuthorization(UserAuthorization.Microphone);

        if (microphoneGranted)
        {
            if (enableDebugLog)
                Debug.Log("PermissionManager: ✅ 麦克风权限已授予");
        }
        else
        {
            Debug.LogError("PermissionManager: ❌ 麦克风权限被拒绝");
        }
        #else
        microphoneGranted = true;
        yield return null;
        #endif
    }

    /// <summary>
    /// 请求摄像头权限
    /// </summary>
    private IEnumerator RequestCameraPermission()
    {
        #if UNITY_ANDROID || UNITY_IOS
        if (enableDebugLog)
            Debug.Log("PermissionManager: >>> 进入 RequestCameraPermission 方法");

        bool hasPermissionBefore = Application.HasUserAuthorization(UserAuthorization.WebCam);
        if (enableDebugLog)
            Debug.Log($"PermissionManager: 摄像头权限检查 - 当前状态: {hasPermissionBefore}");

        if (!hasPermissionBefore)
        {
            if (enableDebugLog)
                Debug.Log("PermissionManager: 准备调用 RequestUserAuthorization(WebCam)...");

            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);

            if (enableDebugLog)
                Debug.Log("PermissionManager: RequestUserAuthorization(WebCam) 调用完成");
        }
        else
        {
            if (enableDebugLog)
                Debug.Log("PermissionManager: 摄像头权限已存在，跳过请求");
        }

        cameraGranted = Application.HasUserAuthorization(UserAuthorization.WebCam);

        if (enableDebugLog)
            Debug.Log($"PermissionManager: 摄像头权限最终状态: {cameraGranted}");

        if (cameraGranted)
        {
            if (enableDebugLog)
                Debug.Log("PermissionManager: ✅ 摄像头权限已授予");
        }
        else
        {
            Debug.LogError("PermissionManager: ❌ 摄像头权限被拒绝");
        }
        #else
        cameraGranted = true;
        yield return null;
        #endif
    }

    /// <summary>
    /// 检查所有权限状态
    /// </summary>
    private void CheckAllPermissions()
    {
        bool allGranted = true;

        if (requestMicrophone && !microphoneGranted)
            allGranted = false;

        if (requestCamera && !cameraGranted)
            allGranted = false;

        allPermissionsGranted = allGranted;
    }

    #region 公开API - 查询权限状态

    /// <summary>
    /// 检查摄像头权限是否已授予
    /// </summary>
    public bool IsCameraGranted()
    {
        #if UNITY_ANDROID || UNITY_IOS
        return Application.HasUserAuthorization(UserAuthorization.WebCam);
        #else
        return true;
        #endif
    }

    /// <summary>
    /// 检查麦克风权限是否已授予
    /// </summary>
    public bool IsMicrophoneGranted()
    {
        #if UNITY_ANDROID || UNITY_IOS
        return Application.HasUserAuthorization(UserAuthorization.Microphone);
        #else
        return true;
        #endif
    }

    /// <summary>
    /// 检查所有权限是否都已授予
    /// </summary>
    public bool AreAllPermissionsGranted()
    {
        return allPermissionsGranted;
    }

    /// <summary>
    /// 手动请求所有权限
    /// </summary>
    public void RequestPermissions()
    {
        StartCoroutine(RequestAllPermissions());
    }

    #endregion
}
