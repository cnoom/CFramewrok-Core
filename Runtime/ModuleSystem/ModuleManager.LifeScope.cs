using System.Collections.Generic;
using CFramework.Core.Interfaces.LifeScope;

namespace CFramework.Core.ModuleSystem
{
    public partial class ModuleManager
    {
        private readonly List<IUpdate> _updateModules = new();
        private readonly List<ILateUpdate> _lateUpdates = new();
        private readonly List<IPhysicsUpdate> _physicsUpdates = new();
        private readonly List<IPauseHandler> _pauseHandlers = new();
        private readonly List<IFocusHandler> _focusHandlers = new();
        private readonly List<IQuitHandler> _quitHandlers = new();

        private readonly List<IUpdate> _tmpUpdate = new();
        private readonly List<ILateUpdate> _tmpLateUpdate = new();
        private readonly List<IPhysicsUpdate> _tmpPhysicsUpdate = new();
        private readonly List<IPauseHandler> _tmpPauseHandlers = new();
        private readonly List<IFocusHandler> _tempFocusHandlers = new();
        private readonly List<IQuitHandler> _tmpQuitHandlers = new();


        public void Update()
        {
            _tmpUpdate.AddRange(_updateModules);
            foreach (IUpdate module in _tmpUpdate) module.Update();
            _tmpUpdate.Clear();
        }

        public void LateUpdate()
        {
            _tmpLateUpdate.AddRange(_lateUpdates);
            foreach (ILateUpdate module in _tmpLateUpdate) module.LateUpdate();
            _tmpLateUpdate.Clear();
        }

        public void PhysicsUpdate()
        {
            _tmpPhysicsUpdate.AddRange(_physicsUpdates);
            foreach (var m in _tmpPhysicsUpdate) m.PhysicsUpdate();
            _tmpPhysicsUpdate.Clear();
        }

        public void OnApplicationPause(bool isPaused)
        {
            _tmpPauseHandlers.AddRange(_pauseHandlers);
            foreach (var m in _tmpPauseHandlers) m.OnApplicationPause(isPaused);
            _tmpPauseHandlers.Clear();
        }

        public void OnApplicationFocus(bool hasFocus)
        {
            _tempFocusHandlers.AddRange(_focusHandlers);
            foreach (var m in _tempFocusHandlers) m.OnApplicationFocus(hasFocus);
            _tempFocusHandlers.Clear();
        }

        public void OnApplicationQuit()
        {
            _tmpQuitHandlers.AddRange(_quitHandlers);
            foreach (var m in _tmpQuitHandlers) m.OnApplicationQuit();
            _tmpQuitHandlers.Clear();
        }
    }
}