using UnityEngine;

/// <summary>
/// TTS 采样率诊断和修复工具
/// 用于检查和修复 TTS 采样率配置问题
/// </summary>
public class TTSSampleRateFix : MonoBehaviour
{
    [Header("目标采样率")]
    [Tooltip("期望的采样率（Hz）")]
    public int targetSampleRate = 44100;

    [Header("调试")]
    [Tooltip("是否启用调试日志")]
    public bool enableDebugLog = true;

    void Start()
    {
        DiagnoseAndFix();
    }

    /// <summary>
    /// 诊断并修复采样率问题
    /// </summary>
    [ContextMenu("诊断采样率")]
    public void DiagnoseAndFix()
    {
        if (enableDebugLog)
            LoggerManager.Info("========== TTS 采样率诊断开始 ==========", "TTS");

        // 检查 SenseOnnxManager
        if (SenseOnnxManager.Instance == null)
        {
            LoggerManager.Error("❌ 未找到 SenseOnnxManager", "TTS");
            return;
        }

        if (enableDebugLog)
            LoggerManager.Info($"✅ 找到 SenseOnnxManager", "TTS");

        // 检查 TTS 采样率
        int ttsSampleRate = SenseOnnxManager.Instance.GetTtsSampleRate();
        if (enableDebugLog)
            LoggerManager.Debug($"SenseOnnx TTS Sample Rate = {ttsSampleRate} Hz", "TTS");

        if (ttsSampleRate <= 0)
        {
            LoggerManager.Warning("⚠️ TTS 采样率无效或未初始化 (0 Hz)", "TTS");
        }

        // 检查 Unity 音频系统配置
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        int outputSampleRate = AudioSettings.outputSampleRate;

        if (enableDebugLog)
        {
            LoggerManager.Debug($"Unity 音频系统配置:", "TTS");
            LoggerManager.Debug($"  System Sample Rate: {audioConfig.sampleRate} Hz", "TTS");
            LoggerManager.Debug($"  Output Sample Rate: {outputSampleRate} Hz", "TTS");
            LoggerManager.Debug($"  DSP Buffer Size: {audioConfig.dspBufferSize}", "TTS");
        }

        if (ttsSampleRate > 0 && outputSampleRate != ttsSampleRate)
        {
            LoggerManager.Warning($"⚠️ Unity 输出采样率 ({outputSampleRate} Hz) 与 TTS 采样率 ({ttsSampleRate} Hz) 不一致", "TTS");
            LoggerManager.Warning($"  这会导致 Unity 自动进行重采样，通常是可以接受的。", "TTS");
            LoggerManager.Warning($"  如果为了最佳性能，请考虑在 ProjectSettings 中调整 System Sample Rate。", "TTS");
        }
        else if (ttsSampleRate > 0)
        {
            if (enableDebugLog)
                LoggerManager.Info($"✅ Unity 输出采样率与 TTS 采样率一致 ({ttsSampleRate} Hz)", "TTS");
        }

        if (enableDebugLog)
        {
            LoggerManager.Info("========== TTS 采样率诊断完成 ==========", "TTS");
        }
    }
}
