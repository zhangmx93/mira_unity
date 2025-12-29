# SenseOnnxManager 完善总结

## 概述
根据提供的 Android Demo (`MainActivity.kt`)，对 `SenseOnnxManager.cs` 进行了全面完善，实现了完整的语音交互流程。

## 数据流程图

![SenseOnnx 数据流程](/Users/zhangmengxi.vendor/.gemini/antigravity/brain/5e6dc6d8-5e92-409a-bb70-5077e2f11032/senseonnx_data_flow_1766979711954.png)

完整的语音交互流程如上图所示，从录音到唤醒词检测，再到语音识别和语音合成，最后通过 AudioTrack 播放音频。


## 主要改进

### 1. **架构重构**
- **从旧架构**: 使用独立的 `OnnxTtsDetector` 和 `OnnxSttDetector`
- **到新架构**: 使用 SenseOnnx 单例 + 能力模式 (Ability Pattern)
  - `RecordAbility`: 音频录制
  - `TtsAbility`: 文字转语音
  - `SttAbility`: 语音转文字
  - `KwsAbility`: 关键词唤醒检测

### 2. **新增核心组件**

#### 2.1 Android JNI 对象
```csharp
private AndroidJavaObject senseOnnxInstance;  // SenseOnnx 单例
private AndroidJavaObject recordAbility;      // 录音能力
private AndroidJavaObject ttsAbility;         // TTS 能力
private AndroidJavaObject sttAbility;         // STT 能力
private AndroidJavaObject kwsAbility;         // KWS 唤醒词识别能力
private AndroidJavaObject audioTrack;         // 音频播放器
```

#### 2.2 状态管理
```csharp
private bool isRecordReady = false;
private bool isKwsReady = false;
private bool kwsSwitch = false;  // 唤醒词开关
private bool wakeup = false;     // 唤醒状态
```

#### 2.3 事件系统
```csharp
public event Action<string> OnSttResult;        // STT 识别结果事件
public event Action<string> OnKwsDetected;      // KWS 唤醒词检测事件
public event Action<float[]> OnTtsAudioChunk;  // TTS 音频块事件
```

### 3. **初始化流程优化**

#### 新的初始化步骤
1. **等待权限授予** (录音权限、存储权限)
2. **获取 Unity Activity**
3. **初始化 SenseOnnx 单例**
4. **获取各个能力实例** (Record, TTS, STT, KWS)
5. **初始化 AudioTrack** (用于 TTS 音频播放)
6. **设置回调监听器**

#### AudioTrack 初始化
- 从 `TtsAbility` 获取采样率
- 配置 AudioAttributes (CONTENT_TYPE_SPEECH, USAGE_MEDIA)
- 配置 AudioFormat (ENCODING_PCM_FLOAT, CHANNEL_OUT_MONO)
- 创建并启动 AudioTrack

### 4. **完整的数据流实现**

#### 4.1 录音 → 唤醒词检测流程
```
RecordAbility (录音)
    ↓ onChunk
RecordDataCallback
    ↓ (如果 kwsSwitch=true && wakeup=false)
KwsAbility.inputData (唤醒词检测)
    ↓ onFinish
KwsDataCallback
    ↓ 设置 wakeup=true
触发 OnKwsDetected 事件
```

#### 4.2 唤醒后语音识别流程
```
RecordAbility (录音)
    ↓ onChunk
RecordDataCallback
    ↓ (如果 wakeup=true)
SttAbility.inputData (语音识别)
    ↓ onChunk / onFinish
SttDataCallback
    ↓ 
触发 OnSttResult 事件
    ↓ (可选)
TtsAbility.inputData (语音合成)
```

#### 4.3 TTS 播放流程
```
TtsAbility.inputData (输入文本)
    ↓ onChunk
TtsDataCallback
    ↓ 
AudioTrack.write (写入音频数据)
    ↓ 实时播放
    ↓ onFinish
AudioTrack.stop (停止播放)
```

### 5. **新增公共 API**

#### TTS 接口
```csharp
void TtsGenerate(string text)           // 文字转语音
bool IsTtsAbilityReady()                 // 检查 TTS 是否就绪
```

#### STT 接口
```csharp
void SttStartRecognition()               // 开始语音识别
bool IsSttAbilityReady()                 // 检查 STT 是否就绪
```

#### KWS 接口
```csharp
void SetKwsSwitch(bool enabled)          // 设置唤醒词开关
bool GetKwsSwitch()                      // 获取唤醒词开关状态
void SetWakeup(bool isWakeup)            // 设置唤醒状态
bool GetWakeup()                         // 获取唤醒状态
bool IsKwsReady()                        // 检查 KWS 是否就绪
```

#### Record 接口
```csharp
void StartRecord()                       // 开始录音
void StopRecord()                        // 停止录音
bool IsRecordReady()                     // 检查 Record 是否就绪
```

### 6. **回调系统重构**

#### DataCallbackListener 基类
```csharp
public abstract class DataCallbackListener<T>
{
    public abstract void OnDataChunkCallback(T data);
    public virtual void OnDataFinishCallback(T data) { }
}
```

#### 四个回调实现
1. **TtsDataCallback**: 处理 TTS 音频数据，写入 AudioTrack
2. **SttDataCallback**: 处理 STT 识别结果，触发事件
3. **KwsDataCallback**: 处理唤醒词检测，设置唤醒状态
4. **RecordDataCallback**: 处理录音数据，根据状态分发给 KWS 或 STT

#### Android 回调代理
- 使用 `AndroidJavaProxy` 桥接 Java 回调到 C#
- 使用 `UnityMainThreadDispatcher` 确保回调在主线程执行
- 回调方法名匹配 Android SDK: `onChunk`, `onFinish`

### 7. **与 Android Demo 的对应关系**

| Android Demo | Unity C# |
|-------------|----------|
| `SenseOnnx.getInstance()` | `senseOnnxInstance` |
| `RecordAbility.getInstance()` | `recordAbility` |
| `TtsAbility.getInstance()` | `ttsAbility` |
| `SttAbility.getInstance()` | `sttAbility` |
| `KwsAbility.getInstance()` | `kwsAbility` |
| `initAudioTrack()` | `InitializeAudioTrack()` |
| `SenseOnnx.getInstance().kwsSwitch` | `SetKwsSwitch()` / `GetKwsSwitch()` |
| `SenseOnnx.getInstance().wakeup` | `SetWakeup()` / `GetWakeup()` |
| `tts.dataCallbackListener = object : DataCallbackListener<FloatArray>()` | `TtsDataCallback` |
| `stt.dataCallbackListener = object : DataCallbackListener<String>()` | `SttDataCallback` |
| `kws.dataCallbackListener = object : DataCallbackListener<String>()` | `KwsDataCallback` |
| `record.dataCallbackListener = object : DataCallbackListener<FloatArray>()` | `RecordDataCallback` |

## 使用示例

### 基本使用流程
```csharp
// 1. 等待初始化完成
SenseOnnxManager.Instance.OnSenseOnnxInitialized += () => {
    Debug.Log("SenseOnnx 初始化完成");
    
    // 2. 启用唤醒词检测
    SenseOnnxManager.Instance.SetKwsSwitch(true);
    
    // 3. 开始录音
    SenseOnnxManager.Instance.StartRecord();
};

// 4. 监听唤醒词检测
SenseOnnxManager.Instance.OnKwsDetected += (keyword) => {
    Debug.Log($"检测到唤醒词: {keyword}");
    // 唤醒状态已自动设置为 true
};

// 5. 监听 STT 识别结果
SenseOnnxManager.Instance.OnSttResult += (text) => {
    Debug.Log($"识别结果: {text}");
    // 可以在这里处理识别的文本
};

// 6. 手动触发 TTS
SenseOnnxManager.Instance.TtsGenerate("你好，我是语音助手");
```

### 完整对话流程
```csharp
// 1. 用户说出唤醒词 (例如: "你好小智")
//    → RecordAbility 录音
//    → KwsAbility 检测到唤醒词
//    → OnKwsDetected 事件触发
//    → wakeup 设置为 true

// 2. 用户说出指令 (例如: "今天天气怎么样")
//    → RecordAbility 继续录音
//    → SttAbility 识别语音
//    → OnSttResult 事件触发，得到文本 "今天天气怎么样"

// 3. 处理指令并回复
//    → 调用 LLM 或其他服务获取答案
//    → 调用 TtsGenerate("今天天气晴朗，温度25度")
//    → TtsAbility 生成音频
//    → AudioTrack 播放语音

// 4. 重置唤醒状态
SenseOnnxManager.Instance.SetWakeup(false);
```

## 关键改进点

1. ✅ **完整的能力模式**: 使用 Ability 模式替代旧的 Detector 模式
2. ✅ **AudioTrack 集成**: 实现 TTS 音频的实时播放
3. ✅ **唤醒词检测**: 实现 KWS 唤醒机制
4. ✅ **数据流管道**: Record → KWS → STT → TTS 完整流程
5. ✅ **事件驱动**: 使用事件系统解耦业务逻辑
6. ✅ **线程安全**: 使用 UnityMainThreadDispatcher 确保回调在主线程
7. ✅ **错误处理**: 完善的异常捕获和日志记录

## 注意事项

1. **权限要求**: 需要 `RECORD_AUDIO` 和 `EXTERNAL_STORAGE_READ` 权限
2. **线程安全**: 所有 Android 回调都通过 `UnityMainThreadDispatcher` 转发到主线程
3. **资源管理**: AudioTrack 需要在适当时机释放资源
4. **唤醒状态**: 需要在对话完成后手动重置 `wakeup` 状态
5. **包名匹配**: 确保 Android SDK 的包名为 `com.sensetime.senseonnx.*`

## 下一步建议

1. 实现 LLM 集成，完成完整的对话流程
2. 添加音频可视化 (波形图、音量指示器)
3. 实现多轮对话管理
4. 添加语音活动检测 (VAD) 自动停止录音
5. 实现离线模式和在线模式切换
6. 添加语音情感识别
7. 实现多语言支持
