using System;
using System.Collections.Generic;
using CFramework.Core.Execution;
using CFramework.Core.Log;
using CFramework.Core.ModuleSystem;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CFramework.Core
{
    /// <summary>
    /// 框架的unity绑定（仅使用 CFrameworkConfig 作为配置来源）
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CFrameworkUnityEntry : MonoBehaviour
    {
        [Header("配置对象"), Space]
        public CFrameworkConfig config;

        private CFramework _cFramework;

        protected virtual void Awake()
        {
            if(config == null)
            {
                Debug.LogError("CFrameworkUnityEntry: 请在 Inspector 中指定 CFrameworkConfig 配置对象。");
                enabled = false;
                return;
            }

            // 防重复初始化：如已存在实例，则复用并销毁当前Entry，避免覆盖与后续OnDestroy错误清理。
            if(CF.CFramework() != null)
            {
                Debug.LogWarning("CFrameworkUnityEntry: 检测到已有 CFramework 实例，当前 Entry 将自动销毁以避免重复初始化。");
                Destroy(gameObject);
                return;
            }

            var options = new ModuleDiscoverOptions(
                assemblyWhitelist: config.autoDiscoverConfig.assemblyWhitelist
            );

            // 设置已启用模块列表获取回调
            options.GetEnabledModules = () => config.autoDiscoverConfig.GetEnabledModules();
            // 保持向后兼容性
            options.GetModuleEnabled = (moduleType) => config.autoDiscoverConfig.IsModuleEnabled(moduleType);

            var execOptions = new CFExecutionOptions
            {
                EnsureMainThread = config.executionConfig.ensureMainThread,
                BroadcastConcurrency =
                    config.executionConfig.broadcastConcurrent ? ConcurrencyMode.Concurrent : ConcurrencyMode.Sequential,
                OverallTimeoutSeconds = config.executionConfig.overallTimeoutSeconds,
                PerHandlerTimeoutSeconds = config.executionConfig.perHandlerTimeoutSeconds,
                ErrorPolicy = config.executionConfig.errorPolicy,
                CancellationPolicy = config.executionConfig.cancellationPolicy
            };

            LoggerColorManager.Initialize(config);

            var created = new CFramework(gameObject.GetCancellationTokenOnDestroy(),
                new CFramework.TagConfig(config.tagConfig.broadcastTag, config.tagConfig.moduleManagerTag,
                    config.tagConfig.commandTag, config.tagConfig.queryTag),
                options,
                execOptions,
                new CFramework.LoggerBootstrapOptions(
                    GlobalEnabled: config.loggerConfig.loggerGlobalEnabled,
                    DefaultEnabled: config.loggerConfig.enableDefaultLogger,
                    BroadcastEnabled: config.loggerConfig.enableBroadcastLogger,
                    ModuleEnabled: config.loggerConfig.enableModuleLogger,
                    CommandEnabled: config.loggerConfig.enableCommandLogger,
                    QueryEnabled: config.loggerConfig.enableQueryLogger,
                    GlobalLevel: config.loggerConfig.defaultLogLevel)
            );

            if(!CF.Initialize(created))
            {
                // 若已存在不同实例，放弃当前创建的实例并销毁 Entry
                Debug.LogWarning("CFrameworkUnityEntry: 已存在CF实例，忽略重复初始化并销毁当前Entry。");
                Destroy(gameObject);
                return;
            }

            if(config.loggerConfig.additionalLoggerToggles != null)
            {
                foreach (var t in config.loggerConfig.additionalLoggerToggles)
                {
                    if(t == null) continue;
                    if(string.IsNullOrEmpty(t.tag)) continue;
                    CF.SetLoggerEnabled(t.enabled && config.loggerConfig.loggerGlobalEnabled, t.tag);
                }
            }

            _cFramework = CF.CFramework();
            DontDestroyOnLoad(gameObject);
        }

        protected virtual void Start()
        {
            if(_cFramework == null) return;
            _cFramework.ModuleManager.AutoDiscoverModules(CF.CancellationToken).Forget(e => { Debug.LogError($"模块自动发现失败：{e}"); });
        }

        private void Update()
        {
            _cFramework?.Update();
        }

        private void LateUpdate()
        {
            _cFramework?.LateUpdate();
        }

        private void FixedUpdate()
        {
            _cFramework?.PhysicsUpdate();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _cFramework?.OnApplicationPause(pauseStatus);
        }

        private void OnApplicationFocus(bool focus)
        {
            _cFramework?.OnApplicationFocus(focus);
        }

        private void OnApplicationQuit()
        {
            _cFramework?.OnApplicationQuit();
        }

        private void OnDestroy()
        {
            if(_cFramework == null || CF.CFramework() != _cFramework) return;
            _cFramework.DisposeAsync().Forget(e =>
            {
                Debug.LogError($"CF 框架卸载异常：{e}");
                CF.TryClearInstance(_cFramework);
            });
            LoggerColorManager.Clear();
        }
    }
}