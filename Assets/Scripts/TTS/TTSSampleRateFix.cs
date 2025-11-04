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
            Debug.Log("========== TTS 采样率诊断开始 ==========");

        // 查找 RKTTSManager
        RKTTSManager ttsManager = FindObjectOfType<RKTTSManager>();
        if (ttsManager == null)
        {
            Debug.LogError("TTSSampleRateFix: ❌ 未找到 RKTTSManager");
            return;
        }

        if (enableDebugLog)
            Debug.Log($"TTSSampleRateFix: ✅ 找到 RKTTSManager");

        // 检查 RKTTSManager 的采样率
        int managerSampleRate = ttsManager.sampleRate;
        if (enableDebugLog)
            Debug.Log($"TTSSampleRateFix: RKTTSManager.sampleRate = {managerSampleRate} Hz");

        if (managerSampleRate != targetSampleRate)
        {
            Debug.LogWarning($"TTSSampleRateFix: ⚠️ RKTTSManager 采样率不正确！");
            Debug.LogWarning($"  当前: {managerSampleRate} Hz");
            Debug.LogWarning($"  期望: {targetSampleRate} Hz");
            Debug.LogWarning($"  正在修复...");

            // 修复 RKTTSManager 采样率
            ttsManager.sampleRate = targetSampleRate;
            ttsManager.SetSampleRate(targetSampleRate);

            if (enableDebugLog)
                Debug.Log($"TTSSampleRateFix: ✅ 已修复 RKTTSManager 采样率为 {targetSampleRate} Hz");
        }
        else
        {
            if (enableDebugLog)
                Debug.Log($"TTSSampleRateFix: ✅ RKTTSManager 采样率正确");
        }

        // 检查 TTSAudioPlayer
        TTSAudioPlayer audioPlayer = ttsManager.audioPlayer;
        if (audioPlayer == null)
        {
            audioPlayer = ttsManager.GetComponent<TTSAudioPlayer>();
        }

        if (audioPlayer == null)
        {
            Debug.LogWarning($"TTSSampleRateFix: ⚠️ 未找到 TTSAudioPlayer，尝试创建...");
            audioPlayer = ttsManager.gameObject.AddComponent<TTSAudioPlayer>();
            ttsManager.audioPlayer = audioPlayer;
        }

        if (enableDebugLog)
            Debug.Log($"TTSSampleRateFix: ✅ 找到 TTSAudioPlayer");

        // 检查 AudioPlayer 的采样率
        int playerSampleRate = audioPlayer.sampleRate;
        if (enableDebugLog)
            Debug.Log($"TTSSampleRateFix: TTSAudioPlayer.sampleRate = {playerSampleRate} Hz");

        if (playerSampleRate != targetSampleRate)
        {
            Debug.LogWarning($"TTSSampleRateFix: ⚠️ TTSAudioPlayer 采样率不正确！");
            Debug.LogWarning($"  当前: {playerSampleRate} Hz");
            Debug.LogWarning($"  期望: {targetSampleRate} Hz");
            Debug.LogWarning($"  正在修复...");

            // 修复 AudioPlayer 采样率
            audioPlayer.sampleRate = targetSampleRate;
            audioPlayer.SetSampleRate(targetSampleRate);

            if (enableDebugLog)
                Debug.Log($"TTSSampleRateFix: ✅ 已修复 TTSAudioPlayer 采样率为 {targetSampleRate} Hz");
        }
        else
        {
            if (enableDebugLog)
                Debug.Log($"TTSSampleRateFix: ✅ TTSAudioPlayer 采样率正确");
        }

        // 检查 Unity 音频系统配置
        AudioConfiguration audioConfig = AudioSettings.GetConfiguration();
        if (enableDebugLog)
        {
            Debug.Log($"TTSSampleRateFix: Unity 音频系统配置:");
            Debug.Log($"  Sample Rate: {audioConfig.sampleRate} Hz");
            Debug.Log($"  Output Sample Rate: {AudioSettings.outputSampleRate} Hz");
            Debug.Log($"  DSP Buffer Size: {audioConfig.dspBufferSize}");
        }

        if (audioConfig.sampleRate != targetSampleRate && AudioSettings.outputSampleRate != targetSampleRate)
        {
            Debug.LogWarning($"TTSSampleRateFix: ⚠️ Unity 音频系统采样率不是 {targetSampleRate} Hz");
            Debug.LogWarning($"  注意: 这需要在 ProjectSettings/AudioManager.asset 中配置");
            Debug.LogWarning($"  或者 TTSAudioPlayer 会自动使用 pitch 补偿");
        }

        if (enableDebugLog)
        {
            Debug.Log("========== TTS 采样率诊断完成 ==========");
            Debug.Log("✅ 采样率配置检查完成");
            Debug.Log($"RKTTSManager.sampleRate = {ttsManager.sampleRate} Hz");
            Debug.Log($"TTSAudioPlayer.sampleRate = {audioPlayer.sampleRate} Hz");
            Debug.Log("如果仍有问题，请检查:");
            Debug.Log("1. RKTTSManager Inspector 中的 Sample Rate 设置");
            Debug.Log("2. TTSAudioPlayer Inspector 中的 Sample Rate 设置");
            Debug.Log("3. ProjectSettings → Audio → System Sample Rate");
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

        Debug.Log("========================================");
        Debug.Log("✅ 已强制重置所有采样率为 44100 Hz");
        Debug.Log("========================================");
    }
}
