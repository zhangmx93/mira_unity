using UnityEngine;
using System;
using System.Diagnostics;

/// <summary>
/// 日志管理器 - 统一管理所有日志输出
/// 支持日志级别过滤、开关控制、日志分类等功能
/// </summary>
public class LoggerManager : MonoBehaviour
{
    #region 单例模式

    private static LoggerManager instance;

    public static LoggerManager Instance
    {
        get
        {
            if (instance == null)
            {
                // 尝试在场景中查找
                instance = FindObjectOfType<LoggerManager>();

                // 如果场景中没有，则创建一个新的
                if (instance == null)
                {
                    GameObject go = new GameObject("LoggerManager");
                    instance = go.AddComponent<LoggerManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    #endregion

    #region 日志级别枚举

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Verbose = 0,  // 详细信息（最低级别）
        Debug = 1,    // 调试信息
        Info = 2,     // 一般信息
        Warning = 3,  // 警告信息
        Error = 4,    // 错误信息
        Fatal = 5     // 致命错误（最高级别）
    }

    #endregion

    #region 配置参数

    [Header("日志开关")]
    [Tooltip("全局日志开关")]
    public bool enableLogging = true;

    [Tooltip("是否在 Release 构建中禁用日志")]
    public bool disableInReleaseBuild = true;

    [Header("日志级别")]
    [Tooltip("最低日志级别（低于此级别的日志不会输出）")]
    public LogLevel minimumLogLevel = LogLevel.Debug;

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

    [Header("性能设置")]
    [Tooltip("是否使用日志池（减少 GC）")]
    public bool useLogPool = true;

    [Tooltip("最大日志缓存数量")]
    public int maxLogCacheSize = 1000;

    #endregion

    #region Unity 生命周期

    void Awake()
    {
        // 单例检查
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        // Release 构建检查
        if (disableInReleaseBuild && !UnityEngine.Debug.isDebugBuild)
        {
            enableLogging = false;
        }
    }

    #endregion

    #region 公共日志方法

    /// <summary>
    /// 输出详细日志（Verbose）
    /// </summary>
    public static void Verbose(string message, string category = "General")
    {
        Instance.Log(LogLevel.Verbose, message, category);
    }

    /// <summary>
    /// 输出调试日志（Debug）
    /// </summary>
    public static void Debug(string message, string category = "General")
    {
        Instance.Log(LogLevel.Debug, message, category);
    }

    /// <summary>
    /// 输出信息日志（Info）
    /// </summary>
    public static void Info(string message, string category = "General")
    {
        Instance.Log(LogLevel.Info, message, category);
    }

    /// <summary>
    /// 输出警告日志（Warning）
    /// </summary>
    public static void Warning(string message, string category = "General")
    {
        Instance.Log(LogLevel.Warning, message, category);
    }

    /// <summary>
    /// 输出错误日志（Error）
    /// </summary>
    public static void Error(string message, string category = "General")
    {
        Instance.Log(LogLevel.Error, message, category);
    }

    /// <summary>
    /// 输出致命错误日志（Fatal）
    /// </summary>
    public static void Fatal(string message, string category = "General")
    {
        Instance.Log(LogLevel.Fatal, message, category);
    }

    /// <summary>
    /// 输出异常日志
    /// </summary>
    public static void Exception(Exception exception, string category = "General")
    {
        if (Instance.enableLogging && Instance.ShouldLog(LogLevel.Error, category))
        {
            string message = Instance.FormatLogMessage(LogLevel.Error, $"Exception: {exception.Message}", category);
            UnityEngine.Debug.LogException(exception);

            if (Instance.showStackTrace)
            {
                UnityEngine.Debug.Log($"{message}\nStackTrace: {exception.StackTrace}");
            }
        }
    }

    #endregion

    #region 带格式化的日志方法

    /// <summary>
    /// 输出格式化的调试日志
    /// </summary>
    public static void DebugFormat(string format, string category = "General", params object[] args)
    {
        Instance.Log(LogLevel.Debug, string.Format(format, args), category);
    }

    /// <summary>
    /// 输出格式化的信息日志
    /// </summary>
    public static void InfoFormat(string format, string category = "General", params object[] args)
    {
        Instance.Log(LogLevel.Info, string.Format(format, args), category);
    }

    /// <summary>
    /// 输出格式化的警告日志
    /// </summary>
    public static void WarningFormat(string format, string category = "General", params object[] args)
    {
        Instance.Log(LogLevel.Warning, string.Format(format, args), category);
    }

    /// <summary>
    /// 输出格式化的错误日志
    /// </summary>
    public static void ErrorFormat(string format, string category = "General", params object[] args)
    {
        Instance.Log(LogLevel.Error, string.Format(format, args), category);
    }

    #endregion

    #region 核心日志方法

    /// <summary>
    /// 核心日志输出方法
    /// </summary>
    private void Log(LogLevel level, string message, string category)
    {
        // 检查是否应该输出日志
        if (!enableLogging || !ShouldLog(level, category))
        {
            return;
        }

        // 格式化日志消息
        string formattedMessage = FormatLogMessage(level, message, category);

        // 根据日志级别选择输出方式
        switch (level)
        {
            case LogLevel.Verbose:
            case LogLevel.Debug:
            case LogLevel.Info:
                UnityEngine.Debug.Log(formattedMessage);
                break;

            case LogLevel.Warning:
                UnityEngine.Debug.LogWarning(formattedMessage);
                break;

            case LogLevel.Error:
            case LogLevel.Fatal:
                UnityEngine.Debug.LogError(formattedMessage);
                break;
        }

        // 如果需要显示堆栈信息
        if (showStackTrace && level >= LogLevel.Warning)
        {
            UnityEngine.Debug.Log($"StackTrace:\n{Environment.StackTrace}");
        }
    }

    #endregion

    #region 日志过滤

    /// <summary>
    /// 检查是否应该输出日志
    /// </summary>
    private bool ShouldLog(LogLevel level, string category)
    {
        // 检查日志级别
        if (level < minimumLogLevel)
        {
            return false;
        }

        // 检查类别过滤
        if (enabledCategories.Length > 0)
        {
            // 如果设置了启用列表，则只输出列表中的类别
            bool found = false;
            foreach (string enabledCategory in enabledCategories)
            {
                if (category.Equals(enabledCategory, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            if (!found) return false;
        }

        // 检查禁用列表
        foreach (string disabledCategory in disabledCategories)
        {
            if (category.Equals(disabledCategory, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    #endregion

    #region 日志格式化

    /// <summary>
    /// 格式化日志消息
    /// </summary>
    private string FormatLogMessage(LogLevel level, string message, string category)
    {
        string result = "";

        // 添加时间戳
        if (showTimestamp)
        {
            result += $"[{DateTime.Now:HH:mm:ss.fff}] ";
        }

        // 添加日志级别
        if (showLogLevel)
        {
            result += $"[{GetLogLevelString(level)}] ";
        }

        // 添加类别
        if (showCategory)
        {
            result += $"[{category}] ";
        }

        // 添加消息内容
        result += message;

        return result;
    }

    /// <summary>
    /// 获取日志级别字符串（带颜色）
    /// </summary>
    private string GetLogLevelString(LogLevel level)
    {
        switch (level)
        {
            case LogLevel.Verbose:
                return "<color=#808080>VERBOSE</color>";
            case LogLevel.Debug:
                return "<color=#00BFFF>DEBUG</color>";
            case LogLevel.Info:
                return "<color=#00FF00>INFO</color>";
            case LogLevel.Warning:
                return "<color=#FFA500>WARNING</color>";
            case LogLevel.Error:
                return "<color=#FF0000>ERROR</color>";
            case LogLevel.Fatal:
                return "<color=#8B0000>FATAL</color>";
            default:
                return level.ToString().ToUpper();
        }
    }

    #endregion

    #region 运行时控制方法

    /// <summary>
    /// 启用日志输出
    /// </summary>
    public static void Enable()
    {
        Instance.enableLogging = true;
    }

    /// <summary>
    /// 禁用日志输出
    /// </summary>
    public static void Disable()
    {
        Instance.enableLogging = false;
    }

    /// <summary>
    /// 设置最低日志级别
    /// </summary>
    public static void SetMinimumLogLevel(LogLevel level)
    {
        Instance.minimumLogLevel = level;
    }

    /// <summary>
    /// 启用指定类别的日志
    /// </summary>
    public static void EnableCategory(string category)
    {
        if (!Array.Exists(Instance.enabledCategories, c => c == category))
        {
            Array.Resize(ref Instance.enabledCategories, Instance.enabledCategories.Length + 1);
            Instance.enabledCategories[Instance.enabledCategories.Length - 1] = category;
        }
    }

    /// <summary>
    /// 禁用指定类别的日志
    /// </summary>
    public static void DisableCategory(string category)
    {
        if (!Array.Exists(Instance.disabledCategories, c => c == category))
        {
            Array.Resize(ref Instance.disabledCategories, Instance.disabledCategories.Length + 1);
            Instance.disabledCategories[Instance.disabledCategories.Length - 1] = category;
        }
    }

    /// <summary>
    /// 清空所有类别过滤
    /// </summary>
    public static void ClearCategoryFilters()
    {
        Instance.enabledCategories = new string[] { };
        Instance.disabledCategories = new string[] { };
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取当前日志配置信息
    /// </summary>
    public static string GetConfigInfo()
    {
        return $"LoggerManager Configuration:\n" +
               $"- Enabled: {Instance.enableLogging}\n" +
               $"- Minimum Level: {Instance.minimumLogLevel}\n" +
               $"- Show Timestamp: {Instance.showTimestamp}\n" +
               $"- Show Log Level: {Instance.showLogLevel}\n" +
               $"- Show Category: {Instance.showCategory}\n" +
               $"- Show StackTrace: {Instance.showStackTrace}\n" +
               $"- Enabled Categories: {string.Join(", ", Instance.enabledCategories)}\n" +
               $"- Disabled Categories: {string.Join(", ", Instance.disabledCategories)}";
    }

    #endregion
}
