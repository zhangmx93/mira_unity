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
        Debug.Log("RKLLMMethodFinder: 仅在 Android 设备上运行");
        #endif
    }

    private void FindGetInstanceMethod()
    {
        Debug.Log("========== 查找 getInstance 方法签名 ==========");

        try
        {
            // 获取 Unity Activity
            AndroidJavaObject activity = null;
            using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            }

            // 获取 SenseRKLlmDetector 类
            using (AndroidJavaClass rkllmClass = new AndroidJavaClass("com.senseflow.rkllm.SenseRKLlmDetector"))
            {
                // 获取 Class 对象 - 使用反射获取 java.lang.Class
                using (AndroidJavaClass classClass = new AndroidJavaClass("java.lang.Class"))
                {
                    // 通过类名获取 Class 对象
                    AndroidJavaObject classObj = classClass.CallStatic<AndroidJavaObject>("forName", "com.senseflow.rkllm.SenseRKLlmDetector");

                    // 获取所有方法
                    AndroidJavaObject[] methods = classObj.Call<AndroidJavaObject[]>("getMethods");

                    Debug.Log($"找到 {methods.Length} 个方法");

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

                                Debug.Log($"✅ 找到方法: {methodName}");
                                Debug.Log($"   静态方法: {isStatic}");
                                Debug.Log($"   返回类型: {returnTypeName}");
                                Debug.Log($"   参数类型: ({paramStr})");
                                Debug.Log($"   完整签名: {method.Call<string>("toString")}");

                                // 尝试调用这个方法
                                if (isStatic && methodName == "getInstance")
                                {
                                    TryCallMethod(rkllmClass, methodName, parameterTypes.Length, activity);
                                }
                            }
                        }
                    }

                    // 查找构造函数
                    Debug.Log("\n========== 查找构造函数 ==========");
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

                        Debug.Log($"✅ 构造函数参数: ({paramStr})");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"查找方法时出错: {e.Message}\n{e.StackTrace}");
        }

        Debug.Log("========== 查找完成 ==========");
    }

    private void TryCallMethod(AndroidJavaClass rkllmClass, string methodName, int paramCount, AndroidJavaObject activity)
    {
        Debug.Log($"\n尝试调用 {methodName} (参数数量: {paramCount})");

        try
        {
            AndroidJavaObject result = null;

            if (paramCount == 0)
            {
                result = rkllmClass.CallStatic<AndroidJavaObject>(methodName);
                Debug.Log($"✅ 无参数调用成功！");
            }
            else if (paramCount == 1)
            {
                result = rkllmClass.CallStatic<AndroidJavaObject>(methodName, activity);
                Debug.Log($"✅ 单参数(Activity)调用成功！");
            }

            if (result != null)
            {
                Debug.Log($"✅ 返回对象不为 null，类型: {result.Call<AndroidJavaObject>("getClass").Call<string>("getName")}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 调用失败: {e.Message}");
        }
    }
}
