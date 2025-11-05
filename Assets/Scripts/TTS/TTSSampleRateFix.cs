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
    [ContextMenu("诊断并修复采样率")]
    public void DiagnoseAndFix()
    {
        if (enableDebugLog)
            LoggerManager.Info("========== TTS 采样率诊断开始 ==========", "TTS");

        // 查找 RKTTSManager
        RKTTSManager ttsManager = FindObjectOfType<RKTTSManager>();
        if (ttsManager == null)
        {
            LoggerManager.Error("❌ 未找到 RKTTSManager", "TTS");
            return;
        }

        if (enableDebugLog)
            LoggerManager.Info($"✅ 找到 RKTTSManager", "TTS");

        // 检查 RKTTSManager 的采样率
        int managerSampleRate = ttsManager.sampleRate;
        if (enableDebugLog)
            LoggerManager.Debug($"RKTTSManager.sampleRate = {managerSampleRate} Hz", "TTS");

        if (managerSampleRate != targetSampleRate)
        {
            LoggerManager.Warning($"⚠️ RKTTSManager 采样率不正确！", "TTS");
            LoggerManager.Warning($"  当前: {managerSampleRate} Hz", "TTS");
            LoggerManager.Warning($"  期望: {targetSampleRate} Hz", "TTS");
            LoggerManager.Warning($"  正在修复...", "TTS");

            // 修复 RKTTSManager 采样率
            ttsManager.sampleRate = targetSampleRate;
            ttsManager.SetSampleRate(targetSampleRate);

            if (enableDebugLog)
                LoggerManager.Info($"✅ 已修复 RKTTSManager 采样率为 {targetSampleRate} Hz", "TTS");
        }
        else
        {
            if (enableDebugLog)
                LoggerManager.Info($"✅ RKTTSManager 采样率正确", "TTS");
        }

        // 检查 TTSAudioPlayer
        TTSAudioPlayer audioPlayer = ttsManager.audioPlayer;
        if (audioPlayer == null)
        {
            audioPlayer = ttsManager.GetComponent<TTSAudioPlayer>();
        }

        if (audioPlayer == null)
        {
            LoggerManager.Warning($"⚠️ 未找到 TTSAudioPlayer，尝试创建...", "TTS");
            audioPlayer = ttsManager.gameObject.AddComponent<TTSAudioPlayer>();
            ttsManager.audioPlayer = audioPlayer;
        }

        if (enableDebugLog)
            LoggerManager.Info($"✅ 找到 TTSAudioPlayer", "TTS");

        // 检查 AudioPlayer 的采样率
        int playerSampleRate = audioPlayer.sampleRate;
        if (enableDebugLog)
            LoggerManager.Debug($"TTSAudioPlayer.sampleRate = {playerSampleRate} Hz", "TTS");

        if (playerSampleRate != targetSampleRate)
        {
            LoggerManager.Warning($"⚠️ TTSAudioPlayer 采样率不正确！", "TTS");
            LoggerManager.Warning($"  当前: {playerSampleRate} Hz", "TTS");
            LoggerManager.Warning($"  期望: {targetSampleRate} Hz", "TTS");
            LoggerManager.Warning($"  正在修复...", "TTS");

            // 修复 AudioPlayer 采样率
            audioPlayer.sampleRate = targetSampleRate;
            audioPlayer.SetSampleRate(targetSampleRate);

            if (enableDebugLog)
                LoggerManager.Info($"✅ 已修复 TTSAudioPlayer 采样率为 {targetSampleRate} Hz", "TTS");
        }
        else
        {
            if (enableDebugLog)
                LoggerManager.Info($"✅ TTSAudioPlayer 采样率正确", "TTS");
        }

        // 检查 Unity 音频系统配置
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        if (enableDebugLog)
        {
            LoggerManager.Debug($"Unity 音频系统配置:", "TTS");
            LoggerManager.Debug($"  Sample Rate: {audioConfig.sampleRate} Hz", "TTS");
            LoggerManager.Debug($"  Output Sample Rate: {AudioSettings.outputSampleRate} Hz", "TTS");
            LoggerManager.Debug($"  DSP Buffer Size: {audioConfig.dspBufferSize}", "TTS");
        }

        if (audioConfig.sampleRate != targetSampleRate && AudioSettings.outputSampleRate != targetSampleRate)
        {
            LoggerManager.Warning($"⚠️ Unity 音频系统采样率不是 {targetSampleRate} Hz", "TTS");
            LoggerManager.Warning($"  注意: 这需要在 ProjectSettings/AudioManager.asset 中配置", "TTS");
            LoggerManager.Warning($"  或者 TTSAudioPlayer 会自动使用 pitch 补偿", "TTS");
        }

        if (enableDebugLog)
        {
            LoggerManager.Info("========== TTS 采样率诊断完成 ==========", "TTS");
            LoggerManager.Info("✅ 采样率配置检查完成", "TTS");
            LoggerManager.Info($"RKTTSManager.sampleRate = {ttsManager.sampleRate} Hz", "TTS");
            LoggerManager.Info($"TTSAudioPlayer.sampleRate = {audioPlayer.sampleRate} Hz", "TTS");
            LoggerManager.Info("如果仍有问题，请检查:", "TTS");
            LoggerManager.Info("1. RKTTSManager Inspector 中的 Sample Rate 设置", "TTS");
            LoggerManager.Info("2. TTSAudioPlayer Inspector 中的 Sample Rate 设置", "TTS");
            LoggerManager.Info("3. ProjectSettings → Audio → System Sample Rate", "TTS");
        }
    }

    /// <summary>
    /// 强制重置所有采样率为 44100 Hz
    /// </summary>
    [ContextMenu("强制重置为 44100 Hz")]
    public void ForceReset44100()
    {
        targetSampleRate = 44100;
        DiagnoseAndFix();

        LoggerManager.Info("========================================", "TTS");
        LoggerManager.Info("✅ 已强制重置所有采样率为 44100 Hz", "TTS");
        LoggerManager.Info("========================================", "TTS");
    }
}
