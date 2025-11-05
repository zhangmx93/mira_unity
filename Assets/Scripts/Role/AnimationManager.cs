using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 动画管理器 - 统一管理角色动画的播放和切换
/// 提供简单的 API 供其他脚本调用
/// </summary>
public class AnimationManager : MonoBehaviour
{
    [Header("动画组件引用")]
    [Tooltip("Animator 组件引用")]
    public Animator animator;

    [Header("动画设置")]
    [Tooltip("是否在切换动画时使用淡入淡出效果")]
    public bool useCrossFade = true;

    [Tooltip("淡入淡出过渡时间（秒）")]
    [Range(0.1f, 2f)]
    public float crossFadeDuration = 0.3f;

    [Tooltip("是否允许动画中断（切换到新动画）")]
    public bool allowInterruption = true;

    [Header("调试")]
    [Tooltip("是否打印调试日志")]
    public bool enableDebugLog = true;

    // 当前播放的动画状态
    private string currentAnimationState = "";

    // 上一个播放的动画状态
    private string previousAnimationState = "";

    // 动画层级（默认为 0）
    private int animationLayer = 0;

    // 动画状态缓存
    private Dictionary<string, int> animationHashCache = new Dictionary<string, int>();

    #region Unity 生命周期

    void Awake()
    {
        // 自动获取 Animator 组件
        if (animator == null)
        {
            animator = GetComponent<Animator>();
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        if (animator == null)
        {
            LoggerManager.Error("未找到 Animator 组件！请确保角色上有 Animator 组件。", "Animation");
        }
    }

    void Start()
    {
        if (animator != null)
        {
            // 获取当前动画状态
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(animationLayer);
            currentAnimationState = GetStateName(stateInfo);

            LogDebug($"初始化完成，当前动画: {currentAnimationState}");
        }
    }

    #endregion

    #region 动画播放方法

    /// <summary>
    /// 播放指定动画
    /// </summary>
    /// <param name="animationName">动画状态名称</param>
    public void PlayAnimation(string animationName)
    {
        if (!ValidateAnimator()) return;

        if (string.IsNullOrEmpty(animationName))
        {
            LoggerManager.Warning("动画名称为空！", "Animation");
            return;
        }

        // 检查是否已经在播放该动画
        if (currentAnimationState == animationName && !allowInterruption)
        {
            LogDebug($"动画 '{animationName}' 已在播放中，跳过切换");
            return;
        }

        previousAnimationState = currentAnimationState;
        currentAnimationState = animationName;

        if (useCrossFade)
        {
            animator.CrossFade(animationName, crossFadeDuration, animationLayer);
            LogDebug($"淡入播放动画: {animationName} (过渡时间: {crossFadeDuration}s)");
        }
        else
        {
            animator.Play(animationName, animationLayer);
            LogDebug($"直接播放动画: {animationName}");
        }
    }

    /// <summary>
    /// 播放指定动画（使用 Hash 优化性能）
    /// </summary>
    /// <param name="animationName">动画状态名称</param>
    public void PlayAnimationOptimized(string animationName)
    {
        if (!ValidateAnimator()) return;

        int animationHash = GetAnimationHash(animationName);

        previousAnimationState = currentAnimationState;
        currentAnimationState = animationName;

        if (useCrossFade)
        {
            animator.CrossFade(animationHash, crossFadeDuration, animationLayer);
        }
        else
        {
            animator.Play(animationHash, animationLayer);
        }

        LogDebug($"播放动画 (优化): {animationName}");
    }

    /// <summary>
    /// 播放动画并指定过渡时间
    /// </summary>
    /// <param name="animationName">动画状态名称</param>
    /// <param name="transitionDuration">过渡时间（秒）</param>
    public void PlayAnimation(string animationName, float transitionDuration)
    {
        if (!ValidateAnimator()) return;

        previousAnimationState = currentAnimationState;
        currentAnimationState = animationName;

        animator.CrossFade(animationName, transitionDuration, animationLayer);
        LogDebug($"播放动画: {animationName} (自定义过渡: {transitionDuration}s)");
    }

    /// <summary>
    /// 立即播放动画（无过渡）
    /// </summary>
    /// <param name="animationName">动画状态名称</param>
    public void PlayAnimationImmediate(string animationName)
    {
        if (!ValidateAnimator()) return;

        previousAnimationState = currentAnimationState;
        currentAnimationState = animationName;

        animator.Play(animationName, animationLayer, 0f);
        LogDebug($"立即播放动画: {animationName}");
    }

    /// <summary>
    /// 回到上一个动画状态
    /// </summary>
    public void PlayPreviousAnimation()
    {
        if (!string.IsNullOrEmpty(previousAnimationState))
        {
            PlayAnimation(previousAnimationState);
        }
        else
        {
            LoggerManager.Warning("没有上一个动画状态！", "Animation");
        }
    }

    #endregion

    #region 动画参数设置

    /// <summary>
    /// 设置布尔类型参数
    /// </summary>
    public void SetBool(string parameterName, bool value)
    {
        if (ValidateAnimator())
        {
            animator.SetBool(parameterName, value);
            LogDebug($"设置参数 {parameterName} = {value}");
        }
    }

    /// <summary>
    /// 设置整数类型参数
    /// </summary>
    public void SetInt(string parameterName, int value)
    {
        if (ValidateAnimator())
        {
            animator.SetInteger(parameterName, value);
            LogDebug($"设置参数 {parameterName} = {value}");
        }
    }

    /// <summary>
    /// 设置浮点类型参数
    /// </summary>
    public void SetFloat(string parameterName, float value)
    {
        if (ValidateAnimator())
        {
            animator.SetFloat(parameterName, value);
            LogDebug($"设置参数 {parameterName} = {value}");
        }
    }

    /// <summary>
    /// 触发动画触发器
    /// </summary>
    public void SetTrigger(string triggerName)
    {
        if (ValidateAnimator())
        {
            animator.SetTrigger(triggerName);
            LogDebug($"触发 Trigger: {triggerName}");
        }
    }

    /// <summary>
    /// 重置动画触发器
    /// </summary>
    public void ResetTrigger(string triggerName)
    {
        if (ValidateAnimator())
        {
            animator.ResetTrigger(triggerName);
            LogDebug($"重置 Trigger: {triggerName}");
        }
    }

    #endregion

    #region 动画速度控制

    /// <summary>
    /// 设置动画播放速度
    /// </summary>
    /// <param name="speed">速度倍数（1.0 为正常速度）</param>
    public void SetAnimationSpeed(float speed)
    {
        if (ValidateAnimator())
        {
            animator.speed = speed;
            LogDebug($"设置动画速度: {speed}x");
        }
    }

    /// <summary>
    /// 暂停动画
    /// </summary>
    public void PauseAnimation()
    {
        SetAnimationSpeed(0f);
    }

    /// <summary>
    /// 恢复动画（恢复正常速度）
    /// </summary>
    public void ResumeAnimation()
    {
        SetAnimationSpeed(1f);
    }

    #endregion

    #region 动画层级控制

    /// <summary>
    /// 设置动画层级权重
    /// </summary>
    /// <param name="layerIndex">层级索引</param>
    /// <param name="weight">权重（0-1）</param>
    public void SetLayerWeight(int layerIndex, float weight)
    {
        if (ValidateAnimator())
        {
            animator.SetLayerWeight(layerIndex, weight);
            LogDebug($"设置层级 {layerIndex} 权重: {weight}");
        }
    }

    /// <summary>
    /// 获取动画层级权重
    /// </summary>
    public float GetLayerWeight(int layerIndex)
    {
        if (ValidateAnimator())
        {
            return animator.GetLayerWeight(layerIndex);
        }
        return 0f;
    }

    /// <summary>
    /// 切换当前动画层
    /// </summary>
    public void SetCurrentLayer(int layerIndex)
    {
        animationLayer = layerIndex;
        LogDebug($"切换到动画层: {layerIndex}");
    }

    #endregion

    #region 动画状态查询

    /// <summary>
    /// 获取当前动画状态名称
    /// </summary>
    public string GetCurrentAnimationState()
    {
        return currentAnimationState;
    }

    /// <summary>
    /// 获取上一个动画状态名称
    /// </summary>
    public string GetPreviousAnimationState()
    {
        return previousAnimationState;
    }

    /// <summary>
    /// 检查是否正在播放指定动画
    /// </summary>
    public bool IsPlayingAnimation(string animationName)
    {
        if (!ValidateAnimator()) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(animationLayer);
        return stateInfo.IsName(animationName);
    }

    /// <summary>
    /// 获取当前动画的播放进度（0-1）
    /// </summary>
    public float GetAnimationProgress()
    {
        if (!ValidateAnimator()) return 0f;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(animationLayer);
        return stateInfo.normalizedTime % 1f;
    }

    /// <summary>
    /// 检查当前动画是否播放完成
    /// </summary>
    public bool IsAnimationFinished()
    {
        if (!ValidateAnimator()) return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(animationLayer);
        return stateInfo.normalizedTime >= 1f && !animator.IsInTransition(animationLayer);
    }

    /// <summary>
    /// 检查是否正在过渡动画
    /// </summary>
    public bool IsInTransition()
    {
        if (!ValidateAnimator()) return false;
        return animator.IsInTransition(animationLayer);
    }

    #endregion

    #region 工具方法

    /// <summary>
    /// 验证 Animator 组件是否有效
    /// </summary>
    private bool ValidateAnimator()
    {
        if (animator == null)
        {
            LoggerManager.Error("Animator 组件为空！", "Animation");
            return false;
        }
        return true;
    }

    /// <summary>
    /// 获取动画状态的 Hash 值（用于性能优化）
    /// </summary>
    private int GetAnimationHash(string animationName)
    {
        if (!animationHashCache.ContainsKey(animationName))
        {
            animationHashCache[animationName] = Animator.StringToHash(animationName);
        }
        return animationHashCache[animationName];
    }

    /// <summary>
    /// 从 AnimatorStateInfo 获取状态名称
    /// </summary>
    private string GetStateName(AnimatorStateInfo stateInfo)
    {
        // 注意：这只是一个简化的实现
        // 实际项目中可能需要更复杂的逻辑来获取准确的状态名
        return stateInfo.fullPathHash.ToString();
    }

    /// <summary>
    /// 调试日志
    /// </summary>
    private void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            LoggerManager.Debug(message, "Animation");
        }
    }

    #endregion

    #region 公共 API - 便捷方法

    /// <summary>
    /// 淡入播放动画
    /// </summary>
    public void FadeToAnimation(string animationName, float duration = 0.3f)
    {
        PlayAnimation(animationName, duration);
    }

    /// <summary>
    /// 切换动画（根据设置使用淡入或直接播放）
    /// </summary>
    public void SwitchAnimation(string animationName)
    {
        PlayAnimation(animationName);
    }

    /// <summary>
    /// 停止所有动画（设置速度为0）
    /// </summary>
    public void StopAllAnimations()
    {
        PauseAnimation();
    }

    /// <summary>
    /// 重置动画系统
    /// </summary>
    public void ResetAnimator()
    {
        if (ValidateAnimator())
        {
            animator.Rebind();
            animator.Update(0f);
            LogDebug("动画系统已重置");
        }
    }

    #endregion
}
