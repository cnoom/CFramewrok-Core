using System.Collections.Generic;
using CFramework.Core.Interfaces.LifeScope;

namespace CFramework.Core.ModuleSystem
{
    public partial class ModuleManager
    {
        private readonly List<IFocusHandler> _focusHandlers = new List<IFocusHandler>();
        private readonly List<ILateUpdate> _lateUpdates = new List<ILateUpdate>();
        private readonly List<IPauseHandler> _pauseHandlers = new List<IPauseHandler>();
        private readonly List<IPhysicsUpdate> _physicsUpdates = new List<IPhysicsUpdate>();
        private readonly List<IQuitHandler> _quitHandlers = new List<IQuitHandler>();
        private readonly List<IFocusHandler> _tempFocusHandlers = new List<IFocusHandler>();
        private readonly List<ILateUpdate> _tmpLateUpdate = new List<ILateUpdate>();
        private readonly List<IPauseHandler> _tmpPauseHandlers = new List<IPauseHandler>();
        private readonly List<IPhysicsUpdate> _tmpPhysicsUpdate = new List<IPhysicsUpdate>();
        private readonly List<IQuitHandler> _tmpQuitHandlers = new List<IQuitHandler>();

        private readonly List<IUpdate> _tmpUpdate = new List<IUpdate>();
        private readonly List<IUpdate> _updateModules = new List<IUpdate>();

        public void LateUpdate()
        {
            _tmpLateUpdate.AddRange(_lateUpdates);
            foreach (ILateUpdate module in _tmpLateUpdate) module.LateUpdate();
            _tmpLateUpdate.Clear();
        }


        public void Update()
        {
            _tmpUpdate.AddRange(_updateModules);
            foreach (IUpdate module in _tmpUpdate) module.Update();
            _tmpUpdate.Clear();
        }

        public void PhysicsUpdate()
        {
            _tmpPhysicsUpdate.AddRange(_physicsUpdates);
            foreach (IPhysicsUpdate m in _tmpPhysicsUpdate) m.PhysicsUpdate();
            _tmpPhysicsUpdate.Clear();
        }

        public void OnApplicationPause(bool isPaused)
        {
            _tmpPauseHandlers.AddRange(_pauseHandlers);
            foreach (IPauseHandler m in _tmpPauseHandlers) m.OnApplicationPause(isPaused);
            _tmpPauseHandlers.Clear();
        }

        public void OnApplicationFocus(bool hasFocus)
        {
            _tempFocusHandlers.AddRange(_focusHandlers);
            foreach (IFocusHandler m in _tempFocusHandlers) m.OnApplicationFocus(hasFocus);
            _tempFocusHandlers.Clear();
        }

        public void OnApplicationQuit()
        {
            _tmpQuitHandlers.AddRange(_quitHandlers);
            foreach (IQuitHandler m in _tmpQuitHandlers) m.OnApplicationQuit();
            _tmpQuitHandlers.Clear();
        }
    }
}