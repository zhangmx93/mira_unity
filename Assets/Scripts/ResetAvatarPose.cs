using UnityEngine;

public class ResetAvatarPose : MonoBehaviour
{
    private Animator animator;

    void Start()
    {
        animator = GetComponent<Animator>();
        if (animator != null)
        {
            // 强制Avatar重置为初始姿势
            animator.Update(0f);
            // 或者，更彻底的方法：重新启用Animator
            animator.enabled = false;
            animator.enabled = true;
        }
    }
}