using UnityEngine;
using TMPro;

// 类名必须与文件名匹配，否则IL2CPP构建会失败
public class FPSCounter : MonoBehaviour
{
    public float updateInterval = 0.5f;
    public TextMeshProUGUI fpsText;
    
    private int frameCount = 0;
    private float elapsedTime = 0f;
    private float currentFPS = 0f;

    void Update()
    {
        frameCount++;
        elapsedTime += Time.unscaledDeltaTime;
        
        if (elapsedTime >= updateInterval)
        {
            currentFPS = frameCount / elapsedTime;
            frameCount = 0;
            elapsedTime = 0f;
            
            if (fpsText != null)
                fpsText.text = $"FPS: {currentFPS:F1}";
        }
    }
}