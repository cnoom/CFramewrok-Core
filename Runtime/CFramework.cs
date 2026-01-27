using System.Threading;
using CFramework.Core.BroadcastSystem;
using CFramework.Core.CommandSystem;
using CFramework.Core.Execution;
using CFramework.Core.Log;
using CFramework.Core.ModuleSystem;
using CFramework.Core.QuerySystem;
using Cysharp.Threading.Tasks;

namespace CFramework.Core
{
    public class CFramework
    {
        public record TagConfig(string BroadcastTag, string ModuleManagerTag, string CommandTag, string QueryTag)
        {
            public string BroadcastTag { get; } = BroadcastTag;
            public string ModuleManagerTag { get; } = ModuleManagerTag;
            public string CommandTag { get; } = CommandTag;
            public string QueryTag { get; } = QueryTag;
        }

        public readonly ModuleManager ModuleManager;
        public readonly BroadcastManager BroadcastManager;
        public readonly CommandManager CommandManager;
        public readonly QueryManager QueryManager;
        public readonly LogManager LogManager;
        public CancellationToken CancellationToken { get; private set; }


        public record LoggerBootstrapOptions(
            bool GlobalEnabled = true,
            bool DefaultEnabled = true,
            bool BroadcastEnabled = true,
            bool ModuleEnabled = true,
            bool CommandEnabled = true,
            bool QueryEnabled = true,
            ICFLogger.Level GlobalLevel = ICFLogger.Level.Debug
        )
        {
            public bool GlobalEnabled { get; } = GlobalEnabled;
            public bool DefaultEnabled { get; } = DefaultEnabled;
            public bool BroadcastEnabled { get; } = BroadcastEnabled;
            public bool ModuleEnabled { get; } = ModuleEnabled;
            public bool CommandEnabled { get; } = CommandEnabled;
            public bool QueryEnabled { get; } = QueryEnabled;
            public ICFLogger.Level GlobalLevel { get; } = GlobalLevel;
        }

        internal CFramework(CancellationToken cancellationToken, TagConfig tagConfig,
            ModuleDiscoverOptions discoverOptions = null,
            CFExecutionOptions executionOptions = null,
            LoggerBootstrapOptions loggerOptions = null)
        {
            CancellationToken = cancellationToken;
            LogManager = new LogManager();
            LogManager.SetLevelAll(loggerOptions?.GlobalLevel ?? ICFLogger.Level.Debug);

            // 先应用默认 Logger 的启用/禁用，以控制构造期输出
            var effectiveLoggerOptions = loggerOptions ?? new LoggerBootstrapOptions();
            LogManager.CFLogger.SetEnabled(
                effectiveLoggerOptions.DefaultEnabled && effectiveLoggerOptions.GlobalEnabled);

            var exec = executionOptions ?? new CFExecutionOptions();
            BroadcastManager =
                new BroadcastManager(
                    LogManager.Create(tagConfig.BroadcastTag,
                        effectiveLoggerOptions.GlobalEnabled && effectiveLoggerOptions.BroadcastEnabled), exec);
            CommandManager =
                new CommandManager(
                        LogManager.Create(tagConfig.CommandTag,
                            effectiveLoggerOptions.GlobalEnabled && effectiveLoggerOptions.CommandEnabled), exec)
                    { EnsureMainThread = exec.EnsureMainThread };
            QueryManager =
                new QueryManager(
                        LogManager.Create(tagConfig.QueryTag,
                            effectiveLoggerOptions.GlobalEnabled && effectiveLoggerOptions.QueryEnabled), exec)
                    { EnsureMainThread = exec.EnsureMainThread };
            ModuleManager =
                new ModuleManager(
                    LogManager.Create(tagConfig.ModuleManagerTag,
                        effectiveLoggerOptions.GlobalEnabled && effectiveLoggerOptions.ModuleEnabled),
                    discoverOptions ?? new ModuleDiscoverOptions());

            // 构造完成后再输出初始化日志，已受启用/禁用控制
            LogManager.CFLogger.LogDebug("初始化CF...");
        }

        public void Update() => ModuleManager.Update();
        public void LateUpdate() => ModuleManager.LateUpdate();
        public void PhysicsUpdate() => ModuleManager.PhysicsUpdate();
        public void OnApplicationPause(bool isPaused) => ModuleManager.OnApplicationPause(isPaused);
        public void OnApplicationFocus(bool hasFocus) => ModuleManager.OnApplicationFocus(hasFocus);
        public void OnApplicationQuit() => ModuleManager.OnApplicationQuit();

        public async UniTask DisposeAsync()
        {
            await ModuleManager.DisposeAsync();
            await QueryManager.DisposeAsync();
            await CommandManager.DisposeAsync();
            await BroadcastManager.DisposeAsync();

            // 清理所有被跟踪的 CTS，防止内存泄漏
            CF.ClearTrackedCts();

            LogManager.CFLogger.LogDebug("CF框架异步关闭完成!");
            LogManager.Clear();
        }
    }
}