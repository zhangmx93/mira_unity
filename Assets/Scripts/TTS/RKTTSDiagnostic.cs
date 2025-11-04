using UnityEngine;

/// <summary>
/// RKTTS SDK 诊断工具
/// 用于检查 Android TTS SDK 是否正确集成
/// </summary>
public class RKTTSDiagnostic : MonoBehaviour
{
    void Start()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        DiagnoseSDK();
        #else
        Debug.Log("RKTTSDiagnostic: 仅在 Android 设备上运行诊断");
        #endif
    }

    private void DiagnoseSDK()
    {
        Debug.Log("========== RKTTS SDK 诊断开始 ==========");

        // 1. 检查 Unity Activity
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity != null)
                {
                    Debug.Log("✅ [1/5] Unity Activity 获取成功");
                }
                else
                {
                    Debug.LogError("❌ [1/5] Unity Activity 为 null");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [1/5] 获取 Unity Activity 失败: {e.Message}");
        }

        // 2. 检查 RKTTS 主类
        CheckClass("com.senseflow.rktts.SenseRKTtsDetector", "[2/5] RKTTS 主类");

        // 3. 检查 OnResultListener 接口
        CheckClass("com.senseflow.rktts.OnResultListener", "[3/5] OnResultListener 接口");

        // 4. 尝试获取 TTS 实例
        try
        {
            using (AndroidJavaClass ttsClass = new AndroidJavaClass("com.senseflow.rktts.SenseRKTtsDetector"))
            {
                // RKTTS SDK 使用无参数的 getInstance()，类似 RKFace 和 RKLLM SDK
                AndroidJavaObject ttsInstance = ttsClass.CallStatic<AndroidJavaObject>("getInstance");

                if (ttsInstance != null)
                {
                    Debug.Log("✅ [4/5] RKTTS getInstance() 调用成功 (无参数)");

                    // 尝试调用方法
                    try
                    {
                        ttsInstance.Call("initialize");
                        Debug.Log("   ✅ initialize() 方法调用成功");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"   ❌ initialize() 调用失败: {e.Message}");
                    }
                }
                else
                {
                    Debug.LogError("❌ [4/5] RKTTS getInstance 返回 null");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [4/5] 获取 RKTTS 实例失败: {e.Message}");
        }

        // 5. 检查权限
        CheckPermissions();

        Debug.Log("========== RKTTS SDK 诊断结束 ==========");
    }

    private void CheckClass(string className, string description)
    {
        try
        {
            using (AndroidJavaClass javaClass = new AndroidJavaClass(className))
            {
                if (javaClass != null)
                {
                    Debug.Log($"✅ {description}: 找到类 {className}");
                }
                else
                {
                    Debug.LogError($"❌ {description}: 类 {className} 为 null");
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ {description}: 找不到类 {className}\n   错误: {e.Message}");
        }
    }

    private void CheckPermissions()
    {
        Debug.Log("========== [5/5] 权限检查 ==========");

        #if UNITY_ANDROID && !UNITY_EDITOR
        bool hasStorage = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
            UnityEngine.Android.Permission.ExternalStorageRead
        );
        bool hasAudio = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
            UnityEngine.Android.Permission.Microphone
        );

        if (hasStorage)
            Debug.Log("   ✅ 存储权限已授予");
        else
            Debug.LogWarning("   ⚠️ 存储权限未授予");

        if (hasAudio)
            Debug.Log("   ✅ 音频权限已授予");
        else
            Debug.LogWarning("   ⚠️ 音频权限未授予");

        if (hasStorage && hasAudio)
            Debug.Log("✅ [5/5] 所有权限已授予");
        else
            Debug.LogWarning("⚠️ [5/5] 部分权限未授予");
        #endif
    }
}
