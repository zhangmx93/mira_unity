using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerRotation : MonoBehaviour
{
    [Header("惯性旋转设置")]
    public float rotationSpeed = 0.5f;
    public float deceleration = 0.9f;
    public float minVelocity = 0.1f;
    
    [Header("旋转限制")]
    public bool yAxisOnly = false;

    private Vector2 lastTouchPosition;
    private Vector2 currentVelocity;
    private bool isDragging = false;

    void Update()
    {
        #if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouseInput();
        #else
        HandleTouchInput();
        #endif
        ApplyInertia();
    }

    // 桌面设备的鼠标操作
    void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            lastTouchPosition = Input.mousePosition;
            currentVelocity = Vector2.zero;
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector2 currentMousePosition = Input.mousePosition;
            Vector2 delta = currentMousePosition - lastTouchPosition;

            currentVelocity = delta * rotationSpeed;

            RotateObject(delta);

            lastTouchPosition = currentMousePosition;
        }
    }

    // Android/iOS 触摸操作
    void HandleTouchInput()
    {
        if (Input.touchCount == 0)
        {
            isDragging = false;
            return;
        }

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case TouchPhase.Began:
                isDragging = true;
                lastTouchPosition = touch.position;
                currentVelocity = Vector2.zero;
                break;
            case TouchPhase.Moved:
                if (isDragging)
                {
                    Vector2 currentTouchPosition = touch.position;
                    Vector2 delta = currentTouchPosition - lastTouchPosition;

                    currentVelocity = delta * rotationSpeed;
                    RotateObject(delta);

                    lastTouchPosition = currentTouchPosition;
                }
                break;
            case TouchPhase.Ended:
            case TouchPhase.Canceled:
                isDragging = false;
                break;
        }
    }

    void ApplyInertia()
    {
        // 应用惯性
        if (!isDragging && currentVelocity.magnitude > minVelocity)
        {
            RotateObject(currentVelocity);
            currentVelocity *= deceleration;

            if (currentVelocity.magnitude <= minVelocity)
            {
                currentVelocity = Vector2.zero;
            }
        }
    }

    void RotateObject(Vector2 delta)
    {
        float rotationX = delta.y * rotationSpeed;
        float rotationY = -delta.x * rotationSpeed;

        if (yAxisOnly)
        {
            transform.Rotate(0, rotationY, 0, Space.World);
        }
        else
        {
            transform.Rotate(rotationX, rotationY, 0, Space.World);

            Vector3 currentEuler = transform.eulerAngles;
            if (currentEuler.x > 180f) currentEuler.x -= 360f;
            currentEuler.x = Mathf.Clamp(currentEuler.x, -80f, 80f);
            currentEuler.z = 0f;
            transform.eulerAngles = currentEuler;
        }
    }

    // 停止惯性（可选）
    public void StopInertia()
    {
        currentVelocity = Vector2.zero;
    }
    
    // 公共方法：设置是否只允许Y轴旋转
    public void SetYAxisOnly(bool yOnly)
    {
        yAxisOnly = yOnly;

        if (yAxisOnly)
        {
            Vector3 currentEuler = transform.eulerAngles;
            currentEuler.x = 0f;
            currentEuler.z = 0f;
            transform.eulerAngles = currentEuler;
        }
    }
    
    // 公共方法：切换旋转模式
    public void ToggleRotationMode()
    {
        SetYAxisOnly(!yAxisOnly);
    }
}