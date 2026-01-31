using UnityEngine;

namespace CFramework.Core.Utilities
{
    public class HandlerMonobehaviour : MonoBehaviour
    {
        protected virtual void Awake()
        {
            CF.TryRegisterHandler(this);
        }

        protected virtual void OnDestroy()
        {
            CF.TryUnregisterHandler(this);
        }
    }
}