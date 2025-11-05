using UnityEngine;

/// <summary>
/// LoggerManager 使用示例
/// 展示如何在项目中使用日志管理器
/// </summary>
public class LoggerManagerExample : MonoBehaviour
{
    void Start()
    {
        // ========== 基本日志输出 ==========

        // 输出不同级别的日志
        LoggerManager.Verbose("这是一条详细日志", "Example");
        LoggerManager.Debug("这是一条调试日志", "Example");
        LoggerManager.Info("这是一条信息日志", "Example");
        LoggerManager.Warning("这是一条警告日志", "Example");
        LoggerManager.Error("这是一条错误日志", "Example");
        LoggerManager.Fatal("这是一条致命错误日志", "Example");

        // ========== 不同类别的日志 ==========

        LoggerManager.Info("SDK 初始化完成", "SDK");
        LoggerManager.Debug("用户点击了按钮", "UI");
        LoggerManager.Warning("网络连接不稳定", "Network");
        LoggerManager.Error("数据库查询失败", "Database");

        // ========== 格式化日志 ==========

        string playerName = "Player123";
        int score = 1000;
        LoggerManager.InfoFormat("玩家 {0} 获得了 {1} 分", "Game", playerName, score);

        float fps = 60.5f;
        LoggerManager.DebugFormat("当前帧率: {0:F2} FPS", "Performance", fps);

        // ========== 异常日志 ==========

        try
        {
            // 模拟一个异常
            throw new System.Exception("这是一个测试异常");
        }
        catch (System.Exception ex)
        {
            LoggerManager.Exception(ex, "Example");
        }

        // ========== 运行时控制 ==========

        // 禁用所有日志
        // LoggerManager.Disable();

        // 启用所有日志
        // LoggerManager.Enable();

        // 设置最低日志级别（只显示 Warning 及以上级别）
        // LoggerManager.SetMinimumLogLevel(LoggerManager.LogLevel.Warning);

        // 启用特定类别的日志
        // LoggerManager.EnableCategory("Game");
        // LoggerManager.EnableCategory("UI");

        // 禁用特定类别的日志
        // LoggerManager.DisableCategory("Debug");

        // 清空所有类别过滤
        // LoggerManager.ClearCategoryFilters();

        // ========== 获取配置信息 ==========

        // LoggerManager.Debug(LoggerManager.GetConfigInfo(), "System");
    }

    void Update()
    {
        // 在 Update 中使用日志（注意性能）
        if (Input.GetKeyDown(KeyCode.Space))
        {
            LoggerManager.Info("用户按下了空格键", "Input");
        }
    }

    /// <summary>
    /// 示例：在自定义方法中使用日志
    /// </summary>
    void LoadGameData()
    {
        LoggerManager.Debug("开始加载游戏数据", "GameData");

        try
        {
            // 加载逻辑...
            LoggerManager.Info("游戏数据加载成功", "GameData");
        }
        catch (System.Exception ex)
        {
            LoggerManager.Error($"游戏数据加载失败: {ex.Message}", "GameData");
            LoggerManager.Exception(ex, "GameData");
        }
    }

    /// <summary>
    /// 示例：使用不同类别区分不同模块
    /// </summary>
    void NetworkExample()
    {
        LoggerManager.Info("开始连接服务器", "Network");
        LoggerManager.Debug("正在建立 TCP 连接...", "Network");
        LoggerManager.Warning("连接超时，尝试重连", "Network");
        LoggerManager.Error("连接失败: 无法访问服务器", "Network");
    }
}
