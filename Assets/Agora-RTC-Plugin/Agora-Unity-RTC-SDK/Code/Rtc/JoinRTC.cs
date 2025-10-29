using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Agora.Rtc;
#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
    using UnityEngine.Android;
#endif

public class JoinRTC : MonoBehaviour
{
    // 填入你的 App ID
    private string _appID = "0790dea83cfc4c9884149c488f673912";
    // 填入你的频道名
    private string _channelName = "123456";
    // 填入 Token
    private string _token = "007eJxTYHi7v/iTo/bZu7f3Le13npuUPuHQkwtLDNrVF1v/fbfrydZABQYDc0uDlNREC+PktGSTZEsLCxNDE8tkEwuLNDNzY0tDozMPrmQ0BDIyHFufysjIAIEgPhuDoZGxiakZAwMA8KsjxQ==";

    // 每次启动生成唯一的用户ID（0表示由服务器自动分配）
    private uint _userID = 0;

#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
    // 仅申请麦克风权限（纯语音通话不需要相机权限）
    private ArrayList permissionList = new ArrayList() { Permission.Microphone };
#endif

    public Text LogText;
    internal Logger Log;
    internal IRtcEngine RtcEngine = null;

    // 日志缓存，用于在UI上显示
    private System.Text.StringBuilder logBuilder = new System.Text.StringBuilder();
    private int maxLogLines = 50; // 最多显示50行日志

    void Start()
    {
        // 初始化日志系统
        InitializeLogger();

        // 检测运行平台并给出提示
        CheckPlatformSupport();

        LogMessage("=== JoinRTC Start() 开始 ===");

        // 0. 生成唯一的用户ID
        GenerateUniqueUserID();

        // 1. 创建 RtcEngine 实例
        LogMessage("1. 正在创建 RtcEngine 实例...");
        RtcEngine = Agora.Rtc.RtcEngine.CreateAgoraRtcEngine();

        if (RtcEngine == null)
        {
            LogError("创建 RtcEngine 实例失败");
            return;
        }
        LogSuccess("RtcEngine 实例创建成功");

        // 2. 检查 AppID
        LogMessage("2. 正在检查 AppID...");
        CheckAppId();

        // 3. 设置视频 SDK 引擎
        LogMessage("3. 正在设置视频 SDK 引擎...");
        SetupVideoSDKEngine();

        // 4. 确保运行时 UI 存在并可交互
        LogMessage("4. 正在确保运行时 UI...");
        EnsureRuntimeUI();
        SetupUI();

        // 5. 初始化事件处理器
        LogMessage("5. 正在初始化事件处理器...");
        InitEventHandler();

        // 6. 异步检查权限（避免启动时闪退）
        LogMessage("6. 准备检查Android权限...");
        StartCoroutine(CheckPermissionsAsync());

        LogMessage("=== JoinRTC Start() 完成 ===");
    }

    // 创建实例并初始化
    private void SetupVideoSDKEngine()
    {
        LogMessage("📱 开始设置视频 SDK 引擎...");

        try
        {
            // 创建 RtcEngineContext
            RtcEngineContext context = new RtcEngineContext();
            LogSuccess("RtcEngineContext 创建成功");

            // 设置配置参数
            context.appId = _appID;
            context.channelProfile = CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING;
            context.audioScenario = AUDIO_SCENARIO_TYPE.AUDIO_SCENARIO_DEFAULT;

            LogMessage($"📋 配置参数设置完成:");
            LogMessage($"   - AppID: {_appID}");
            LogMessage($"   - ChannelProfile: {context.channelProfile}");
            LogMessage($"   - AudioScenario: {context.audioScenario}");

            // 初始化 IRtcEngine
            LogMessage("🔄 正在初始化 RtcEngine...");
            int result = RtcEngine.Initialize(context);

            if (result == 0)
            {
                LogSuccess("RtcEngine 初始化成功");
            }
            else
            {
                LogError($"RtcEngine 初始化失败，错误代码: {result}");
                // 根据错误代码提供详细信息
                switch (result)
                {
                    case -1:
                        LogError("一般性错误，请检查网络连接");
                        break;
                    case -2:
                        LogError("参数无效，请检查 AppID 和配置");
                        break;
                    case -7:
                        LogError("引擎未初始化");
                        break;
                    case -101:
                        LogError("AppID 无效");
                        break;
                    default:
                        LogError($"未知错误代码: {result}");
                        break;
                }
            }
        }
        catch (System.Exception ex)
        {
            LogError($"设置视频 SDK 引擎时出现异常: {ex.Message}");
            LogError($"堆栈跟踪: {ex.StackTrace}");
        }
    }

    private bool CheckAppId()
    {
        LogMessage("🔍 检查 AppID 有效性...");

        bool isValid = !string.IsNullOrEmpty(_appID) && _appID.Length > 10;

        if (!isValid)
        {
            LogWarning($"AppID 可能无效，当前值: {_appID}");
            LogWarning("请检查 AppID 配置，长度应大于10个字符");
        }
        else
        {
            LogSuccess($"AppID 检查通过: {_appID}");
        }

        return isValid;
    }

    /// <summary>
    /// 生成唯一的用户ID
    /// 每次应用启动时都会生成不同的ID，确保每个实例都是独立的
    /// </summary>
    private void GenerateUniqueUserID()
    {
        LogMessage("🆔 开始生成唯一用户ID...");

        // 方式1: 使用时间戳生成（推荐）
        // 基于当前时间的Unix时间戳，确保每次启动都不同
        System.DateTime epoch = new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc);
        long timestamp = (long)(System.DateTime.UtcNow - epoch).TotalMilliseconds;

        // 取时间戳的后9位作为UID（确保在uint范围内）
        _userID = (uint)(timestamp % 1000000000);

        // 方式2: 使用随机数（备选）
        // System.Random random = new System.Random();
        // _userID = (uint)random.Next(100000, 999999999);

        // 方式3: 使用设备标识+随机数（更可靠）
        // string deviceId = SystemInfo.deviceUniqueIdentifier;
        // int hashCode = deviceId.GetHashCode();
        // System.Random random = new System.Random(hashCode);
        // _userID = (uint)random.Next(100000, 999999999);

        LogSuccess($"✅ 生成唯一用户ID: {_userID}");
        LogMessage($"   - 生成方式: 时间戳（毫秒级）");
        LogMessage($"   - ID范围: 0 - 999999999");
    }

    /// <summary>
    /// 检测运行平台并给出相应提示
    /// </summary>
    private void CheckPlatformSupport()
    {
        LogMessage("");
        LogMessage("╔══════════════════════════════════════════╗");

#if UNITY_EDITOR
        LogWarning("║  ⚠️ 当前在Unity编辑器中运行          ║");
        LogMessage("╚══════════════════════════════════════════╝");
        LogWarning("📝 编辑器环境说明:");
        LogWarning("   - Agora SDK在编辑器中支持有限");
        LogWarning("   - 音频采集可能不工作");
        LogWarning("   - 无法测试音频通话功能");
        LogMessage("");
        LogMessage("💡 建议:");
        LogMessage("   1. 编辑器仅用于UI开发和逻辑验证");
        LogMessage("   2. 音频通话测试请打包到真实设备");
        LogMessage("   3. 使用两台Android设备进行测试");
        LogMessage("");
        LogWarning("⚠️ 编辑器 + Android设备通话可能不工作！");

#elif UNITY_ANDROID
        LogSuccess("║  📱 当前在Android设备上运行          ║");
        LogMessage("╚══════════════════════════════════════════╝");
        LogSuccess("✅ 完整功能支持:");
        LogMessage("   - 音频采集 ✅");
        LogMessage("   - 音频播放 ✅");
        LogMessage("   - 扬声器控制 ✅");
        LogMessage("   - 权限管理 ✅");
        LogMessage("");
        LogMessage("💡 测试建议:");
        LogMessage("   - 使用另一台Android设备加入同一频道");
        LogMessage("   - 或使用iOS设备进行跨平台测试");

#elif UNITY_IOS
        LogSuccess("║  📱 当前在iOS设备上运行              ║");
        LogMessage("╚══════════════════════════════════════════╝");
        LogSuccess("✅ 完整功能支持:");
        LogMessage("   - 音频采集 ✅");
        LogMessage("   - 音频播放 ✅");
        LogMessage("   - 扬声器控制 ✅");
        LogMessage("   - 权限管理 ✅");

#else
        LogMessage("║  🖥️ 当前在其他平台运行              ║");
        LogMessage("╚══════════════════════════════════════════╝");
        LogWarning("⚠️ 未知平台，功能可能受限");
#endif

        LogMessage("");
    }

    /// <summary>
    /// 设置音频音量（确保可以听到声音）
    /// </summary>
    private void SetupAudioVolume()
    {
        if (RtcEngine == null) return;

        try
        {
            LogMessage("🔊 正在设置音频音量...");

            // 设置播放音量为 100%（确保能听到远端声音）
            RtcEngine.AdjustPlaybackSignalVolume(100);
            LogSuccess("   - 播放音量: 100%");

            // 设置录音音量为 100%（确保对方能听到你的声音）
            RtcEngine.AdjustRecordingSignalVolume(100);
            LogSuccess("   - 录音音量: 100%");

            LogSuccess("✅ 音量设置完成");
        }
        catch (System.Exception ex)
        {
            LogError($"设置音量时出错: {ex.Message}");
        }
    }

    /// <summary>
    /// 启用/关闭扬声器（免提模式）
    /// 在Android上，默认音频输出到听筒，需要切换到扬声器才能听清楚
    /// </summary>
    private void EnableSpeakerphone(bool enabled)
    {
        if (RtcEngine == null) return;

        try
        {
#if UNITY_ANDROID || UNITY_IOS
            RtcEngine.SetEnableSpeakerphone(enabled);

            if (enabled)
            {
                LogSuccess("╔══════════════════════════════════════════╗");
                LogSuccess("║  📢 已启用扬声器（免提模式）        ║");
                LogSuccess("╚══════════════════════════════════════════╝");
                LogMessage("   - 声音将从手机底部扬声器播放");
                LogMessage("   - 音量会比较大，方便收听");
                LogWarning("   ⚠️ 如果仍听不到，请检查系统音量");
            }
            else
            {
                LogMessage("╔══════════════════════════════════════════╗");
                LogMessage("║  📱 已切换到听筒模式                ║");
                LogMessage("╚══════════════════════════════════════════╝");
                LogMessage("   - 声音将从手机顶部听筒播放");
                LogMessage("   - 请将手机贴在耳朵上收听");
            }
#else
            LogMessage("ℹ️ 扬声器控制仅适用于移动设备");
#endif
        }
        catch (System.Exception ex)
        {
            LogError($"设置扬声器时出错: {ex.Message}");
        }
    }

    private void SetupUI()
    {
        LogMessage("🎨 设置 UI 组件...");

        try
        {
            // 查找离开按钮
            GameObject leaveGo = GameObject.Find("Canvas/Leave");
            if (leaveGo != null)
            {
                Button leaveButton = leaveGo.GetComponent<Button>();
                if (leaveButton != null)
                {
                    leaveButton.onClick.AddListener(Leave);
                    LogSuccess("离开按钮设置成功");
                }
                else
                {
                    LogWarning("未找到离开按钮的 Button 组件");
                }
            }
            else
            {
                LogWarning("未找到 Canvas/Leave 游戏对象");
            }

            // 查找加入按钮
            GameObject joinGo = GameObject.Find("Canvas/Join");
            if (joinGo != null)
            {
                Button joinButton = joinGo.GetComponent<Button>();
                if (joinButton != null)
                {
                    joinButton.onClick.AddListener(Join);
                    LogSuccess("加入按钮设置成功");
                }
                else
                {
                    LogWarning("未找到加入按钮的 Button 组件");
                }
            }
            else
            {
                LogWarning("未找到 Canvas/Join 游戏对象");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"设置 UI 时出现异常: {ex.Message}");
        }
    }

    // 确保运行时具备可点击 UI（Canvas、GraphicRaycaster、EventSystem、Join/Leave 按钮）
    private void EnsureRuntimeUI()
    {
        EnsureEventSystemExists();

        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            canvasGo = new GameObject("Canvas");
            canvasGo.layer = LayerMask.NameToLayer("UI");

            var rectTransform = canvasGo.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            if (canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>() == null)
            {
                var scaler = canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();
                scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 1f;
            }

            if (canvasGo.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                canvasGo.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }

        // 如果没有按钮则创建
        if (GameObject.Find("Canvas/Join") == null)
        {
            CreateButton(canvasGo.transform, "Join", new Vector2(0.5f, 0.2f), Join);
        }
        if (GameObject.Find("Canvas/Leave") == null)
        {
            CreateButton(canvasGo.transform, "Leave", new Vector2(0.5f, 0.1f), Leave);
        }
    }

    private void EnsureEventSystemExists()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null)
        {
            return;
        }

        var es = new GameObject("EventSystem");
        es.AddComponent<UnityEngine.EventSystems.EventSystem>();
        es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    private void CreateButton(Transform parent, string buttonName, Vector2 anchor, UnityEngine.Events.UnityAction onClick)
    {
        var go = new GameObject(buttonName);
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(280, 90);
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.color = new Color(0.15f, 0.6f, 1f, 0.9f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        // 文本
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var text = textGo.AddComponent<Text>();
        text.text = buttonName;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        var trt = text.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = Vector2.zero;
        trt.offsetMax = Vector2.zero;
    }

    // 网络连接诊断方法
    private bool CheckNetworkConnectivity()
    {
        LogMessage("╔══════════════════════════════════════════╗");
        LogMessage("║  🌐 开始检测网络连接状态...        ║");
        LogMessage("╚══════════════════════════════════════════╝");

        // 检查 Unity 的网络可达性
        UnityEngine.NetworkReachability reachability = Application.internetReachability;

        LogMessage("📊 网络连接信息:");

        switch (reachability)
        {
            case UnityEngine.NetworkReachability.NotReachable:
                LogError("❌ 网络不可用");
                LogMessage("💡 请检查:");
                LogMessage("   1. 是否开启了WiFi或移动数据");
                LogMessage("   2. 是否处于飞行模式");
                LogMessage("   3. 网络设置是否正确");
                LogError("🚫 无法加入频道，请先连接网络！");
                return false;

            case UnityEngine.NetworkReachability.ReachableViaCarrierDataNetwork:
                LogSuccess("✅ 已连接移动数据网络");
                LogMessage("📱 连接类型: 4G/5G移动网络");
                LogWarning("⚠️ 提示: 使用移动数据可能产生流量费用");
                LogMessage("💡 建议:");
                LogMessage("   - 确保流量充足");
                LogMessage("   - 信号较差时可能影响通话质量");
                break;

            case UnityEngine.NetworkReachability.ReachableViaLocalAreaNetwork:
                LogSuccess("✅ 已连接WiFi网络");
                LogMessage("📶 连接类型: WiFi局域网");
                LogSuccess("💡 WiFi连接通常有更好的稳定性");
                break;
        }

        // 显示当前网络配置信息
        LogMessage("");
        LogMessage("🔧 Agora SDK配置:");
        LogMessage($"   - AppID: {_appID.Substring(0, 8)}...");
        LogMessage($"   - 频道名: {_channelName}");
        LogMessage($"   - 用户ID: {_userID}");
        LogMessage($"   - Token: {(_token.Length > 0 ? "已配置" : "未配置")}");

        // 网络连接可用，但给出优化建议
        LogMessage("");
        LogMessage("🎯 网络优化建议:");
        LogMessage("   1. 保持稳定的网络连接");
        LogMessage("   2. 避免在网络拥塞时段使用");
        LogMessage("   3. 关闭其他占用带宽的应用");
        LogMessage("   4. 尽量靠近WiFi路由器");

        LogSuccess("");
        LogSuccess("✅ 网络检测通过，可以开始连接！");
        LogMessage("");

        return true;
    }

    public void Join()
    {
        LogSuccess($"");
        LogSuccess($"║  🔘 点击了 [加入频道] 按钮           ║");
        LogMessage($"🎯 开始加入频道: {_channelName}");
        LogMessage($"👤 使用用户ID: {_userID}");

        if (RtcEngine == null)
        {
            LogError("RtcEngine 未初始化，无法加入频道");
            LogError("❌ 加入频道失败！");
            return;
        }

        // 🔥 先检测网络连接状态
        if (!CheckNetworkConnectivity())
        {
            LogError("❌ 网络连接检测失败，无法加入频道");
            return;
        }

        try
        {
            // 启用音频模块
            LogMessage("🔊 启用音频模块...");
            RtcEngine.EnableAudio();
            LogSuccess("音频模块启用成功");

            // 设置频道媒体选项
            LogMessage("⚙️ 设置频道媒体选项...");
            ChannelMediaOptions options = new ChannelMediaOptions();

            // 发布麦克风采集的音频流
            options.publishMicrophoneTrack.SetValue(true);
            // 自动订阅所有音频流
            options.autoSubscribeAudio.SetValue(true);
            // 将频道场景设为直播
            options.channelProfile.SetValue(CHANNEL_PROFILE_TYPE.CHANNEL_PROFILE_LIVE_BROADCASTING);
            // 将用户角色设为主播
            options.clientRoleType.SetValue(CLIENT_ROLE_TYPE.CLIENT_ROLE_BROADCASTER);

            LogSuccess("频道媒体选项设置完成:");

            // 加入频道（使用生成的唯一用户ID）
            LogMessage($"🚪 正在加入频道: {_channelName}");
            int result = RtcEngine.JoinChannel(_token, _channelName, _userID, options);

            if (result == 0)
            {
                LogSuccess($"✅ 加入频道请求发送成功!");
                LogMessage($"📋 频道信息:");
                LogMessage($"   - 频道名: {_channelName}");
                LogMessage($"   - 本地用户ID: {_userID}");
                LogMessage($"   - 等待连接到服务器...");
                LogWarning($"⏳ 请稍候，正在建立连接...");

                // 🔥 设置音频播放和录音音量
                SetupAudioVolume();

                // 🔥 启用扬声器（免提模式）
                EnableSpeakerphone(true);
            }
            else
            {
                LogError($"❌ 加入频道失败，错误代码: {result}");
            }
        }
        catch (System.Exception ex)
        {
            LogError($"❌ 加入频道时出现异常: {ex.Message}");
        }
    }

    // 创建用户回调类实例，并设置回调
    private void InitEventHandler()
    {
        LogMessage("🔄 初始化事件处理器...");

        try
        {
            UserEventHandler handler = new UserEventHandler(this);
            RtcEngine.InitEventHandler(handler);
            LogSuccess("事件处理器初始化成功");
        }
        catch (System.Exception ex)
        {
            LogError($"初始化事件处理器时出现异常: {ex.Message}");
        }
    }

    // 实现你自己的回调类，可以继承 IRtcEngineEventHandler 接口类实现
    internal class UserEventHandler : IRtcEngineEventHandler
    {
        private readonly JoinRTC _joinRTC;
        private int _remoteUserCount = 0; // 记录远端用户数量

        internal UserEventHandler(JoinRTC joinRTC)
        {
            _joinRTC = joinRTC;
            _joinRTC.LogSuccess("UserEventHandler 创建成功");
        }

        // 本地用户成功加入频道时，会触发该回调
        public override void OnJoinChannelSuccess(RtcConnection connection, int elapsed)
        {
            _joinRTC.LogSuccess($"🎉 本地用户成功加入频道!");
            _joinRTC.LogMessage($"   - 频道名: {connection.channelId}");
            _joinRTC.LogMessage($"   - 本地用户ID: {connection.localUid}");
            _joinRTC.LogMessage($"   - 加入耗时: {elapsed}ms");
            _joinRTC.LogMessage($"   - 等待其他用户加入...");
        }

        // ========== 远端用户加入频道监听 ==========
        // 当有新用户加入频道时会触发此回调
        public override void OnUserJoined(RtcConnection connection, uint uid, int elapsed)
        {
            _remoteUserCount++;

            _joinRTC.LogSuccess($"╔══════════════════════════════════╗");
            _joinRTC.LogSuccess($"║   👤 有新用户进入房间！        ║");
            _joinRTC.LogSuccess($"╚══════════════════════════════════╝");
            _joinRTC.LogMessage($"📊 用户信息:");
            _joinRTC.LogMessage($"   - 远端用户ID: {uid}");
            _joinRTC.LogMessage($"   - 频道名称: {connection.channelId}");
            _joinRTC.LogMessage($"   - 本地用户ID: {connection.localUid}");
            _joinRTC.LogMessage($"   - 加入耗时: {elapsed}ms");
            _joinRTC.LogMessage($"📈 房间统计:");
            _joinRTC.LogMessage($"   - 当前远端用户数: {_remoteUserCount}");
            _joinRTC.LogMessage($"   - 房间总人数: {_remoteUserCount + 1} (包括本地用户)");
            _joinRTC.LogSuccess($"✅ 现在可以开始通话了!");
        }

        // 远端用户离开当前频道时，会触发该回调
        public override void OnUserOffline(RtcConnection connection, uint uid, USER_OFFLINE_REASON_TYPE reason)
        {
            _remoteUserCount--;
            if (_remoteUserCount < 0) _remoteUserCount = 0;

            _joinRTC.LogWarning($"╔══════════════════════════════════╗");
            _joinRTC.LogWarning($"║   👋 用户离开房间            ║");
            _joinRTC.LogWarning($"╚══════════════════════════════════╝");
            _joinRTC.LogMessage($"📊 用户信息:");
            _joinRTC.LogMessage($"   - 离开用户ID: {uid}");
            _joinRTC.LogMessage($"   - 频道名称: {connection.channelId}");
            _joinRTC.LogMessage($"   - 离开原因: {GetOfflineReasonText(reason)}");
            _joinRTC.LogMessage($"📈 房间统计:");
            _joinRTC.LogMessage($"   - 当前远端用户数: {_remoteUserCount}");
            _joinRTC.LogMessage($"   - 房间总人数: {_remoteUserCount + 1} (包括本地用户)");
        }

        // ========== 音频相关回调 ==========
        // 远端用户音频状态改变
        public override void OnRemoteAudioStateChanged(RtcConnection connection, uint uid, REMOTE_AUDIO_STATE state, REMOTE_AUDIO_STATE_REASON reason, int elapsed)
        {
            string stateText = GetAudioStateText(state);
            string reasonText = GetAudioStateReasonText(reason);

            _joinRTC.LogMessage($"🔊 用户 {uid} 音频状态变化:");
            _joinRTC.LogMessage($"   - 状态: {stateText}");
            _joinRTC.LogMessage($"   - 原因: {reasonText}");
        }

        // 远端用户音量提示
        public override void OnAudioVolumeIndication(RtcConnection connection, AudioVolumeInfo[] speakers, uint speakerNumber, int totalVolume)
        {
            foreach (var speaker in speakers)
            {
                if (speaker.uid != 0) // 不是本地用户
                {
                    // 只在音量较大时显示（避免日志刷屏）
                    if (speaker.volume > 50)
                    {
                        _joinRTC.LogMessage($"🎤 用户 {speaker.uid} 正在说话，音量: {speaker.volume}");
                    }
                }
            }
        }

        // ========== 网络质量回调 ==========
        public override void OnNetworkQuality(RtcConnection connection, uint remoteUid, int txQuality, int rxQuality)
        {
            if (remoteUid > 0) // 远端用户
            {
                string quality = GetNetworkQualityText(rxQuality);
                // 只在网络质量较差时提示
                if (rxQuality >= 3)
                {
                    _joinRTC.LogWarning($"📡 用户 {remoteUid} 网络质量: {quality}");
                }
            }
            else // 本地用户 (remoteUid == 0)
            {
                string txQualityText = GetNetworkQualityText(txQuality);
                string rxQualityText = GetNetworkQualityText(rxQuality);

                // 只在网络质量较差时提示
                if (txQuality >= 4 || rxQuality >= 4)
                {
                    _joinRTC.LogWarning($"╔══════════════════════════════════╗");
                    _joinRTC.LogWarning($"║  ⚠️ 本地网络质量较差！      ║");
                    _joinRTC.LogWarning($"╚══════════════════════════════════╝");
                    _joinRTC.LogMessage($"📡 网络状态:");
                    _joinRTC.LogMessage($"   - 上行质量(发送): {txQualityText}");
                    _joinRTC.LogMessage($"   - 下行质量(接收): {rxQualityText}");
                    _joinRTC.LogWarning($"💡 建议:");
                    _joinRTC.LogMessage($"   - 检查WiFi或移动网络信号");
                    _joinRTC.LogMessage($"   - 尝试靠近路由器");
                    _joinRTC.LogMessage($"   - 关闭其他占用网络的应用");
                }
            }
        }

        // ========== 连接状态变化回调 ==========
        public override void OnConnectionStateChanged(RtcConnection connection, CONNECTION_STATE_TYPE state, CONNECTION_CHANGED_REASON_TYPE reason)
        {
            string stateText = GetConnectionStateText(state);
            string reasonText = GetConnectionChangeReasonText(reason);

            _joinRTC.LogMessage($"╔══════════════════════════════════╗");
            _joinRTC.LogMessage($"║  🔌 连接状态变化              ║");
            _joinRTC.LogMessage($"╚══════════════════════════════════╝");
            _joinRTC.LogMessage($"📊 连接信息:");
            _joinRTC.LogMessage($"   - 当前状态: {stateText}");
            _joinRTC.LogMessage($"   - 变化原因: {reasonText}");

            // 根据不同状态给出提示
            switch (state)
            {
                case CONNECTION_STATE_TYPE.CONNECTION_STATE_CONNECTING:
                    _joinRTC.LogMessage($"⏳ 正在连接到Agora服务器...");
                    break;

                case CONNECTION_STATE_TYPE.CONNECTION_STATE_CONNECTED:
                    _joinRTC.LogSuccess($"✅ 已成功连接到服务器！");
                    break;

                case CONNECTION_STATE_TYPE.CONNECTION_STATE_RECONNECTING:
                    _joinRTC.LogWarning($"⚠️ 网络连接中断，正在尝试重新连接...");
                    _joinRTC.LogMessage($"💡 请保持应用运行，等待重连成功");
                    break;

                case CONNECTION_STATE_TYPE.CONNECTION_STATE_DISCONNECTED:
                    _joinRTC.LogError($"❌ 已断开与服务器的连接");

                    // 根据断开原因给出详细说明
                    if (reason == CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_INTERRUPTED)
                    {
                        _joinRTC.LogError($"📡 网络中断导致断开连接");
                        _joinRTC.LogMessage($"💡 可能的原因:");
                        _joinRTC.LogMessage($"   1. WiFi或移动网络信号弱");
                        _joinRTC.LogMessage($"   2. 网络切换(WiFi<->移动数据)");
                        _joinRTC.LogMessage($"   3. 进入信号盲区");
                        _joinRTC.LogMessage($"   4. 路由器或网络设备问题");
                    }
                    else if (reason == CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_BANNED_BY_SERVER)
                    {
                        _joinRTC.LogError($"🚫 被服务器拒绝连接");
                        _joinRTC.LogMessage($"💡 可能的原因:");
                        _joinRTC.LogMessage($"   1. Token过期或无效");
                        _joinRTC.LogMessage($"   2. AppID配置错误");
                        _joinRTC.LogMessage($"   3. 账号权限问题");
                    }
                    else if (reason == CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_INVALID_APP_ID)
                    {
                        _joinRTC.LogError($"❌ AppID无效");
                        _joinRTC.LogMessage($"💡 请检查:");
                        _joinRTC.LogMessage($"   - AppID是否正确配置");
                        _joinRTC.LogMessage($"   - 当前AppID: {connection.channelId}");
                    }
                    else if (reason == CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_INVALID_CHANNEL_NAME)
                    {
                        _joinRTC.LogError($"❌ 频道名无效");
                        _joinRTC.LogMessage($"💡 频道名要求:");
                        _joinRTC.LogMessage($"   - 长度不超过64字节");
                        _joinRTC.LogMessage($"   - 支持字母、数字、下划线等");
                    }
                    else if (reason == CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_INVALID_TOKEN)
                    {
                        _joinRTC.LogError($"❌ Token无效");
                        _joinRTC.LogMessage($"💡 请检查:");
                        _joinRTC.LogMessage($"   - Token是否过期");
                        _joinRTC.LogMessage($"   - Token是否与AppID和频道匹配");
                    }
                    else if (reason == CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_TOKEN_EXPIRED)
                    {
                        _joinRTC.LogError($"⏰ Token已过期");
                        _joinRTC.LogMessage($"💡 需要:");
                        _joinRTC.LogMessage($"   - 重新生成Token");
                        _joinRTC.LogMessage($"   - 使用新Token重新加入频道");
                    }
                    break;

                case CONNECTION_STATE_TYPE.CONNECTION_STATE_FAILED:
                    _joinRTC.LogError($"❌ 连接失败");
                    _joinRTC.LogMessage($"💡 请尝试:");
                    _joinRTC.LogMessage($"   1. 检查网络连接");
                    _joinRTC.LogMessage($"   2. 检查防火墙设置");
                    _joinRTC.LogMessage($"   3. 重启应用后重试");
                    break;
            }
        }

        // ========== 错误回调 ==========
        public override void OnError(int err, string msg)
        {
            _joinRTC.LogError($"╔══════════════════════════════════╗");
            _joinRTC.LogError($"║  ❌ 发生错误                  ║");
            _joinRTC.LogError($"╚══════════════════════════════════╝");
            _joinRTC.LogError($"错误代码: {err}");
            _joinRTC.LogError($"错误信息: {msg}");

            // 根据错误码给出具体建议
            switch (err)
            {
                case 17: // ERR_JOIN_CHANNEL_REJECTED
                    _joinRTC.LogMessage($"💡 加入频道被拒绝，可能原因:");
                    _joinRTC.LogMessage($"   - Token无效或过期");
                    _joinRTC.LogMessage($"   - 已在其他频道中");
                    break;

                case 2: // ERR_INVALID_ARGUMENT
                    _joinRTC.LogMessage($"💡 参数错误，请检查:");
                    _joinRTC.LogMessage($"   - AppID格式");
                    _joinRTC.LogMessage($"   - 频道名格式");
                    _joinRTC.LogMessage($"   - Token格式");
                    break;

                case 109: // ERR_TOKEN_EXPIRED
                    _joinRTC.LogMessage($"💡 Token已过期:");
                    _joinRTC.LogMessage($"   - 需要重新生成Token");
                    _joinRTC.LogMessage($"   - 建议使用更长有效期");
                    break;

                case 110: // ERR_INVALID_TOKEN
                    _joinRTC.LogMessage($"💡 Token无效:");
                    _joinRTC.LogMessage($"   - 检查Token是否正确");
                    _joinRTC.LogMessage($"   - 确认AppID和频道名匹配");
                    break;

                case 1001: // ERR_LOAD_MEDIA_ENGINE
                    _joinRTC.LogMessage($"💡 加载媒体引擎失败:");
                    _joinRTC.LogMessage($"   - 检查设备音频权限");
                    _joinRTC.LogMessage($"   - 重启应用后重试");
                    break;

                default:
                    _joinRTC.LogMessage($"💡 详细错误信息请查看Agora错误码文档");
                    break;
            }
        }

        // ========== Token即将过期回调 ==========
        public override void OnTokenPrivilegeWillExpire(RtcConnection connection, string token)
        {
            _joinRTC.LogWarning($"╔══════════════════════════════════╗");
            _joinRTC.LogWarning($"║  ⏰ Token即将过期！          ║");
            _joinRTC.LogWarning($"╚══════════════════════════════════╝");
            _joinRTC.LogMessage($"⚠️ Token将在30秒后过期");
            _joinRTC.LogMessage($"💡 建议:");
            _joinRTC.LogMessage($"   - 立即从服务器获取新Token");
            _joinRTC.LogMessage($"   - 调用RenewToken更新Token");
            _joinRTC.LogWarning($"⚠️ 如不更新，连接将会中断！");
        }

        // ========== 请求Token回调 ==========
        public override void OnRequestToken(RtcConnection connection)
        {
            _joinRTC.LogError($"╔══════════════════════════════════╗");
            _joinRTC.LogError($"║  🔑 需要Token！              ║");
            _joinRTC.LogError($"╚══════════════════════════════════╝");
            _joinRTC.LogMessage($"⚠️ 当前Token已失效");
            _joinRTC.LogMessage($"💡 需要:");
            _joinRTC.LogMessage($"   1. 从服务器获取新Token");
            _joinRTC.LogMessage($"   2. 调用RenewToken更新");
            _joinRTC.LogMessage($"   3. 或者重新加入频道");
        }

        // ========== 辅助方法：文本转换 ==========

        private string GetOfflineReasonText(USER_OFFLINE_REASON_TYPE reason)
        {
            switch (reason)
            {
                case USER_OFFLINE_REASON_TYPE.USER_OFFLINE_QUIT:
                    return "主动退出";
                case USER_OFFLINE_REASON_TYPE.USER_OFFLINE_DROPPED:
                    return "网络断开";
                case USER_OFFLINE_REASON_TYPE.USER_OFFLINE_BECOME_AUDIENCE:
                    return "切换为观众";
                default:
                    return $"未知原因 ({reason})";
            }
        }

        private string GetAudioStateText(REMOTE_AUDIO_STATE state)
        {
            switch (state)
            {
                case REMOTE_AUDIO_STATE.REMOTE_AUDIO_STATE_STOPPED:
                    return "已停止";
                case REMOTE_AUDIO_STATE.REMOTE_AUDIO_STATE_STARTING:
                    return "正在启动";
                case REMOTE_AUDIO_STATE.REMOTE_AUDIO_STATE_DECODING:
                    return "正在解码";
                case REMOTE_AUDIO_STATE.REMOTE_AUDIO_STATE_FROZEN:
                    return "已冻结";
                case REMOTE_AUDIO_STATE.REMOTE_AUDIO_STATE_FAILED:
                    return "失败";
                default:
                    return $"未知状态 ({state})";
            }
        }

        private string GetAudioStateReasonText(REMOTE_AUDIO_STATE_REASON reason)
        {
            switch (reason)
            {
                case REMOTE_AUDIO_STATE_REASON.REMOTE_AUDIO_REASON_INTERNAL:
                    return "内部原因";
                case REMOTE_AUDIO_STATE_REASON.REMOTE_AUDIO_REASON_NETWORK_CONGESTION:
                    return "网络拥塞";
                case REMOTE_AUDIO_STATE_REASON.REMOTE_AUDIO_REASON_NETWORK_RECOVERY:
                    return "网络恢复";
                case REMOTE_AUDIO_STATE_REASON.REMOTE_AUDIO_REASON_LOCAL_MUTED:
                    return "本地静音";
                case REMOTE_AUDIO_STATE_REASON.REMOTE_AUDIO_REASON_LOCAL_UNMUTED:
                    return "取消静音";
                case REMOTE_AUDIO_STATE_REASON.REMOTE_AUDIO_REASON_REMOTE_MUTED:
                    return "远端静音";
                case REMOTE_AUDIO_STATE_REASON.REMOTE_AUDIO_REASON_REMOTE_UNMUTED:
                    return "远端取消静音";
                default:
                    return $"未知原因 ({reason})";
            }
        }

        private string GetNetworkQualityText(int quality)
        {
            switch (quality)
            {
                case 0: return "未知";
                case 1: return "优秀";
                case 2: return "良好";
                case 3: return "一般";
                case 4: return "较差";
                case 5: return "很差";
                case 6: return "极差";
                default: return $"未知 ({quality})";
            }
        }

        private string GetConnectionStateText(CONNECTION_STATE_TYPE state)
        {
            switch (state)
            {
                case CONNECTION_STATE_TYPE.CONNECTION_STATE_DISCONNECTED:
                    return "已断开";
                case CONNECTION_STATE_TYPE.CONNECTION_STATE_CONNECTING:
                    return "连接中";
                case CONNECTION_STATE_TYPE.CONNECTION_STATE_CONNECTED:
                    return "已连接";
                case CONNECTION_STATE_TYPE.CONNECTION_STATE_RECONNECTING:
                    return "重连中";
                case CONNECTION_STATE_TYPE.CONNECTION_STATE_FAILED:
                    return "连接失败";
                default:
                    return $"未知状态 ({state})";
            }
        }

        private string GetConnectionChangeReasonText(CONNECTION_CHANGED_REASON_TYPE reason)
        {
            switch (reason)
            {
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_CONNECTING:
                    return "正在建立连接";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_JOIN_SUCCESS:
                    return "加入频道成功";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_INTERRUPTED:
                    return "网络连接中断";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_BANNED_BY_SERVER:
                    return "被服务器禁止";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_JOIN_FAILED:
                    return "加入频道失败";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_LEAVE_CHANNEL:
                    return "离开频道";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_INVALID_APP_ID:
                    return "AppID无效";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_INVALID_CHANNEL_NAME:
                    return "频道名无效";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_INVALID_TOKEN:
                    return "Token无效";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_TOKEN_EXPIRED:
                    return "Token过期";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_REJECTED_BY_SERVER:
                    return "被服务器拒绝";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_SETTING_PROXY_SERVER:
                    return "设置代理服务器";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_RENEW_TOKEN:
                    return "Token更新";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_CLIENT_IP_ADDRESS_CHANGED:
                    return "客户端IP地址改变";
                case CONNECTION_CHANGED_REASON_TYPE.CONNECTION_CHANGED_KEEP_ALIVE_TIMEOUT:
                    return "心跳超时";
                default:
                    return $"未知原因 ({reason})";
            }
        }
    }

    public void Leave()
    {
        LogWarning($"");
        LogWarning($"║  🔘 点击了 [离开频道] 按钮           ║");
        LogMessage($"🚪 开始离开频道: {_channelName}");

        if (RtcEngine == null)
        {
            LogError("RtcEngine 未初始化，无法离开频道");
            LogError("❌ 离开频道失败！");
            return;
        }

        try
        {
            // 离开频道
            LogMessage("📤 正在发送离开频道请求...");
            int result = RtcEngine.LeaveChannel();

            if (result == 0)
            {
                LogSuccess($"✅ 离开频道请求发送成功!");
            }
            else
            {
                LogError($"❌ 离开频道失败，错误代码: {result}");
            }

            // 关闭音频模块
            RtcEngine.DisableAudio();
            LogMessage("🔇 音频模块已关闭");
            LogMessage("👋 您已离开语音频道");
        }
        catch (System.Exception ex)
        {
            LogError($"❌ 离开频道时出现异常: {ex.Message}");
        }
    }

    void OnDestroy()
    {
        LogMessage("🗑️ 清理 RtcEngine 资源...");

        if (RtcEngine != null)
        {
            try
            {
                // 离开频道
                RtcEngine.LeaveChannel();
                // 禁用音频
                RtcEngine.DisableAudio();
                // 释放资源
                RtcEngine.Dispose();
                LogSuccess("RtcEngine 资源清理完成");
            }
            catch (System.Exception ex)
            {
                LogError($"清理 RtcEngine 资源时出现异常: {ex.Message}");
            }
        }
    }

    void Update()
    {
        // Update方法保留为空，避免不必要的性能开销
        // 权限检查已移至 Start() 中的 CheckPermissionsAsync() 协程
    }

    /// <summary>
    /// 异步检查和请求Android权限（协程方式，避免闪退）
    /// 优化版：支持权限说明、重试机制、跳转设置
    /// </summary>
    private IEnumerator CheckPermissionsAsync()
    {
#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
        LogMessage("║  🔐 开始检查Android运行时权限       ║");

        // 等待一帧，确保UI已经初始化完成
        yield return null;

        bool allPermissionsGranted = true;
        int deniedCount = 0;
        List<string> deniedPermissions = new List<string>();

        foreach (string permission in permissionList)
        {
            string permissionName = GetPermissionName(permission);
            LogMessage($"");
            LogMessage($"📋 检查权限: {permissionName}");
            LogMessage($"   - 系统权限: {permission}");

            if (!Permission.HasUserAuthorizedPermission(permission))
            {
                LogWarning($"⚠️ 缺少权限，正在请求...");
                LogMessage($"   - 用途: {GetPermissionDescription(permission)}");
                allPermissionsGranted = false;

                // 请求权限
                Permission.RequestUserPermission(permission);
                LogMessage($"   ⏳ 等待用户授予权限...");

                // 等待用户响应（最多等待10秒，增加超时时间）
                float timeout = 10f;
                float elapsed = 0f;

                while (!Permission.HasUserAuthorizedPermission(permission) && elapsed < timeout)
                {
                    yield return new WaitForSeconds(0.5f);
                    elapsed += 0.5f;

                    // 每2秒显示一次等待提示
                    if ((int)elapsed % 2 == 0 && elapsed > 0)
                    {
                        LogMessage($"   ⏰ 已等待 {(int)elapsed} 秒...");
                    }
                }

                // 检查权限是否被授予
                if (Permission.HasUserAuthorizedPermission(permission))
                {
                    LogSuccess($"✅ 权限已授予: {permissionName}");
                }
                else
                {
                    deniedCount++;
                    deniedPermissions.Add(permissionName);
                    LogError($"❌ 权限被拒绝: {permissionName}");
                    LogWarning($"   ⚠️ 影响: {GetPermissionImpact(permission)}");
                }
            }
            else
            {
                LogSuccess($"✅ 已有权限: {permissionName}");
            }
        }

        LogMessage($"");
        LogMessage($"═════════════════════════════════════════");

        if (allPermissionsGranted)
        {
            LogSuccess($"║  ✅ 所有必需权限已授予               ║");
            LogMessage($"📊 权限统计:");
            LogMessage($"   - 已授予: {permissionList.Count} 项");
            LogMessage($"   - 被拒绝: 0 项");
        }
        else
        {
            LogError($"║  ⚠️ 部分权限未授予                   ║");
            LogMessage($"📊 权限统计:");
            LogMessage($"   - 已授予: {permissionList.Count - deniedCount} 项");
            LogMessage($"   - 被拒绝: {deniedCount} 项");
            LogMessage($"");
            LogWarning($"❌ 被拒绝的权限:");
            foreach (var perm in deniedPermissions)
            {
                LogWarning($"   - {perm}");
            }
            LogMessage($"");
            LogWarning($"📱 如何手动授予权限:");
            LogWarning($"   1. 打开手机「设置」");
            LogWarning($"   2. 找到「应用管理」或「应用程序」");
            LogWarning($"   3. 找到本应用");
            LogWarning($"   4. 点击「权限」");
            LogWarning($"   5. 开启所有被拒绝的权限");
            LogWarning($"   6. 重新启动应用");

            // 可选：显示重试提示
            LogMessage($"");
            LogMessage($"💡 提示: 您也可以重启应用重新授予权限");
        }

        LogMessage($"═════════════════════════════════════════");
#else
        LogMessage("ℹ️ 非Android平台，跳过权限检查");
        yield return null;
#endif
    }

    /// <summary>
    /// 获取权限的友好名称
    /// </summary>
    private string GetPermissionName(string permission)
    {
        if (permission.Contains("CAMERA"))
            return "相机权限";
        else if (permission.Contains("MICROPHONE") || permission.Contains("RECORD_AUDIO"))
            return "麦克风权限";
        else if (permission.Contains("WRITE_EXTERNAL_STORAGE"))
            return "存储写入权限";
        else if (permission.Contains("READ_EXTERNAL_STORAGE"))
            return "存储读取权限";
        else
            return permission;
    }

    /// <summary>
    /// 获取权限的用途说明
    /// </summary>
    private string GetPermissionDescription(string permission)
    {
        if (permission.Contains("CAMERA"))
            return "用于视频通话（当前仅语音，暂不需要）";
        else if (permission.Contains("MICROPHONE") || permission.Contains("RECORD_AUDIO"))
            return "用于采集麦克风音频，进行语音通话";
        else if (permission.Contains("WRITE_EXTERNAL_STORAGE"))
            return "用于保存录音文件和日志";
        else if (permission.Contains("READ_EXTERNAL_STORAGE"))
            return "用于读取音频文件";
        else
            return "应用正常运行所需";
    }

    /// <summary>
    /// 获取权限被拒绝后的影响
    /// </summary>
    private string GetPermissionImpact(string permission)
    {
        if (permission.Contains("CAMERA"))
            return "当前仅语音通话，影响较小";
        else if (permission.Contains("MICROPHONE") || permission.Contains("RECORD_AUDIO"))
            return "无法进行语音通话！（严重影响）";
        else if (permission.Contains("WRITE_EXTERNAL_STORAGE"))
            return "无法保存录音和日志";
        else if (permission.Contains("READ_EXTERNAL_STORAGE"))
            return "无法读取本地音频文件";
        else
            return "某些功能可能无法正常工作";
    }

    /// <summary>
    /// 【已废弃】旧的同步权限检查方法（可能导致闪退）
    /// 请使用 CheckPermissionsAsync() 代替
    /// </summary>
    [System.Obsolete("此方法可能导致闪退，请使用 CheckPermissionsAsync() 代替")]
    private void CheckPermissions()
    {
#if (UNITY_2018_3_OR_NEWER && UNITY_ANDROID)
        foreach (string permission in permissionList) {
            if (!Permission.HasUserAuthorizedPermission(permission)) {
                Permission.RequestUserPermission(permission);
            }
        }
#endif
    }

    // ============= 日志辅助方法 =============

    /// <summary>
    /// 初始化日志系统
    /// </summary>
    private void InitializeLogger()
    {
        // 如果没有指定LogText，尝试自动查找
        if (LogText == null)
        {
            LogText = GameObject.Find("Canvas/LogText")?.GetComponent<Text>();

            // 如果还是找不到，创建一个
            if (LogText == null)
            {
                CreateLogText();
            }
        }

        if (LogText != null)
        {
            LogText.text = "=== Agora RTC 日志 ===\n";
            LogText.fontSize = 24;
            LogText.color = Color.white;
            LogText.alignment = TextAnchor.UpperLeft;
            Debug.Log("✅ 日志Text组件初始化成功");
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到LogText组件，日志将只在控制台输出");
        }
    }

    /// <summary>
    /// 创建日志Text组件
    /// </summary>
    private void CreateLogText()
    {
        GameObject canvasGo = GameObject.Find("Canvas");
        if (canvasGo == null)
        {
            Debug.LogError("❌ 未找到Canvas对象，无法创建LogText");
            return;
        }

        GameObject logTextGo = new GameObject("LogText");
        logTextGo.transform.SetParent(canvasGo.transform, false);
        logTextGo.layer = LayerMask.NameToLayer("UI");

        RectTransform rt = logTextGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.3f);
        rt.anchorMax = new Vector2(1, 0.9f);
        rt.offsetMin = new Vector2(20, 0);
        rt.offsetMax = new Vector2(-20, -20);

        LogText = logTextGo.AddComponent<Text>();
        LogText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        LogText.fontSize = 24;
        LogText.color = Color.white;
        LogText.alignment = TextAnchor.UpperLeft;
        LogText.verticalOverflow = VerticalWrapMode.Overflow;
        LogText.horizontalOverflow = HorizontalWrapMode.Wrap;

        // 添加背景
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(logTextGo.transform, false);
        bgGo.layer = LayerMask.NameToLayer("UI");
        bgGo.transform.SetAsFirstSibling();

        RectTransform bgRt = bgGo.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        Image bgImg = bgGo.AddComponent<Image>();
        bgImg.color = new Color(0, 0, 0, 0.7f);

        Debug.Log("✅ 自动创建LogText组件成功");
    }

    /// <summary>
    /// 添加日志消息（同时输出到控制台和UI）
    /// </summary>
    /// <param name="message">日志消息</param>
    private void LogMessage(string message)
    {
        // 输出到Unity控制台
        Debug.Log(message);

        // 输出到UI
        if (LogText != null)
        {
            // 添加时间戳
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string logLine = $"[{timestamp}] {message}";

            // 添加到日志构建器
            logBuilder.AppendLine(logLine);

            // 限制日志行数
            string[] lines = logBuilder.ToString().Split('\n');
            if (lines.Length > maxLogLines)
            {
                logBuilder.Clear();
                for (int i = lines.Length - maxLogLines; i < lines.Length; i++)
                {
                    logBuilder.AppendLine(lines[i]);
                }
            }

            // 更新UI
            LogText.text = logBuilder.ToString();
        }
    }

    /// <summary>
    /// 添加错误日志
    /// </summary>
    private void LogError(string message)
    {
        Debug.LogError(message);

        if (LogText != null)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string logLine = $"[{timestamp}] <color=red>❌ {message}</color>";
            logBuilder.AppendLine(logLine);

            // 限制日志行数
            string[] lines = logBuilder.ToString().Split('\n');
            if (lines.Length > maxLogLines)
            {
                logBuilder.Clear();
                for (int i = lines.Length - maxLogLines; i < lines.Length; i++)
                {
                    logBuilder.AppendLine(lines[i]);
                }
            }

            LogText.text = logBuilder.ToString();
        }
    }

    /// <summary>
    /// 添加警告日志
    /// </summary>
    private void LogWarning(string message)
    {
        Debug.LogWarning(message);

        if (LogText != null)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string logLine = $"[{timestamp}] <color=yellow>⚠️ {message}</color>";
            logBuilder.AppendLine(logLine);

            // 限制日志行数
            string[] lines = logBuilder.ToString().Split('\n');
            if (lines.Length > maxLogLines)
            {
                logBuilder.Clear();
                for (int i = lines.Length - maxLogLines; i < lines.Length; i++)
                {
                    logBuilder.AppendLine(lines[i]);
                }
            }

            LogText.text = logBuilder.ToString();
        }
    }

    /// <summary>
    /// 添加成功日志
    /// </summary>
    private void LogSuccess(string message)
    {
        Debug.Log(message);

        if (LogText != null)
        {
            string timestamp = System.DateTime.Now.ToString("HH:mm:ss");
            string logLine = $"[{timestamp}] <color=green>✅ {message}</color>";
            logBuilder.AppendLine(logLine);

            // 限制日志行数
            string[] lines = logBuilder.ToString().Split('\n');
            if (lines.Length > maxLogLines)
            {
                logBuilder.Clear();
                for (int i = lines.Length - maxLogLines; i < lines.Length; i++)
                {
                    logBuilder.AppendLine(lines[i]);
                }
            }

            LogText.text = logBuilder.ToString();
        }
    }

}