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
        LoggerManager.Debug("仅在 Android 设备上运行诊断", "TTS");
        #endif
    }

    private void DiagnoseSDK()
    {
        LoggerManager.Info("========== RKTTS SDK 诊断开始 ==========", "TTS");

        // 1. 检查 Unity Activity
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                if (activity != null)
                {
                    LoggerManager.Info("✅ [1/5] Unity Activity 获取成功", "TTS");
                }
                else
                {
                    LoggerManager.Error("❌ [1/5] Unity Activity 为 null", "TTS");
                }
            }
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"❌ [1/5] 获取 Unity Activity 失败: {e.Message}", "TTS");
        }

        // 2. 检查 RKTTS 主类
        CheckClass("com.sensetime.rktts.SenseRKTtsDetector", "[2/5] RKTTS 主类");

        // 3. 检查 OnResultListener 接口
        CheckClass("com.sensetime.rktts.OnResultListener", "[3/5] OnResultListener 接口");

        // 4. 尝试获取 TTS 实例
        try
        {
            using (AndroidJavaClass ttsClass = new AndroidJavaClass("com.sensetime.rktts.SenseRKTtsDetector"))
            {
                // RKTTS SDK 使用无参数的 getInstance()，类似 RKFace 和 RKLLM SDK
                AndroidJavaObject ttsInstance = ttsClass.CallStatic<AndroidJavaObject>("getInstance");

                if (ttsInstance != null)
                {
                    LoggerManager.Info("✅ [4/5] RKTTS getInstance() 调用成功 (无参数)", "TTS");

                    // 尝试调用方法
                    try
                    {
                        ttsInstance.Call("initialize");
                        LoggerManager.Info("   ✅ initialize() 方法调用成功", "TTS");
                    }
                    catch (System.Exception e)
                    {
                        LoggerManager.Error($"   ❌ initialize() 调用失败: {e.Message}", "TTS");
                    }
                }
                else
                {
                    LoggerManager.Error("❌ [4/5] RKTTS getInstance 返回 null", "TTS");
                }
            }
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"❌ [4/5] 获取 RKTTS 实例失败: {e.Message}", "TTS");
        }

        // 5. 检查权限
        CheckPermissions();

        LoggerManager.Info("========== RKTTS SDK 诊断结束 ==========", "TTS");
    }

    private void CheckClass(string className, string description)
    {
        try
        {
            using (AndroidJavaClass javaClass = new AndroidJavaClass(className))
            {
                if (javaClass != null)
                {
                    LoggerManager.Info($"✅ {description}: 找到类 {className}", "TTS");
                }
                else
                {
                    LoggerManager.Error($"❌ {description}: 类 {className} 为 null", "TTS");
                }
            }
        }
        catch (System.Exception e)
        {
            LoggerManager.Error($"❌ {description}: 找不到类 {className}\n   错误: {e.Message}", "TTS");
        }
    }

    private void CheckPermissions()
    {
        LoggerManager.Info("========== [5/5] 权限检查 ==========", "TTS");

        #if UNITY_ANDROID && !UNITY_EDITOR
        bool hasStorage = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
            UnityEngine.Android.Permission.ExternalStorageRead
        );
        bool hasAudio = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
            UnityEngine.Android.Permission.Microphone
        );

        if (hasStorage)
            LoggerManager.Info("   ✅ 存储权限已授予", "TTS");
        else
            LoggerManager.Warning("   ⚠️ 存储权限未授予", "TTS");

        if (hasAudio)
            LoggerManager.Info("   ✅ 音频权限已授予", "TTS");
        else
            LoggerManager.Warning("   ⚠️ 音频权限未授予", "TTS");

        if (hasStorage && hasAudio)
            LoggerManager.Info("✅ [5/5] 所有权限已授予", "TTS");
        else
            LoggerManager.Warning("⚠️ [5/5] 部分权限未授予", "TTS");
        #endif
    }
}
