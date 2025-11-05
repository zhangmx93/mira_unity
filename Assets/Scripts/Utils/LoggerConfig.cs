using UnityEngine;

/// <summary>
/// 日志配置文件 - ScriptableObject
/// 可以在 Unity Editor 中创建不同的日志配置预设
/// </summary>
[CreateAssetMenu(fileName = "LoggerConfig", menuName = "Settings/Logger Config", order = 1)]
public class LoggerConfig : ScriptableObject
{
    [Header("全局开关")]
    [Tooltip("是否启用日志")]
    public bool enableLogging = true;

    [Tooltip("是否在 Release 构建中禁用日志")]
    public bool disableInReleaseBuild = true;

    [Header("日志级别")]
    [Tooltip("最低日志级别")]
    public LoggerManager.LogLevel minimumLogLevel = LoggerManager.LogLevel.Debug;

    [Header("日志格式")]
    [Tooltip("是否显示时间戳")]
    public bool showTimestamp = true;

    [Tooltip("是否显示日志级别")]
    public bool showLogLevel = true;

    [Tooltip("是否显示类别标签")]
    public bool showCategory = true;

    [Tooltip("是否显示调用堆栈信息")]
    public bool showStackTrace = false;

    [Header("类别过滤")]
    [Tooltip("启用的日志类别（为空则显示所有类别）")]
    public string[] enabledCategories = new string[] { };

    [Tooltip("禁用的日志类别")]
    public string[] disabledCategories = new string[] { };

    [Header("预设配置")]
    [Tooltip("常用类别预设")]
    public string[] commonCategories = new string[]
    {
        "General",
        "UI",
        "Network",
        "Game",
        "Audio",
        "Animation",
        "SDK",
        "Database",
        "Physics",
        "AI"
    };

    /// <summary>
    /// 应用配置到 LoggerManager
    /// </summary>
    public void ApplyToLoggerManager()
    {
        if (LoggerManager.Instance == null) return;

        var manager = LoggerManager.Instance;
        manager.enableLogging = enableLogging;
        manager.disableInReleaseBuild = disableInReleaseBuild;
        manager.minimumLogLevel = minimumLogLevel;
        manager.showTimestamp = showTimestamp;
        manager.showLogLevel = showLogLevel;
        manager.showCategory = showCategory;
        manager.showStackTrace = showStackTrace;
        manager.enabledCategories = enabledCategories;
        manager.disabledCategories = disabledCategories;

        UnityEngine.Debug.Log("[LoggerConfig] 配置已应用到 LoggerManager");
    }
}
