using UnityEngine;
using System.Collections;

/// <summary>
/// ANR（Application Not Responding）保护
/// 监控主线程响应，防止 SDK 初始化导致 ANR
/// </summary>
public class ANRProtection : MonoBehaviour
{
    [Header("配置")]
    [Tooltip("帧率监控阈值（FPS 低于此值时警告）")]
    public int fpsThreshold = 10;

    [Tooltip("检查间隔（秒）")]
    public float checkInterval = 1.0f;

    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = false;

    [Header("应用无响应保护")]
    [Tooltip("是否启用 ANR 保护")]
    public bool enableANRProtection = true;

    [Tooltip("主线程最大允许阻塞时间（秒）")]
    public float maxBlockingTime = 3.0f;

    private float lastUpdateTime;
    private int frameCount;
    private float fps;
    private bool isMonitoring = false;

    void Start()
    {
        lastUpdateTime = Time.realtimeSinceStartup;
        StartCoroutine(MonitorPerformance());

        if (enableDebugLog)
            Debug.Log("ANRProtection: 已启动性能监控");
    }

    void Update()
    {
        frameCount++;

        float currentTime = Time.realtimeSinceStartup;
        float deltaTime = currentTime - lastUpdateTime;

        // 检测是否有长时间阻塞
        if (enableANRProtection && deltaTime > maxBlockingTime)
        {
            Debug.LogError($"ANRProtection: ⚠️ 检测到主线程阻塞 {deltaTime:F2} 秒！");
            Debug.LogError($"  这可能导致 ANR（应用无响应）");
            Debug.LogError($"  建议检查是否有同步加载或耗时操作在主线程执行");
        }

        lastUpdateTime = currentTime;
    }

    /// <summary>
    /// 监控性能
    /// </summary>
    IEnumerator MonitorPerformance()
    {
        isMonitoring = true;

        while (true)
        {
            yield return new WaitForSeconds(checkInterval);

            // 计算 FPS
            fps = frameCount / checkInterval;
            frameCount = 0;

            // FPS 警告
            if (fps < fpsThreshold)
            {
                Debug.LogWarning($"ANRProtection: ⚠️ FPS 过低: {fps:F1} (阈值: {fpsThreshold})");
                Debug.LogWarning($"  可能原因: SDK 初始化、大量对象加载、GC 等");
            }

            if (enableDebugLog && fps >= fpsThreshold)
            {
                Debug.Log($"ANRProtection: FPS = {fps:F1}");
            }
        }
    }

    /// <summary>
    /// 获取当前 FPS
    /// </summary>
    public float GetFPS()
    {
        return fps;
    }

    /// <summary>
    /// 获取性能状态
    /// </summary>
    public string GetPerformanceStatus()
    {
        if (fps >= 30)
            return $"✅ 性能良好 ({fps:F1} FPS)";
        else if (fps >= fpsThreshold)
            return $"⚠️ 性能一般 ({fps:F1} FPS)";
        else
            return $"❌ 性能较差 ({fps:F1} FPS)";
    }

    void OnDestroy()
    {
        isMonitoring = false;
    }
}
