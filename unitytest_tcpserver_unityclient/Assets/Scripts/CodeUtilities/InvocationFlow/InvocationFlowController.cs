﻿using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace InvocationFlow.Unity3D
{
    /// <summary>
    /// Will iterate all the delegates used in CustomFunctionInvokes
    /// </summary>
    public class InvocationFlowController : MonoBehaviour
    {
        public static GameObject singleton = null;

        private void Awake()
        {
            if (singleton != gameObject && ReferenceEquals(singleton, null) == false)
            {
                Destroy(gameObject);
                return;
            }

            DontDestroyOnLoad(gameObject);
#if UNITY_EDITOR
            int highestExecutionOrder = 0;
            MonoScript controllerScript = MonoScript.FromMonoBehaviour(this);
            MonoScript[] allScripts = MonoImporter.GetAllRuntimeMonoScripts();
            for (int i = 0; i < allScripts.Length; i++)
            {
                if (allScripts[i] != controllerScript)
                {
                    int scriptExecutionOrder = MonoImporter.GetExecutionOrder(allScripts[i]);
                    if (scriptExecutionOrder > highestExecutionOrder)
                    {
                        highestExecutionOrder = scriptExecutionOrder;
                    }
                }
            }
            var controllerExecutionOrder = MonoImporter.GetExecutionOrder(controllerScript);
            if (controllerExecutionOrder != highestExecutionOrder + 100)
            {
                MonoImporter.SetExecutionOrder(controllerScript, highestExecutionOrder + 100);
            }
#endif
        }
        private static bool isInitialized = false;
        public static void Initiate()
        {
            if (isInitialized) return;

            isInitialized = true;
            InvocationFlow<MonoBehaviour>.IsValid = (MonoBehaviour script) => script != null;
            singleton = new GameObject("InvocationFlowController", typeof(InvocationFlowController));
            DontDestroyOnLoad(singleton);
        }

        // Execution order is expected to be after update but before coroutines as long as execution order is not changed.
        void Update()
        {
            InvocationFlow<MonoBehaviour>.IterateInvocationHandlers(Time.deltaTime, Time.unscaledDeltaTime);
        }
    }
}
