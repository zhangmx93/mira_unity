# SenseOnnxManager 快速参考指南

## 快速开始

### 1. 初始化
```csharp
// 监听初始化完成事件
SenseOnnxManager.Instance.OnSenseOnnxInitialized += OnSenseOnnxReady;

void OnSenseOnnxReady()
{
    Debug.Log("SenseOnnx 已就绪");
    // 开始使用各项功能
}
```

### 2. 启用唤醒词检测
```csharp
// 启用 KWS 开关
SenseOnnxManager.Instance.SetKwsSwitch(true);

// 监听唤醒词检测
SenseOnnxManager.Instance.OnKwsDetected += (keyword) => {
    Debug.Log($"检测到唤醒词: {keyword}");
    // 此时 wakeup 状态已自动设置为 true
};

// 开始录音
SenseOnnxManager.Instance.StartRecord();
```

### 3. 语音识别 (STT)
```csharp
// 监听识别结果
SenseOnnxManager.Instance.OnSttResult += (text) => {
    Debug.Log($"识别结果: {text}");
    // 处理识别的文本
    ProcessUserCommand(text);
};

// 注意: STT 会在 wakeup=true 时自动接收录音数据
```

### 4. 语音合成 (TTS)
```csharp
// 直接播放文本
SenseOnnxManager.Instance.TtsGenerate("你好，我是语音助手");

// 监听音频块 (可选)
SenseOnnxManager.Instance.OnTtsAudioChunk += (audioData) => {
    // 可以在这里处理音频数据，例如可视化
    Debug.Log($"收到音频块: {audioData.Length} 个采样");
};
```

## API 速查表

### 状态查询
| 方法 | 返回值 | 说明 |
|------|--------|------|
| `IsInitialized()` | `bool` | 检查是否已初始化 |
| `IsTtsAbilityReady()` | `bool` | 检查 TTS 是否就绪 |
| `IsSttAbilityReady()` | `bool` | 检查 STT 是否就绪 |
| `IsKwsReady()` | `bool` | 检查 KWS 是否就绪 |
| `IsRecordReady()` | `bool` | 检查 Record 是否就绪 |

### KWS 控制
| 方法 | 参数 | 说明 |
|------|------|------|
| `SetKwsSwitch(bool)` | `enabled` | 设置唤醒词开关 |
| `GetKwsSwitch()` | - | 获取唤醒词开关状态 |
| `SetWakeup(bool)` | `isWakeup` | 设置唤醒状态 |
| `GetWakeup()` | - | 获取唤醒状态 |

### 录音控制
| 方法 | 说明 |
|------|------|
| `StartRecord()` | 开始录音 |
| `StopRecord()` | 停止录音 |

### TTS 控制
| 方法 | 参数 | 说明 |
|------|------|------|
| `TtsGenerate(string)` | `text` | 文字转语音并播放 |

### STT 控制
| 方法 | 说明 |
|------|------|
| `SttStartRecognition()` | 开始语音识别 (通常不需要手动调用) |

### 事件
| 事件 | 参数 | 触发时机 |
|------|------|----------|
| `OnSenseOnnxInitialized` | - | 初始化完成 |
| `OnInitializationError` | `string error` | 初始化失败 |
| `OnKwsDetected` | `string keyword` | 检测到唤醒词 |
| `OnSttResult` | `string text` | STT 识别结果 |
| `OnTtsAudioChunk` | `float[] audioData` | TTS 音频块 |
| `OnConversationResponse` | `string response` | 对话响应 |

## 常见使用场景

### 场景 1: 简单的语音助手
```csharp
void Start()
{
    var manager = SenseOnnxManager.Instance;
    
    // 1. 等待初始化
    manager.OnSenseOnnxInitialized += () => {
        // 2. 启用唤醒词
        manager.SetKwsSwitch(true);
        manager.StartRecord();
    };
    
    // 3. 监听唤醒
    manager.OnKwsDetected += (keyword) => {
        Debug.Log("已唤醒");
    };
    
    // 4. 监听识别
    manager.OnSttResult += (text) => {
        Debug.Log($"用户说: {text}");
        // 5. 回复
        manager.TtsGenerate($"收到指令: {text}");
        // 6. 重置唤醒状态
        manager.SetWakeup(false);
    };
}
```

### 场景 2: 持续对话模式
```csharp
void Start()
{
    var manager = SenseOnnxManager.Instance;
    
    manager.OnSenseOnnxInitialized += () => {
        manager.SetKwsSwitch(true);
        manager.StartRecord();
    };
    
    manager.OnKwsDetected += (keyword) => {
        // 播放提示音
        manager.TtsGenerate("我在听");
    };
    
    manager.OnSttResult += async (text) => {
        // 调用 LLM 获取回复
        string response = await GetLLMResponse(text);
        
        // 播放回复
        manager.TtsGenerate(response);
        
        // 等待 TTS 播放完成后重置
        await Task.Delay(3000);
        manager.SetWakeup(false);
    };
}

async Task<string> GetLLMResponse(string userInput)
{
    // 调用你的 LLM API
    return "这是 LLM 的回复";
}
```

### 场景 3: 免唤醒模式 (直接语音识别)
```csharp
void Start()
{
    var manager = SenseOnnxManager.Instance;
    
    manager.OnSenseOnnxInitialized += () => {
        // 不启用 KWS，直接设置为唤醒状态
        manager.SetKwsSwitch(false);
        manager.SetWakeup(true);
        manager.StartRecord();
    };
    
    manager.OnSttResult += (text) => {
        ProcessCommand(text);
    };
}
```

### 场景 4: 按钮触发的语音识别
```csharp
public void OnRecordButtonPressed()
{
    var manager = SenseOnnxManager.Instance;
    
    // 开始录音和识别
    manager.SetWakeup(true);
    manager.StartRecord();
}

public void OnRecordButtonReleased()
{
    var manager = SenseOnnxManager.Instance;
    
    // 停止录音
    manager.StopRecord();
    manager.SetWakeup(false);
}

void Start()
{
    SenseOnnxManager.Instance.OnSttResult += (text) => {
        Debug.Log($"识别结果: {text}");
    };
}
```

## 最佳实践

### 1. 状态管理
```csharp
// ✅ 好的做法: 在对话完成后重置唤醒状态
manager.OnSttResult += (text) => {
    ProcessCommand(text);
    manager.SetWakeup(false);  // 重置状态
};

// ❌ 不好的做法: 忘记重置状态
manager.OnSttResult += (text) => {
    ProcessCommand(text);
    // 缺少重置，会导致持续识别
};
```

### 2. 错误处理
```csharp
// ✅ 好的做法: 监听初始化错误
manager.OnInitializationError += (error) => {
    Debug.LogError($"初始化失败: {error}");
    ShowErrorToUser(error);
};

// ✅ 好的做法: 检查状态
if (manager.IsTtsAbilityReady())
{
    manager.TtsGenerate("你好");
}
else
{
    Debug.LogWarning("TTS 未就绪");
}
```

### 3. 资源管理
```csharp
void OnDestroy()
{
    // 停止所有操作
    var manager = SenseOnnxManager.Instance;
    manager.StopRecord();
    manager.SetKwsSwitch(false);
    manager.SetWakeup(false);
}
```

### 4. 线程安全
```csharp
// ✅ 所有回调都在主线程执行，可以安全地更新 UI
manager.OnSttResult += (text) => {
    // 直接更新 UI
    resultText.text = text;
};
```

## 调试技巧

### 1. 查看状态信息
```csharp
Debug.Log(SenseOnnxManager.Instance.GetStatusInfo());
```

### 2. 监听所有事件
```csharp
void Start()
{
    var manager = SenseOnnxManager.Instance;
    
    manager.OnSenseOnnxInitialized += () => Debug.Log("[事件] 初始化完成");
    manager.OnKwsDetected += (k) => Debug.Log($"[事件] 唤醒词: {k}");
    manager.OnSttResult += (t) => Debug.Log($"[事件] STT: {t}");
    manager.OnTtsAudioChunk += (d) => Debug.Log($"[事件] TTS 音频块: {d.Length}");
}
```

### 3. 检查权限
```csharp
#if UNITY_ANDROID && !UNITY_EDITOR
bool hasAudio = UnityEngine.Android.Permission.HasUserAuthorizedPermission(
    UnityEngine.Android.Permission.Microphone
);
Debug.Log($"录音权限: {hasAudio}");
#endif
```

## 常见问题

### Q: 为什么没有检测到唤醒词?
A: 检查以下几点:
1. `kwsSwitch` 是否设置为 `true`
2. `wakeup` 是否为 `false` (如果已唤醒，KWS 不会工作)
3. 录音是否已开始 (`StartRecord()`)
4. 是否有录音权限

### Q: 为什么 STT 没有识别?
A: 检查以下几点:
1. `wakeup` 是否为 `true`
2. 录音是否已开始
3. 是否监听了 `OnSttResult` 事件

### Q: 为什么 TTS 没有声音?
A: 检查以下几点:
1. `IsTtsAbilityReady()` 是否返回 `true`
2. AudioTrack 是否初始化成功
3. 设备音量是否打开

### Q: 如何实现多轮对话?
A: 在每次对话完成后不要重置 `wakeup` 状态，或者在 TTS 播放完成后自动重新设置为 `true`

## 性能优化建议

1. **按需启用功能**: 不使用 KWS 时关闭它
2. **及时停止录音**: 不需要时停止 RecordAbility
3. **控制 TTS 长度**: 避免生成过长的语音
4. **使用事件而非轮询**: 使用事件系统而不是在 Update 中检查状态

## 下一步

- 查看 `SenseOnnxManager_完善说明.md` 了解详细的技术实现
- 查看 Android Demo 了解原始实现
- 集成 LLM 实现智能对话
- 添加 UI 界面显示状态和结果
