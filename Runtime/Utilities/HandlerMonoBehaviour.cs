using System;
using UnityEngine;

namespace CFramework.Core.com.cnoom.cframework.core.Runtime.Utilities
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