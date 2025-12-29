/// <summary>
/// 数据回调监听器接口（泛型）
/// 用于处理 TTS 和 STT 的数据流回调
/// </summary>
/// <typeparam name="T">数据类型，TTS 使用 float[]，STT 使用 string</typeparam>
public interface DataCallbackListener<T>
{
    /// <summary>
    /// 数据块回调（流式数据）
    /// </summary>
    /// <param name="data">数据块</param>
    void OnDataChunkCallback(T data);

    /// <summary>
    /// 数据完成回调（最终数据）
    /// </summary>
    /// <param name="data">完整数据</param>
    void OnDataFinishCallback(T data);
}
