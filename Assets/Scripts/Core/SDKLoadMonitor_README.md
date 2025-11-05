# SDK 加载监控系统使用指南

## 概述

为了防止在 SDK 未加载完毕时调用功能导致线程崩溃，我们实现了一套完整的 SDK 加载监控系统。

## 系统组件

### 1. SDKLoadMonitor (主监控器)
负责监控所有 SDK 的加载状态，提供统一的就绪检查和事件通知。

### 2. SDKLoader (延迟加载器)
按顺序延迟加载各个 SDK，避免同时加载导致的性能问题。

### 3. UnityMainThreadDispatcher (线程调度器)
增强版本，添加了线程安全检查和错误处理。

## 快速开始

### 场景设置

1. 在场景中创建一个空对象，命名为 `SDKManagers`
2. 添加以下组件到该对象：
   - `UnityMainThreadDispatcher`
   - `SDKLoader`
   - `SDKLoadMonitor`
   - `RKLLMManager`
   - `RKTTSManager`
   - `RKFaceManager` (可选)

### 代码使用示例

#### 方法 1：使用 SDKLoadMonitor 的安全操作方法

```csharp
using UnityEngine;

public class MyGameController : MonoBehaviour
{
    void Start()
    {
        // 监听所有 SDK 就绪事件
        SDKLoadMonitor.Instance.OnAllSDKsReady.AddListener(OnSDKsReady);
    }

    void OnSDKsReady()
    {
        Debug.Log("所有 SDK 已就绪，可以开始游戏逻辑");
    }

    // 按钮点击：发送消息到 LLM
    public void OnSendMessageButtonClick()
    {
        SDKLoadMonitor.Instance.SafeLLMOperation(
            action: () =>
            {
                // 这里是安全的 LLM 操作
                RKLLMManager.Instance.Chat("你好");
            },
            onError: (error) =>
            {
                Debug.LogError($"LLM 操作失败: {error}");
                // 显示错误提示给用户
            }
        );
    }

    // 按钮点击：文字转语音
    public void OnSpeakButtonClick()
    {
        SDKLoadMonitor.Instance.SafeTTSOperation(
            action: () =>
            {
                // 这里是安全的 TTS 操作
                RKTTSManager.Instance.Speak("你好世界");
            },
            onError: (error) =>
            {
                Debug.LogError($"TTS 操作失败: {error}");
            }
        );
    }
}
```

#### 方法 2：手动检查 SDK 状态

```csharp
using UnityEngine;

public class MyGameController : MonoBehaviour
{
    void Update()
    {
        // 检查所有 SDK 是否就绪
        if (SDKLoadMonitor.Instance.AreAllSDKsReady())
        {
            // 可以安全地使用 SDK
        }

        // 或者检查单个 SDK
        var (llmReady, ttsReady, faceReady) = SDKLoadMonitor.Instance.GetSDKReadyStatus();

        if (llmReady)
        {
            // LLM 可用
        }
    }
}
```

#### 方法 3：显示加载状态

```csharp
using UnityEngine;
using UnityEngine.UI;

public class LoadingUI : MonoBehaviour
{
    public Text statusText;

    void Update()
    {
        if (SDKLoadMonitor.Instance != null)
        {
            statusText.text = SDKLoadMonitor.Instance.GetStatusText();
        }
    }
}
```

## 配置选项

### SDKLoadMonitor 配置

- **checkInterval**: 检查 SDK 状态的间隔时间（秒），默认 0.5
- **maxWaitTime**: 最大等待时间（秒），超时后强制标记为失败，默认 30
- **enableDebugLog**: 是否启用调试日志

### SDKLoader 配置

- **initialDelay**: 启动后延迟多久开始加载第一个 SDK（秒），默认 1.0
- **delayBetweenSDKs**: 各 SDK 之间的延迟时间（秒），默认 2.0
- **enableDebugLog**: 是否启用调试日志

### UnityMainThreadDispatcher 配置

- **enableDebugLog**: 是否启用调试日志
- **maxQueueSize**: 队列最大容量（0 = 无限制），默认 1000

## 加载顺序

系统会按照以下顺序加载 SDK：

1. 初始延迟（1 秒）
2. 加载 RKLLM
3. 延迟（2 秒）
4. 加载 RKTTS
5. 延迟（2 秒）
6. 加载 RKFace（如果存在）
7. 完成加载

总共约需要 5-7 秒（不包括实际的 SDK 初始化时间）

## 事件系统

### SDKLoadMonitor 事件

```csharp
// 所有 SDK 就绪时触发
SDKLoadMonitor.Instance.OnAllSDKsReady.AddListener(() => {
    Debug.Log("准备就绪！");
});

// SDK 加载失败时触发
SDKLoadMonitor.Instance.OnSDKLoadFailed.AddListener((error) => {
    Debug.LogError($"加载失败: {error}");
});
```

## 最佳实践

### 1. 在 SDK 就绪前禁用 UI

```csharp
public Button sendButton;

void Start()
{
    // 禁用按钮直到 SDK 就绪
    sendButton.interactable = false;

    SDKLoadMonitor.Instance.OnAllSDKsReady.AddListener(() => {
        sendButton.interactable = true;
    });
}
```

### 2. 显示加载界面

```csharp
public GameObject loadingPanel;

void Start()
{
    loadingPanel.SetActive(true);

    SDKLoadMonitor.Instance.OnAllSDKsReady.AddListener(() => {
        loadingPanel.SetActive(false);
    });
}
```

### 3. 错误处理

```csharp
void Start()
{
    SDKLoadMonitor.Instance.OnSDKLoadFailed.AddListener((error) => {
        // 显示错误对话框
        ShowErrorDialog($"SDK 加载失败: {error}\n请重启应用");

        // 或者尝试重新加载
        StartCoroutine(RetryLoadSDKs());
    });
}

IEnumerator RetryLoadSDKs()
{
    yield return new WaitForSeconds(2f);
    SDKLoadMonitor.Instance.ResetAllSDKs();
}
```

## 调试技巧

### 查看加载日志

在 Inspector 中启用 `enableDebugLog` 选项，可以看到详细的加载日志：

```
SDKLoader: 等待 1 秒后开始加载...
SDKLoader: [1/3] 正在加载 RKLLM...
SDKLoader: ✅ RKLLM 已启用，正在后台初始化
RKLLMManager: Awake() 被调用
RKLLMManager: OnEnable() 被调用 - 开始初始化
...
SDKLoadMonitor: ✅ RKLLM 已就绪
SDKLoadMonitor: ✅ RKTTS 已就绪
SDKLoadMonitor: ✅ 所有 SDK 已就绪！
```

### 查看线程统计信息

```csharp
// 获取 UnityMainThreadDispatcher 统计信息
var dispatcher = FindObjectOfType<UnityMainThreadDispatcher>();
var (enqueued, executed, errors) = dispatcher.GetStatistics();
Debug.Log($"入队: {enqueued}, 已执行: {executed}, 错误: {errors}");
```

## 故障排除

### 问题：SDK 加载超时

**原因**：SDK 初始化时间过长或卡住

**解决**：
1. 检查 Android 权限是否授予
2. 检查模型文件是否存在
3. 增加 `maxWaitTime` 配置

### 问题：线程崩溃

**原因**：在 SDK 未就绪时调用了功能

**解决**：
1. 使用 `SafeLLMOperation` / `SafeTTSOperation` 方法
2. 在调用前检查 `CanPerformOperations()`

### 问题：加载进度卡住

**原因**：某个 SDK 初始化失败

**解决**：
1. 查看各 Manager 的日志找出问题
2. 确保 UnityMainThreadDispatcher 在场景中存在
3. 检查 SDK 依赖的原生库是否正确

## 性能建议

1. **不要在 Update 中频繁调用** `AreAllSDKsReady()`，使用事件监听更高效
2. **调整延迟时间**：如果设备性能好，可以减少 `delayBetweenSDKs`
3. **禁用不需要的 SDK**：如果不需要 RKFace，不要添加到场景中

## 总结

这套系统提供了完整的 SDK 加载监控和保护机制，防止在 SDK 未就绪时调用导致的线程崩溃。通过使用 `SafeOperation` 方法和事件系统，可以确保应用的稳定性和用户体验。
