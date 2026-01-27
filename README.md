# CFramework-Core

一个轻量级、高效的Unity游戏开发框架，专注于事件驱动系统和模块化架构设计，帮助开发者构建可维护、可扩展的游戏项目。

![Unity](https://img.shields.io/badge/Unity-2021.3%2B-blue.svg)
![Version](https://img.shields.io/badge/Version-1.0.0-green.svg)
![License](https://img.shields.io/badge/License-MIT-yellow.svg)

## ✨ 特性

- **模块化架构** - 基于接口的模块系统，支持自动依赖解析和生命周期管理
- **事件驱动系统** - 三大核心系统（广播、命令、查询）实现解耦通信
- **异步优先** - 深度集成UniTask，提供高性能的异步操作支持
- **类型安全** - 基于泛型的事件系统，编译时类型检查
- **生命周期管理** - 完整的模块生命周期控制（初始化、更新、销毁）
- **日志系统** - 多标签、分级日志管理，支持运行时配置
- **自动注册** - 基于特性的自动发现和注册机制
- **线程安全** - 核心系统支持多线程环境下的安全访问
- **编辑器模块** - 一套用于编辑器各种生命周期的模块化框架
- **上手友好** - 配置自动生成，直接上手使用即可 

## 📋 系统要求

- Unity 2021.3 或更高版本
- [UniTask 2.5.1](https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask)

## 📦 安装

### 通过 Package Manager 安装


1. 在 Unity 编辑器中打开 `Window` > `Package Manager`
2. 点击左上角的 `+` 号，选择 `Add package from git URL`
3. 输入UniTask包仓库地址：`https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask`
4. 点击 `Add` 等待安装完成
5. 输入仓库地址：`https://github.com/cnoom/CFramewrok-Core.git`
6. 点击 `Add` 等待安装完成

### 通过 Git URL 安装
1.在Manifest.json文件中添加如下代码：
```
"dependencies": {
    ...
    "com.cnoom.cframework.core": "https://github.com/cnoom/CFramewrok-Core.git",
    "com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask",
    ...
}
```

### 手动安装

将 `com.cnoom.cframework.core` 文件夹复制到你的 Unity 项目的 `Packages` 目录下。

## 🚀 快速开始

### 1. 设置框架入口

1.在指定的场景新建一个GameObject
2.将 `CFrameworkUnityEntry` 组件添加到该GameObject上
3.将 `Assets/CFramework/Config/CFrameworkConfig.asset` 拖拽到`CFrameworkUnityEntry`所需位置即可

### 2. 定义广播，命令，查询

```csharp
using CFramework.Core.BroadcastSystem;

// 定义计数器变化事件
public struct CounterChangedBroadcast : IBroadcastData
{
    public int NewValue { get; set; }
}
// 定义计数器修改命令
public struct ChangeCounterCommand : ICommandData
{
    public int Amount { get; set; }
}
// 定义计数器查询
public struct GetCounterQuery : IQueryData
{
}
```

### 3. 创建处理器

```csharp
[AutoModule("CounterModule", "一个计数器测试模块")]
public class CounterModule
{
    private int counter = 0;

    [BroadcastHandler]
    public async UniTask OnCounterChanged(CounterChangedBroadcast broadcast, CancellationToken ct)
    {
        Debug.Log($"计数器值变为: {broadcast.NewValue}");
    }

    [CommandHandler]
    public async UniTask HandleChangeCounter(ChangeCounterCommand command, CancellationToken ct)
    {
        counter += command.Amount;
        // 通知计数器变化
        await CF.Broadcast(new CounterChangedBroadcast { NewValue = counter });
    }

    [QueryHandler]
    public async UniTask<int> HandleGetCounter(GetCounterQuery query, CancellationToken ct)
    {
        return counter;
    }
}
```

### 4. 执行广播，命令，查询

```csharp
// 执行命令，处理器会在处理命令后自动发布变化广播然后会执行OnCounterChanged
await CF.Execute(new ChangeCounterCommand { Amount = 1 });

// 执行查询
var count = await CF.Query<GetCounterQuery, int>(new GetCounterQuery());
Debug.Log($"当前计数: {count}");

```

## 🏗️ 核心概念

### 模块系统（Module System）

模块是CFramework的基本组织单元，每个模块实现 `IModule` 接口：

- **生命周期管理**：模块支持完整的生命周期（初始化、更新、销毁）
- **依赖注入**：使用 `[ModuleDependsOn]` 特性声明模块依赖
- **自动发现**：框架自动扫描并注册带有 `[AutoModule]` 特性的模块
- **自动注册取消注册事件**：模块管理会自动注册和取消注册模块的相应事件监听

```csharp
[ModuleDependsOn(typeof(AudioSystemModule))]
[AutoModule("PlayerModule")]
public class PlayerModule : IModule
{
    // 实现
}
```

### 广播系统（Broadcast System）

用于一对多的异步事件传播：

- 支持优先级排序
- 异步处理，不阻塞主线程
- 自动取消令牌管理

### 命令系统（Command System）

用于执行操作和动作：

- 一对多的命令处理
- 支持异步执行
- 确保线程安全

### 查询系统（Query System）

用于请求数据并返回结果：

- 自动结果缓存
- 缓存失效机制
- 支持同步和异步查询

### 日志系统（Log System）

灵活的日志管理：

```csharp
// 使用默认日志
CF.LogInfo("Game started");
CF.LogWarning("Low memory");
CF.LogError("Critical error");

// 创建自定义日志
var logger = CF.CreateLogger("Combat");
logger.LogInfo("Player attacked");

// 配置日志级别
CF.SetLogLevel(ICFLogger.Level.Debug);
CF.SetLoggerEnabled(true,"Combat");
```

## 📚 API 参考

### CF 静态类

框架的统一入口点，提供对所有系统的访问：

| 方法 | 描述 |
|------|------|
| `CF.RegisterModule<T>()` | 注册单个模块 |
| `CF.RegisterModules(registry)` | 批量注册模块 |
| `CF.Broadcast<T>(data)` | 广播事件 |
| `CF.Execute<T>(command)` | 执行命令 |
| `CF.Query<T, TResult>(query)` | 执行查询 |
| `CF.LogInfo(message)` | 记录信息日志 |

详细API文档请参考 [CF.cs](Runtime/CF.cs)

## 🎯 最佳实践

1. **模块职责单一**：每个模块只负责一个功能领域
2. **使用特性自动注册**：利用 `[AutoModule]`、`[BroadcastHandler]` 等特性简化代码
3. **异步优先**：所有耗时操作使用 `UniTask` 实现
4. **日志分级**：使用不同日志级别帮助调试和问题定位

## 🔧 配置

### 框架配置

- 通过 `CFrameworkConfig` 文件来配置框架选项
- 通过 `CFrameworkEditorConfig` 文件来配置框架编辑器选项

## 🤝 贡献

欢迎贡献代码！请遵循以下步骤：

1. Fork 本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 提交 Pull Request

## 📝 许可证

本项目采用 MIT 许可证 - 详见 [LICENSE](LICENSE) 文件

## 📧 联系方式

- 作者：Cnoom
- 邮箱：cnoom@qq.com
- GitHub：[@cnoom](https://github.com/cnoom)

## 🙏 致谢

- [UniTask](https://github.com/Cysharp/UniTask) - 高性能异步/等待库
- Unity 社区的所有贡献者

## 📄 更新日志

### [1.0.0] - 2024-01-27

#### 新增
- 初始版本发布
- 完整的模块系统实现
- 广播、命令、查询三大核心系统
- 日志管理系统
- 自动注册机制
- 编辑器工具模块化支持

---

**CFramework-Core** - 让Unity游戏开发更简单、更高效！
