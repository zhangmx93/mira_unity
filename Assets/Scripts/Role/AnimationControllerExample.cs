using UnityEngine;

/// <summary>
/// AnimationManager 使用示例
/// 演示如何在其他脚本中使用动画管理器
/// </summary>
public class AnimationControllerExample : MonoBehaviour
{
    [Header("动画管理器引用")]
    public AnimationManager animationManager;

    [Header("动画状态名称")]
    [Tooltip("这些名称需要与 Animator Controller 中的状态名称一致")]
    public string idleAnimation = "Azakiel|walk_normal_1_002";
    public string walkAnimation = "Azakiel|sc_talk_1";
    public string runAnimation = "Azakiel|sc_greet_2";
    public string jumpAnimation = "Azakiel|sc_greet_2";
    public string attackAnimation = "Azakiel|sc_greet_2";

    void Start()
    {
        // 如果未指定，自动查找 AnimationManager
        if (animationManager == null)
        {
            animationManager = GetComponent<AnimationManager>();
            if (animationManager == null)
            {
                Debug.LogError("AnimationControllerExample: 未找到 AnimationManager 组件！请确保该脚本附加到有 AnimationManager 的对象上。");
                return;
            }
            else
            {
                Debug.Log("AnimationControllerExample: 自动找到 AnimationManager 组件");
            }
        }

        Debug.Log("AnimationControllerExample 已启动，可以使用键盘控制：\n" +
                  "按 1 - Idle\n" +
                  "按 2 - Walk\n" +
                  "按 3 - Run\n" +
                  "按 空格 - Jump\n" +
                  "按 鼠标左键 - Attack\n" +
                  "按 P - 暂停/恢复");

        // 初始播放 Idle 动画
        if (animationManager != null)
        {
            animationManager.PlayAnimation(idleAnimation);
        }
    }

    void Update()
    {
        // 示例：使用键盘控制动画切换
        HandleInput();
    }

    /// <summary>
    /// 处理键盘输入来切换动画
    /// </summary>
    void HandleInput()
    {
        if (animationManager == null)
        {
            Debug.LogWarning("AnimationControllerExample: animationManager 为空，无法处理输入");
            return;
        }

        // 按 1 键播放 Idle
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            Debug.Log("按下了键盘 1 - 播放 Idle 动画");
            PlayIdle();
        }

        // 按 2 键播放 Walk
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            Debug.Log("按下了键盘 2 - 播放 Walk 动画");
            PlayWalk();
        }

        // 按 3 键播放 Run
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            Debug.Log("按下了键盘 3 - 播放 Run 动画");
            PlayRun();
        }

        // 按空格键播放 Jump
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("按下了空格键 - 播放 Jump 动画");
            PlayJump();
        }

        // 按鼠标左键播放 Attack
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("按下了鼠标左键 - 播放 Attack 动画");
            PlayAttack();
        }

        // 按 P 键暂停/恢复动画
        if (Input.GetKeyDown(KeyCode.P))
        {
            Debug.Log("按下了 P 键 - 切换暂停状态");
            TogglePause();
        }
    }

    #region 动画控制方法

    /// <summary>
    /// 播放待机动画
    /// </summary>
    public void PlayIdle()
    {
        animationManager.PlayAnimation(idleAnimation);
    }

    /// <summary>
    /// 播放行走动画
    /// </summary>
    public void PlayWalk()
    {
        animationManager.PlayAnimation(walkAnimation);
    }

    /// <summary>
    /// 播放奔跑动画
    /// </summary>
    public void PlayRun()
    {
        animationManager.PlayAnimation(runAnimation);
    }

    /// <summary>
    /// 播放跳跃动画
    /// </summary>
    public void PlayJump()
    {
        // 使用较短的过渡时间，让跳跃更迅速
        animationManager.PlayAnimation(jumpAnimation, 0.1f);
    }

    /// <summary>
    /// 播放攻击动画
    /// </summary>
    public void PlayAttack()
    {
        // 立即播放，无过渡
        animationManager.PlayAnimationImmediate(attackAnimation);
    }

    /// <summary>
    /// 根据移动速度切换动画
    /// </summary>
    public void UpdateMovementAnimation(float speed)
    {
        if (speed > 5f)
        {
            // 奔跑
            animationManager.PlayAnimation(runAnimation);
        }
        else if (speed > 0.1f)
        {
            // 行走
            animationManager.PlayAnimation(walkAnimation);
        }
        else
        {
            // 待机
            animationManager.PlayAnimation(idleAnimation);
        }
    }

    /// <summary>
    /// 切换暂停状态
    /// </summary>
    private bool isPaused = false;
    public void TogglePause()
    {
        isPaused = !isPaused;
        if (isPaused)
        {
            animationManager.PauseAnimation();
            Debug.Log("动画已暂停");
        }
        else
        {
            animationManager.ResumeAnimation();
            Debug.Log("动画已恢复");
        }
    }

    #endregion

    #region 高级示例

    /// <summary>
    /// 示例：使用动画参数控制（Blend Tree）
    /// </summary>
    public void SetMovementBlend(float horizontal, float vertical)
    {
        // 如果使用 Blend Tree，可以通过参数控制
        animationManager.SetFloat("Horizontal", horizontal);
        animationManager.SetFloat("Vertical", vertical);
    }

    /// <summary>
    /// 示例：使用触发器切换动画
    /// </summary>
    public void TriggerSkill()
    {
        animationManager.SetTrigger("Skill");
    }

    /// <summary>
    /// 示例：检查动画是否播放完成
    /// </summary>
    public void CheckAnimationFinished()
    {
        if (animationManager.IsAnimationFinished())
        {
            Debug.Log($"动画 '{animationManager.GetCurrentAnimationState()}' 播放完成");
            // 返回待机状态
            PlayIdle();
        }
    }

    /// <summary>
    /// 示例：调整动画速度
    /// </summary>
    public void SetAnimationSpeedByHealth(float healthPercent)
    {
        // 根据生命值调整动画速度
        float speed = Mathf.Lerp(0.5f, 1.5f, healthPercent);
        animationManager.SetAnimationSpeed(speed);
    }

    #endregion

    #region UI 按钮调用方法

    // 以下方法可以绑定到 UI 按钮的 OnClick 事件

    public void OnIdleButtonClick()
    {
        PlayIdle();
    }

    public void OnWalkButtonClick()
    {
        PlayWalk();
    }

    public void OnRunButtonClick()
    {
        PlayRun();
    }

    public void OnJumpButtonClick()
    {
        PlayJump();
    }

    public void OnAttackButtonClick()
    {
        PlayAttack();
    }

    #endregion
}
