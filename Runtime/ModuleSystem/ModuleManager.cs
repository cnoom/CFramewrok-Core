using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading;
using CFramework.Core.Attributes;
using CFramework.Core.Interfaces;
using CFramework.Core.Interfaces.LifeScope;
using CFramework.Core.Log;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CFramework.Core.ModuleSystem
{
    public partial class ModuleManager : IUpdate, ILateUpdate
    {
        private readonly object _lock = new object();
        private readonly ConcurrentDictionary<Type, IModule> _modules = new();
        private readonly CFLogger _logger;
        private readonly ModuleDiscoverOptions _options;

        // 程序集缓存，避免重复扫描
        private static Assembly[] _cachedAssemblies;
        private static DateTime _lastCacheTime;
        private static readonly object _cacheLock = new object();
        private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5); // 缓存5分钟

        internal ModuleManager(CFLogger logger, ModuleDiscoverOptions options = null)
        {
            _logger = logger;
            _options = options ?? new ModuleDiscoverOptions();
            _logger.LogDebug("模块管理器初始化!");
        }

        /// <summary>
        /// 获取需要扫描的程序集列表（带缓存）
        /// </summary>
        private Assembly[] GetAssembliesToScan()
        {
            // 如果缓存有效，直接返回
            lock (_cacheLock)
            {
                if (_cachedAssemblies != null &&
                    (DateTime.Now - _lastCacheTime) < CacheExpiration)
                {
                    return _cachedAssemblies;
                }
            }

            // 重新获取程序集列表
            var allAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var filteredAssemblies = new List<Assembly>();

            foreach (var assembly in allAssemblies)
            {
                try
                {
                    if (_options != null && _options.MatchAssembly(assembly.FullName))
                    {
                        filteredAssemblies.Add(assembly);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"程序集过滤异常 [{assembly.FullName}]: {ex.Message}");
                    // 继续处理其他程序集
                }
            }

            // 更新缓存
            lock (_cacheLock)
            {
                _cachedAssemblies = filteredAssemblies.ToArray();
                _lastCacheTime = DateTime.Now;
            }

            return _cachedAssemblies;
        }

        #region 注册/取消注册

        public UniTask RegisterModule<TModule>() where TModule : IModule, new()
        {
            // ReSharper disable SuspiciousTypeConversion.Global
            TModule module = new TModule();

            return RegisterModule(module);
        }

        public UniTask<bool> UnregisterModule<TModule>(CancellationToken cancellationToken) where TModule : IModule =>
            UnregisterModuleAsync(typeof(TModule), cancellationToken);

        public bool IsRegistered<TModule>() where TModule : IModule => _modules.ContainsKey(typeof(TModule));

        /// <summary>
        /// 批量注册模块，支持依据 ModuleDependsOnAttribute 的依赖解析进行排序注册。
        /// 仅对当前批次中的类型进行依赖排序；对已注册的依赖将视为已满足。
        /// </summary>
        public async UniTask RegisterModules(ModulesRegistry registry)
        {
            if (registry == null) return;

            // 构建仅针对当前批次类型的依赖获取器
            IEnumerable<Type> GetBatchDependencies(Type t)
            {
                // 返回特性中声明的原始依赖类型（包括接口/抽象类型），由拓扑排序阶段统一处理
                return GetModuleDependenciesFromAttributes(t);
            }

            var sortedTypes = TopologicalSortTypes(registry.ModuleTypes, GetBatchDependencies);

            // 依序注册（拓扑序保证先依赖后被依赖）
            foreach (var t in sortedTypes)
            {
                IModule module = (IModule)Activator.CreateInstance(t);
                await RegisterModule(module);
            }
        }

        /// <summary>
        /// 批量卸载模块，支持依据 ModuleDependsOnAttribute 的依赖解析进行排序卸载。
        /// </summary>
        /// <param name="registry">模块集合注册器</param>
        /// <param name="cancellationToken"></param>
        /// <returns>是否全部卸载成功</returns>
        public async UniTask<bool> UnregisterModules(ModulesRegistry registry, CancellationToken cancellationToken)
        {
            if (registry == null) return false;
            bool result = true;
            var list = TopologicalSortTypes(
                registry.ModuleTypes,
                t => GetModuleDependenciesFromAttributes(t)
            ).ToArray();
            for (int i = list.Length - 1; i >= 0; i--)
            {
                bool b = await UnregisterModuleAsync(list[i], cancellationToken);
                result &= b;
            }

            return result;
        }

        private async UniTask RegisterModule(IModule module)
        {
            var type = module.GetType();
            if (module is ICreate create) create.OnCreate();
            if (module is ICreateAsync createAsync) await createAsync.CreateAsync(CF.CancellationToken);
            if (_modules.ContainsKey(type))
            {
                _logger.LogWarning($"{type.Name} 模块已注册，忽略重复注册。");
                return;
            }

            _logger.LogInfo($"注册模块 {type.Name}。");
            CF.RegisterHandler(module);
            _modules.TryAdd(type, module);

            lock (_lock)
            {
                // ReSharper disable SuspiciousTypeConversion.Global
                if (module is IUpdate update) _updateModules.Add(update);
                if (module is ILateUpdate lateUpdate) _lateUpdates.Add(lateUpdate);
                if (module is IPhysicsUpdate physics) _physicsUpdates.Add(physics);
                if (module is IPauseHandler pause) _pauseHandlers.Add(pause);
                if (module is IFocusHandler focus) _focusHandlers.Add(focus);
                if (module is IQuitHandler quit) _quitHandlers.Add(quit);
                if (module is ICancellationHolder cancellationHolder)
                    cancellationHolder.CancellationTokenSource = new CancellationTokenSource();
            }

            // ReSharper disable SuspiciousTypeConversion.Global
            if (module is IRegister register) register.Register();
            if (module is IRegisterAsync registerAsync) await registerAsync.RegisterAsync(CF.CancellationToken);
        }

        private async UniTask<bool> UnregisterModuleAsync(Type moduleType, CancellationToken cancellationToken)
        {
            if (!_modules.TryRemove(moduleType, out IModule module)) return false;

            // 补充：与同步卸载一致，先注销模块在各管理器中的处理器，避免卸载后的回调悬挂
            CF.UnregisterHandler(module);

            lock (_lock)
            {
                if (module is IUpdate update) _updateModules.Remove(update);
                if (module is ILateUpdate lateUpdate) _lateUpdates.Remove(lateUpdate);
                if (module is IPhysicsUpdate physics) _physicsUpdates.Remove(physics);
                if (module is IPauseHandler pause) _pauseHandlers.Remove(pause);
                if (module is IFocusHandler focus) _focusHandlers.Remove(focus);
                if (module is IQuitHandler quit) _quitHandlers.Remove(quit);
                if (module is ICancellationHolder cancellationHolder)
                {
                    cancellationHolder.CancellationTokenSource.Cancel();
                    cancellationHolder.CancellationTokenSource.Dispose();
                    cancellationHolder.CancellationTokenSource = null;
                }
            }

            if (module is IUnRegister unRegister) unRegister.UnRegister();
            if (module is IUnRegisterAsync unRegisterAsync) await unRegisterAsync.UnRegisterAsync(CF.CancellationToken);
            if (module is IDisposable disposable) disposable.Dispose();

            _logger.LogInfo($"卸载模块 {module.GetType().Name}。");
            return true;
        }

        #endregion

        internal async UniTask AutoDiscoverModules(CancellationToken ct)
        {
            _logger.LogDebug("寻找自动注册模块!");

            // 优先从配置获取已启用的模块列表
            string[] enabledModuleTypeNames = null;
            if (_options?.GetEnabledModules != null)
            {
                try
                {
                    enabledModuleTypeNames = _options.GetEnabledModules();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning($"从配置获取已启用模块列表失败: {ex.Message}，将回退到扫描模式。");
                }
            }

            List<(Type type, AutoModuleAttribute auto, Type[] deps)> list = new();

            // 如果配置提供了已启用模块列表，直接从配置加载
            if (enabledModuleTypeNames != null && enabledModuleTypeNames.Length > 0)
            {
                _logger.LogInfo($"从配置加载 {enabledModuleTypeNames.Length} 个已启用模块。");

                foreach (var moduleTypeName in enabledModuleTypeNames)
                {
                    if (ct.IsCancellationRequested) return;
                    if (string.IsNullOrEmpty(moduleTypeName)) continue;

                    try
                    {
                        var type = Type.GetType(moduleTypeName);
                        if (type == null)
                        {
                            // 尝试从已加载的程序集中查找
                            var assemblies = GetAssembliesToScan();
                            foreach (var assembly in assemblies)
                            {
                                type = assembly.GetType(moduleTypeName);
                                if (type != null) break;
                            }
                        }

                        if (type == null)
                        {
                            _logger.LogWarning($"配置中的模块类型 [{moduleTypeName}] 未找到，跳过注册。");
                            continue;
                        }

                        // 验证模块类型
                        if (type.IsInterface || type.IsAbstract)
                        {
                            _logger.LogWarning($"模块类型 [{moduleTypeName}] 是接口或抽象类，跳过注册。");
                            continue;
                        }

                        if (type.GetConstructor(Type.EmptyTypes) == null)
                        {
                            _logger.LogWarning($"模块类型 [{moduleTypeName}] 缺少无参构造函数，跳过注册。");
                            continue;
                        }

                        var attr = type.GetCustomAttribute<AutoModuleAttribute>();
                        if (attr == null)
                        {
                            _logger.LogWarning($"模块类型 [{moduleTypeName}] 没有 AutoModuleAttribute，跳过注册。");
                            continue;
                        }

                        if (!typeof(IModule).IsAssignableFrom(type))
                        {
                            _logger.LogWarning($"模块类型 [{moduleTypeName}] 未实现 IModule 接口，跳过注册。");
                            continue;
                        }

                        var deps = GetModuleDependenciesFromAttributes(type).ToArray();
                        list.Add((type, attr, deps));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"处理配置中的模块 [{moduleTypeName}] 时出错: {ex.Message}");
                    }
                }

                // 检查模块依赖是否都已启用
                CheckModuleDependencies(list, enabledModuleTypeNames);
            }
            else
            {
                // 回退到扫描模式
                _logger.LogDebug("配置中未提供已启用模块列表，使用扫描模式。");
                await AutoDiscoverModulesByScan(ct, list);
            }

            // 拓扑排序（使用通用方法）
            var typeToDeps = list.ToDictionary(n => n.type, n => (IEnumerable<Type>)(n.deps ?? Array.Empty<Type>()));

            var sorted = TopologicalSortTypes(
                typeToDeps.Keys,
                t => typeToDeps.TryGetValue(t, out var d) ? d : Array.Empty<Type>()
            );

            foreach (var moduleType in sorted)
            {
                if (ct.IsCancellationRequested) return;
                try
                {
                    if (Activator.CreateInstance(moduleType) is IModule module)
                    {
                        if (ct.IsCancellationRequested) return;
                        await RegisterModule(module);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"创建模块 [{moduleType.Name}] 的实例失败: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 检查模块依赖是否都已启用
        /// </summary>
        private void CheckModuleDependencies(List<(Type type, AutoModuleAttribute auto, Type[] deps)> moduleList,
            string[] enabledModuleTypeNames)
        {
            var enabledTypes = new HashSet<Type>();
            var enabledTypeNames = new HashSet<string>(enabledModuleTypeNames);

            // 从已启用模块名构建 Type 集合
            var assemblies = GetAssembliesToScan();
            foreach (var moduleName in enabledModuleTypeNames)
            {
                if (string.IsNullOrEmpty(moduleName)) continue;

                var type = Type.GetType(moduleName);
                if (type == null)
                {
                    foreach (var assembly in assemblies)
                    {
                        type = assembly.GetType(moduleName);
                        if (type != null) break;
                    }
                }

                if (type != null)
                {
                    enabledTypes.Add(type);
                }
            }

            // 检查每个模块的依赖
            foreach (var (moduleType, attr, deps) in moduleList)
            {
                if (deps == null || deps.Length == 0) continue;

                foreach (var dep in deps)
                {
                    if (dep == null) continue;

                    // 如果依赖是接口或抽象类，检查是否有实现被启用
                    if (dep.IsInterface || dep.IsAbstract)
                    {
                        bool hasEnabledImplementation = false;
                        foreach (var enabledType in enabledTypes)
                        {
                            if (dep.IsAssignableFrom(enabledType))
                            {
                                hasEnabledImplementation = true;
                                break;
                            }
                        }

                        if (!hasEnabledImplementation)
                        {
                            var attrInfo = attr != null ? $" [{attr.ModuleName}]" : "";
                            _logger.LogError(
                                $"模块 [{moduleType.Name}]{attrInfo} 依赖的接口/抽象类 [{dep.Name}] 没有任何已启用的实现，可能导致运行时错误。");
                        }
                    }
                    else
                    {
                        // 具体类型依赖，检查是否在已启用列表中
                        if (!enabledTypeNames.Contains(dep.FullName) && !enabledTypes.Contains(dep))
                        {
                            var attrInfo = attr != null ? $" [{attr.ModuleName}]" : "";
                            _logger.LogError($"模块 [{moduleType.Name}]{attrInfo} 依赖的模块 [{dep.Name}] 未在配置中启用，可能导致运行时错误。");
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 通过扫描程序集发现模块（回退模式）
        /// </summary>
        private async UniTask AutoDiscoverModulesByScan(CancellationToken ct,
            List<(Type type, AutoModuleAttribute auto, Type[] deps)> list)
        {
            var assemblies = GetAssembliesToScan();

            foreach (var assembly in assemblies)
            {
                if (ct.IsCancellationRequested) return;

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    if (ex.LoaderExceptions != null && ex.LoaderExceptions.Length > 0)
                    {
                        foreach (var loaderEx in ex.LoaderExceptions)
                        {
                            if (loaderEx != null)
                            {
                                Debug.LogWarning($"程序集 [{assembly.FullName}] 类型加载异常: {loaderEx.Message}");
                            }
                        }
                    }

                    types = ex.Types.Where(t => t != null).ToArray();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"在程序集 [{assembly.FullName}] 中发现模块时出错: {ex.Message}");
                    continue;
                }

                foreach (var t in types)
                {
                    if (ct.IsCancellationRequested) return;
                    if (t == null) continue;
                    if (t.IsInterface || t.IsAbstract) continue;
                    if (t.GetConstructor(Type.EmptyTypes) == null) continue;
                    var attr = t.GetCustomAttribute<AutoModuleAttribute>();
                    if (attr == null) continue;
                    if (!typeof(IModule).IsAssignableFrom(t))
                    {
                        _logger.LogWarning($"自动注册模块 [{t.Name}] 未实现 IModule 接口，将被忽略。");
                        continue;
                    }

                    // 检查模块是否在配置中启用
                    if (!_options.IsModuleEnabled(t.FullName))
                    {
                        _logger.LogDebug($"模块 [{attr.ModuleName}] ({t.FullName}) 已在配置中禁用，跳过注册。");
                        continue;
                    }

                    var deps = GetModuleDependenciesFromAttributes(t).ToArray();
                    list.Add((t, attr, deps));
                }
            }
        }

        private static IEnumerable<Type> GetModuleDependenciesFromAttributes(Type moduleType)
        {
            if (moduleType == null)
            {
                return Array.Empty<Type>();
            }

            var attributes = moduleType.GetCustomAttributes<ModuleDependsOnAttribute>(false);

            return attributes
                .Where(attribute => attribute?.Dependencies != null)
                .SelectMany(attribute => attribute.Dependencies)
                .Distinct();
        }

        private IEnumerable<Type> TopologicalSortTypes(
            IEnumerable<Type> types,
            Func<Type, IEnumerable<Type>> getDependencies)
        {
            var nodeTypes = new HashSet<Type>(types);
            var indegree = new Dictionary<Type, int>();
            var edges = new Dictionary<Type, List<Type>>();

            foreach (var t in nodeTypes)
            {
                indegree.TryAdd(t, 0);
                var deps = getDependencies(t) ?? Array.Empty<Type>();

                foreach (var dep in deps)
                {
                    if (dep == null)
                    {
                        continue;
                    }

                    // 依赖类型本身在当前集合中：直接添加依赖边 dep -> t
                    if (nodeTypes.Contains(dep))
                    {
                        if (!edges.TryGetValue(dep, out var directList))
                        {
                            directList = new List<Type>();
                            edges[dep] = directList;
                        }

                        directList.Add(t);
                        indegree[t] = indegree.TryGetValue(t, out var d) ? d + 1 : 1;
                        indegree.TryAdd(dep, 0);
                        continue;
                    }

                    // 接口或抽象基类依赖：扩展为对所有实现/派生模块的依赖
                    if (dep.IsInterface || dep.IsAbstract)
                    {
                        foreach (var impl in nodeTypes)
                        {
                            if (impl == null || impl == dep) continue;
                            if (!dep.IsAssignableFrom(impl)) continue;
                            if (impl.IsInterface || impl.IsAbstract) continue;

                            if (!edges.TryGetValue(impl, out var implList))
                            {
                                implList = new List<Type>();
                                edges[impl] = implList;
                            }

                            implList.Add(t);
                            indegree[t] = indegree.TryGetValue(t, out var d2) ? d2 + 1 : 1;
                            indegree.TryAdd(impl, 0);
                        }
                    }

                    // 对于其他不在节点集合中的具体类型依赖（例如已提前注册的模块），
                    // 在当前拓扑排序中忽略。
                }
            }

            var queue = new Queue<Type>(indegree.Where(kv => kv.Value == 0).Select(kv => kv.Key));

            var result = new List<Type>();
            int visited = 0;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                result.Add(current);
                visited++;

                if (edges.TryGetValue(current, out var nexts))
                {
                    foreach (var v in nexts)
                    {
                        indegree[v]--;
                        if (indegree[v] == 0)
                        {
                            queue.Enqueue(v);
                        }
                    }
                }
            }

            if (visited != indegree.Count)
            {
                var cycle = DetectCycle(edges, indegree);
                var cycleStr = cycle != null ? string.Join(" -> ", cycle.Select(t => t.Name)) : "未知循环";
                _logger.LogError($"模块依赖存在循环: {cycleStr}");
                _logger.LogError("无法完成拓扑排序。将回退为按 Priority 排序，这可能导致运行时依赖未满足的错误。");
                return nodeTypes;
            }

            return result;
        }

        /// <summary>
        /// 检测并返回循环依赖的模块类型列表
        /// </summary>
        private List<Type> DetectCycle(Dictionary<Type, List<Type>> edges, Dictionary<Type, int> indegree)
        {
            // 找出所有未被访问的节点（indegree > 0 或者在剩余的边中）
            var remainingNodes = new HashSet<Type>(indegree.Where(kv => kv.Value > 0).Select(kv => kv.Key));

            foreach (var start in remainingNodes)
            {
                var visited = new HashSet<Type>();
                var path = new List<Type>();
                if (HasCycleFrom(start, edges, visited, path, new HashSet<Type>()))
                {
                    return path;
                }
            }

            return null;
        }

        /// <summary>
        /// 从指定节点开始DFS检测循环
        /// </summary>
        private bool HasCycleFrom(
            Type current,
            Dictionary<Type, List<Type>> edges,
            HashSet<Type> visited,
            List<Type> path,
            HashSet<Type> recursionStack)
        {
            if (recursionStack.Contains(current))
            {
                // 找到循环，将循环路径添加到path中
                var cycleIndex = path.IndexOf(current);
                if (cycleIndex >= 0)
                {
                    path.Add(current);
                    return true;
                }

                return false;
            }

            if (visited.Contains(current))
            {
                return false;
            }

            visited.Add(current);
            recursionStack.Add(current);
            path.Add(current);

            if (edges.TryGetValue(current, out var dependencies))
            {
                foreach (var dep in dependencies)
                {
                    if (HasCycleFrom(dep, edges, visited, path, recursionStack))
                    {
                        return true;
                    }
                }
            }

            recursionStack.Remove(current);
            if (path.Count > 0)
            {
                path.RemoveAt(path.Count - 1);
            }

            return false;
        }

        public async UniTask DisposeAsync()
        {
            // 根据依赖关系进行拓扑排序，然后按逆序销毁，确保被依赖者最后销毁
            var registeredTypes = _modules.Keys.ToArray();
            var topoOrder = TopologicalSortTypes(
                registeredTypes,
                t => GetModuleDependenciesFromAttributes(t)
            ).ToArray();

            for (int i = topoOrder.Length - 1; i >= 0; i--)
            {
                await UnregisterModuleAsync(topoOrder[i], CancellationToken.None);
            }

            _updateModules.Clear();
            _lateUpdates.Clear();
            _physicsUpdates.Clear();
            _pauseHandlers.Clear();
            _focusHandlers.Clear();
            _quitHandlers.Clear();

            _logger.LogDebug("模块管理器卸载完成!");
        }
    }
}