using UnityEngine;

/// <summary>
/// RKLLM SDK 诊断工具
/// 用于检查 Android SDK 是否正确集成
/// </summary>
public class RKLLMDiagnostic : MonoBehaviour
{
    void Start()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        DiagnoseSDK();
        #else
        Debug.Log("RKLLMDiagnostic: 仅在 Android 设备上运行诊断");
        #endif
    }

    private void DiagnoseSDK()
    {
        Debug.Log("========== RKLLM SDK 诊断开始 ==========");

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

        // 2. 检查 RKLLM 主类
        CheckClass("com.senseflow.rkllm.SenseRKLlmDetector", "[2/5] RKLLM 主类");

        // 3. 检查 ModelConfig 类
        CheckClass("com.senseflow.rkllm.ModelConfig", "[3/5] ModelConfig 类");

        // 4. 检查 OnResultListener 接口
        CheckClass("com.senseflow.rkllm.OnResultListener", "[4/5] OnResultListener 接口");

        // 5. 尝试列出所有加载的类（如果可能）
        try
        {
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                AndroidJavaObject classLoader = activity.Call<AndroidJavaObject>("getClassLoader");
                Debug.Log($"✅ [5/5] ClassLoader 获取成功: {classLoader.Call<string>("toString")}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ [5/5] ClassLoader 获取失败: {e.Message}");
        }

        Debug.Log("========== RKLLM SDK 诊断结束 ==========");
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

                    // 尝试调用 getInstance 方法（如果存在）
                    if (className.Contains("SenseRKLlmDetector"))
                    {
                        try
                        {
                            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                            {
                                AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                                AndroidJavaObject instance = javaClass.CallStatic<AndroidJavaObject>("getInstance", activity);
                                if (instance != null)
                                {
                                    Debug.Log($"   ✅ getInstance() 方法调用成功");
                                }
                                else
                                {
                                    Debug.LogWarning($"   ⚠️ getInstance() 返回 null");
                                }
                            }
                        }
                        catch (System.Exception e)
                        {
                            Debug.LogError($"   ❌ getInstance() 调用失败: {e.Message}");
                        }
                    }
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
}
