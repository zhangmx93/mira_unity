using UnityEngine;
using System;
using System.Text;

/// <summary>
/// RKLLM 方法签名诊断工具
/// 使用反射查找正确的 getInstance 方法签名
/// </summary>
public class RKLLMMethodFinder : MonoBehaviour
{
    void Start()
    {
        #if UNITY_ANDROID && !UNITY_EDITOR
        FindGetInstanceMethod();
        #else
        LoggerManager.Debug("仅在 Android 设备上运行", "LLM");
        #endif
    }

    private void FindGetInstanceMethod()
    {
        LoggerManager.Info("========== 查找 getInstance 方法签名 ==========", "LLM");

        try
        {
            // 获取 Unity Activity
            AndroidJavaObject activity = null;
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            // 获取 SenseRKLlmDetector 类
            using (AndroidJavaClass rkllmClass = new AndroidJavaClass("com.sensetime.rkllm.SenseRKLlmDetector"))
            {
                // 获取 Class 对象 - 使用反射获取 java.lang.Class
                using (AndroidJavaClass classClass = new AndroidJavaClass("java.lang.Class"))
                {
                    // 通过类名获取 Class 对象
                    AndroidJavaObject classObj = classClass.CallStatic<AndroidJavaObject>("forName", "com.sensetime.rkllm.SenseRKLlmDetector");

                    // 获取所有方法
                    AndroidJavaObject[] methods = classObj.Call<AndroidJavaObject[]>("getMethods");

                    LoggerManager.Info($"找到 {methods.Length} 个方法", "LLM");

                    // 查找 getInstance 方法
                    foreach (AndroidJavaObject method in methods)
                    {
                        string methodName = method.Call<string>("getName");

                        if (methodName.Contains("getInstance") || methodName.Contains("instance"))
                        {
                            // 获取方法详细信息
                            int modifiers = method.Call<int>("getModifiers");

                            // 检查是否是静态方法
                            using (AndroidJavaClass modifierClass = new AndroidJavaClass("java.lang.reflect.Modifier"))
                            {
                                bool isStatic = modifierClass.CallStatic<bool>("isStatic", modifiers);

                                // 获取返回类型
                                AndroidJavaObject returnType = method.Call<AndroidJavaObject>("getReturnType");
                                string returnTypeName = returnType.Call<string>("getName");

                                // 获取参数类型
                                AndroidJavaObject[] parameterTypes = method.Call<AndroidJavaObject[]>("getParameterTypes");
                                StringBuilder paramStr = new StringBuilder();
                                foreach (AndroidJavaObject paramType in parameterTypes)
                                {
                                    if (paramStr.Length > 0) paramStr.Append(", ");
                                    paramStr.Append(paramType.Call<string>("getName"));
                                }

                                LoggerManager.Info($"✅ 找到方法: {methodName}", "LLM");
                                LoggerManager.Info($"   静态方法: {isStatic}", "LLM");
                                LoggerManager.Info($"   返回类型: {returnTypeName}", "LLM");
                                LoggerManager.Info($"   参数类型: ({paramStr})", "LLM");
                                LoggerManager.Info($"   完整签名: {method.Call<string>("toString")}", "LLM");

                                // 尝试调用这个方法
                                if (isStatic && methodName == "getInstance")
                                {
                                    TryCallMethod(rkllmClass, methodName, parameterTypes.Length, activity);
                                }
                            }
                        }
                    }

                    // 查找构造函数
                    LoggerManager.Info("\n========== 查找构造函数 ==========", "LLM");
                    AndroidJavaObject[] constructors = classObj.Call<AndroidJavaObject[]>("getConstructors");

                    foreach (AndroidJavaObject constructor in constructors)
                    {
                        AndroidJavaObject[] parameterTypes = constructor.Call<AndroidJavaObject[]>("getParameterTypes");
                        StringBuilder paramStr = new StringBuilder();
                        foreach (AndroidJavaObject paramType in parameterTypes)
                        {
                            if (paramStr.Length > 0) paramStr.Append(", ");
                            paramStr.Append(paramType.Call<string>("getName"));
                        }

                        LoggerManager.Info($"✅ 构造函数参数: ({paramStr})", "LLM");
                    }
                }
            }
        }
        catch (Exception e)
        {
            LoggerManager.Error($"查找方法时出错: {e.Message}\n{e.StackTrace}", "LLM");
        }

        LoggerManager.Info("========== 查找完成 ==========", "LLM");
    }

    private void TryCallMethod(AndroidJavaClass rkllmClass, string methodName, int paramCount, AndroidJavaObject activity)
    {
        LoggerManager.Info($"\n尝试调用 {methodName} (参数数量: {paramCount})", "LLM");

        try
        {
            AndroidJavaObject result = null;

            if (paramCount == 0)
            {
                result = rkllmClass.CallStatic<AndroidJavaObject>(methodName);
                LoggerManager.Info($"✅ 无参数调用成功！", "LLM");
            }
            else if (paramCount == 1)
            {
                result = rkllmClass.CallStatic<AndroidJavaObject>(methodName, activity);
                LoggerManager.Info($"✅ 单参数(Activity)调用成功！", "LLM");
            }

            if (result != null)
            {
                LoggerManager.Info($"✅ 返回对象不为 null，类型: {result.Call<AndroidJavaObject>("getClass").Call<string>("getName")}", "LLM");
            }
        }
        catch (Exception e)
        {
            LoggerManager.Error($"❌ 调用失败: {e.Message}", "LLM");
        }
    }
}
