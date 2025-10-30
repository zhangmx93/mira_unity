# 权限管理器使用指南

## 📋 概述

`PermissionManager` 是一个统一的权限管理系统，用于在Android/iOS上依次请求所有需要的权限（摄像头、麦克风等），避免权限请求冲突。

## ✨ 特点

- ✅ 统一管理所有权限请求
- ✅ 依次请求，避免对话框冲突
- ✅ 自动与 CameraCapture 和 MicrophoneCapture 集成
- ✅ 单例模式，全局访问
- ✅ 支持运行时查询权限状态

## 🚀 使用方法

### 方式1：自动管理（推荐）

**步骤：**

1. **在场景中创建权限管理器对象：**
   - Hierarchy 右键 → Create Empty
   - 命名为 "PermissionManager"
   - 添加 `PermissionManager` 组件

2. **配置参数：**
   ```
   Request On Start: ✓ (自动请求)
   Request Camera: ✓ (请求摄像头)
   Request Microphone: ✓ (请求麦克风)
   Enable Debug Log: ✓ (启用日志)
   ```

3. **添加其他组件：**
   - 添加 CameraCapture 和 MicrophoneCapture 到场景中
   - 它们会自动等待 PermissionManager 完成权限请求

4. **运行游戏：**
   - Android上会依次弹出权限对话框
   - 先请求麦克风 → 再请求摄像头
   - 所有权限授予后，自动启动设备

### 方式2：手动控制

```csharp
// 手动请求所有权限
PermissionManager.Instance.RequestPermissions();

// 检查权限状态
bool cameraOK = PermissionManager.Instance.IsCameraGranted();
bool micOK = PermissionManager.Instance.IsMicrophoneGranted();
bool allOK = PermissionManager.Instance.AreAllPermissionsGranted();
```

## 📱 Android权限请求流程

### 时间轴（有PermissionManager）：

```
0.0s  - 场景启动
0.1s  - PermissionManager 开始请求权限
0.2s  - 弹出麦克风权限对话框
1.0s  - 用户点击"允许"麦克风
1.5s  - 弹出摄像头权限对话框
2.5s  - 用户点击"允许"摄像头
3.0s  - 所有权限请求完成
3.1s  - CameraCapture 和 MicrophoneCapture 自动启动
```

### Console日志输出：

```
PermissionManager: 开始请求权限...
PermissionManager: 请求麦克风权限...
[用户点击允许]
PermissionManager: ✅ 麦克风权限已授予
PermissionManager: 请求摄像头权限...
[用户点击允许]
PermissionManager: ✅ 摄像头权限已授予
PermissionManager: ✅ 所有权限已授予
```

## 🔧 API 参考

### 公开方法

```csharp
// 手动请求所有权限
void RequestPermissions()

// 查询摄像头权限状态
bool IsCameraGranted()

// 查询麦克风权限状态
bool IsMicrophoneGranted()

// 查询所有权限是否都已授予
bool AreAllPermissionsGranted()
```

### 访问单例

```csharp
PermissionManager.Instance
```

## 📝 与其他组件的集成

### CameraCapture 和 MicrophoneCapture

这两个组件会自动检测场景中是否有 PermissionManager：

- **有 PermissionManager**：等待它完成权限请求后再启动
- **没有 PermissionManager**：自己请求权限（可能会冲突）

**推荐做法：**
- 在场景中添加 PermissionManager
- 让它统一管理所有权限

## ⚙️ Inspector 参数

| 参数 | 说明 | 默认值 |
|------|------|--------|
| Request On Start | 是否在启动时自动请求所有权限 | ✓ |
| Request Camera | 是否请求摄像头权限 | ✓ |
| Request Microphone | 是否请求麦克风权限 | ✓ |
| Enable Debug Log | 是否启用调试日志 | ✓ |

## 🎯 使用场景

### 场景1：同时需要摄像头和麦克风

```
Hierarchy:
├─ PermissionManager (自动请求所有权限)
├─ CameraCapture (等待权限后启动)
└─ MicrophoneCapture (等待权限后启动)
```

### 场景2：只需要摄像头

```csharp
// 在 Inspector 中设置
Request Camera: ✓
Request Microphone: ✗
```

### 场景3：只需要麦克风

```csharp
// 在 Inspector 中设置
Request Camera: ✗
Request Microphone: ✓
```

## 🐛 常见问题

### Q: 为什么只弹出一个权限对话框？

A: 确保场景中有 PermissionManager 组件，并且 Request On Start 已勾选。

### Q: 权限被拒绝后怎么办？

A:
- Android：卸载应用重新安装，或在设置中手动授予权限
- iOS：在设置 → 应用权限中手动授予

### Q: 如何自定义权限请求顺序？

A: 修改 PermissionManager.cs 中的 `RequestAllPermissions()` 方法，调整顺序。

### Q: 可以在运行时再请求权限吗？

A: 可以，调用 `PermissionManager.Instance.RequestPermissions()`

## 📦 完整示例

```csharp
using UnityEngine;

public class MyApp : MonoBehaviour
{
    void Start()
    {
        // 检查是否所有权限都已授予
        if (PermissionManager.Instance.AreAllPermissionsGranted())
        {
            // 开始使用摄像头和麦克风
            StartApp();
        }
        else
        {
            // 等待权限授予
            StartCoroutine(WaitForPermissions());
        }
    }

    System.Collections.IEnumerator WaitForPermissions()
    {
        while (!PermissionManager.Instance.AreAllPermissionsGranted())
        {
            yield return new WaitForSeconds(0.5f);
        }

        StartApp();
    }

    void StartApp()
    {
        Debug.Log("所有权限已授予，启动应用...");
        // 你的应用逻辑
    }
}
```

## 🔄 执行顺序

PermissionManager 的 `executionOrder` 设置为 `-100`，确保它在其他组件之前执行。

## ✅ 最佳实践

1. ✅ 在每个场景都添加 PermissionManager
2. ✅ 使用 DontDestroyOnLoad 保持跨场景
3. ✅ 在启动场景就请求所有权限
4. ✅ 启用 Debug Log 方便调试
5. ✅ 在 Player Settings 中勾选对应的权限
