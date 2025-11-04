using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// TTS 音频播放器
/// 用于处理和播放来自 RKTTS 的 WAV 音频数据
/// 支持流式播放和音频可视化
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class TTSAudioPlayer : MonoBehaviour
{
    [Header("音频配置")]
    [Tooltip("采样率（Hz）- RKTTS 使用 44100")]
    public int sampleRate = 44100;

    [Tooltip("声道数（1=单声道，2=立体声）")]
    public int channels = 1;

    [Tooltip("音量 (0-1)")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Header("可视化")]
    [Tooltip("是否启用音频可视化")]
    public bool enableVisualization = false;

    [Tooltip("可视化采样数量")]
    public int visualizationSamples = 64;

    private AudioSource audioSource;
    private List<float> audioBuffer = new List<float>();
    private float[] visualizationData;
    private bool isPlaying = false;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.volume = volume;

        if (enableVisualization)
        {
            visualizationData = new float[visualizationSamples];
        }
    }

    void Update()
    {
        // 更新可视化数据
        if (enableVisualization && audioSource.isPlaying)
        {
            audioSource.GetOutputData(visualizationData, 0);
        }
    }

    /// <summary>
    /// 添加音频数据到缓冲区
    /// </summary>
    public void AddAudioData(float[] data)
    {
        if (data != null && data.Length > 0)
        {
            audioBuffer.AddRange(data);
            Debug.Log($"TTSAudioPlayer: 添加 {data.Length} 个采样，总计: {audioBuffer.Count}");
        }
    }

    /// <summary>
    /// 播放缓冲区中的音频
    /// </summary>
    public void Play()
    {
        if (audioBuffer.Count == 0)
        {
            Debug.LogWarning("TTSAudioPlayer: 音频缓冲区为空");
            return;
        }

        StopIfPlaying();

        try
        {
            // 创建 AudioClip
            AudioClip clip = AudioClip.Create(
                "TTS_Audio_" + Time.time,
                audioBuffer.Count,
                channels,
                sampleRate,
                false
            );

            // 设置音频数据
            clip.SetData(audioBuffer.ToArray(), 0);

            // 播放
            audioSource.clip = clip;
            audioSource.Play();
            isPlaying = true;

            float duration = (float)audioBuffer.Count / sampleRate;
            Debug.Log($"TTSAudioPlayer: 播放音频 - 时长: {duration:F2}秒, 采样数: {audioBuffer.Count}, 采样率: {sampleRate}Hz");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"TTSAudioPlayer: 播放失败 - {e.Message}");
            isPlaying = false;
        }
    }

    /// <summary>
    /// 停止播放
    /// </summary>
    public void Stop()
    {
        StopIfPlaying();
        isPlaying = false;
    }

    /// <summary>
    /// 清空音频缓冲区
    /// </summary>
    public void ClearBuffer()
    {
        audioBuffer.Clear();
        Debug.Log("TTSAudioPlayer: 清空音频缓冲区");
    }

    /// <summary>
    /// 重置（停止并清空）
    /// </summary>
    public void Reset()
    {
        Stop();
        ClearBuffer();
    }

    /// <summary>
    /// 获取当前是否正在播放
    /// </summary>
    public bool IsPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }

    /// <summary>
    /// 获取音频缓冲区大小（采样数）
    /// </summary>
    public int GetBufferSize()
    {
        return audioBuffer.Count;
    }

    /// <summary>
    /// 获取音频时长（秒）
    /// </summary>
    public float GetDuration()
    {
        if (audioBuffer.Count == 0 || sampleRate == 0)
            return 0f;

        return (float)audioBuffer.Count / sampleRate;
    }

    /// <summary>
    /// 获取可视化数据（用于音频波形显示）
    /// </summary>
    public float[] GetVisualizationData()
    {
        return visualizationData;
    }

    /// <summary>
    /// 设置采样率
    /// </summary>
    public void SetSampleRate(int rate)
    {
        sampleRate = rate;
        Debug.Log($"TTSAudioPlayer: 设置采样率为 {sampleRate} Hz");
    }

    /// <summary>
    /// 设置音量
    /// </summary>
    public void SetVolume(float newVolume)
    {
        volume = Mathf.Clamp01(newVolume);
        if (audioSource != null)
        {
            audioSource.volume = volume;
        }
    }

    /// <summary>
    /// 保存音频到 WAV 文件（仅用于调试）
    /// </summary>
    public void SaveToWavFile(string filename)
    {
        if (audioBuffer.Count == 0)
        {
            Debug.LogWarning("TTSAudioPlayer: 无音频数据可保存");
            return;
        }

        string path = System.IO.Path.Combine(Application.persistentDataPath, filename);

        try
        {
            WAVWriter.WriteWAV(path, audioBuffer.ToArray(), sampleRate, channels);
            Debug.Log($"TTSAudioPlayer: 音频已保存到 {path}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"TTSAudioPlayer: 保存音频失败 - {e.Message}");
        }
    }

    private void StopIfPlaying()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }

    void OnDestroy()
    {
        Stop();
    }
}

/// <summary>
/// WAV 文件写入工具
/// </summary>
public static class WAVWriter
{
    public static void WriteWAV(string filepath, float[] audioData, int sampleRate, int channels)
    {
        using (var fileStream = new System.IO.FileStream(filepath, System.IO.FileMode.Create))
        using (var writer = new System.IO.BinaryWriter(fileStream))
        {
            int byteRate = sampleRate * channels * 2; // 16-bit = 2 bytes
            int dataSize = audioData.Length * 2;
            int fileSize = 36 + dataSize;

            // WAV Header
            writer.Write(new char[4] { 'R', 'I', 'F', 'F' }); // ChunkID
            writer.Write(fileSize); // ChunkSize
            writer.Write(new char[4] { 'W', 'A', 'V', 'E' }); // Format

            // fmt subchunk
            writer.Write(new char[4] { 'f', 'm', 't', ' ' }); // Subchunk1ID
            writer.Write(16); // Subchunk1Size (PCM = 16)
            writer.Write((ushort)1); // AudioFormat (PCM = 1)
            writer.Write((ushort)channels); // NumChannels
            writer.Write(sampleRate); // SampleRate
            writer.Write(byteRate); // ByteRate
            writer.Write((ushort)(channels * 2)); // BlockAlign
            writer.Write((ushort)16); // BitsPerSample

            // data subchunk
            writer.Write(new char[4] { 'd', 'a', 't', 'a' }); // Subchunk2ID
            writer.Write(dataSize); // Subchunk2Size

            // Write audio data
            foreach (float sample in audioData)
            {
                // Convert float (-1 to 1) to 16-bit PCM (-32768 to 32767)
                short pcmSample = (short)(sample * 32767f);
                writer.Write(pcmSample);
            }
        }
    }
}
