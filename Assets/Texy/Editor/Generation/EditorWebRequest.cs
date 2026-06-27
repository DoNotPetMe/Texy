using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Texy
{
    /// <summary>
    /// Drives a <see cref="UnityWebRequest"/> to completion from the editor without requiring the
    /// com.unity.editorcoroutines package. It polls the request on EditorApplication.update and
    /// invokes the callback on the main thread, so providers can stay simple and dependency-free.
    /// </summary>
    public static class EditorWebRequest
    {
        public static void Send(UnityWebRequest request, Action<UnityWebRequest> onDone, Action<float> onProgress = null)
        {
            var op = request.SendWebRequest();

            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                onProgress?.Invoke(Mathf.Clamp01(op.progress));
                if (!op.isDone) return;

                EditorApplication.update -= poll;
                try { onDone?.Invoke(request); }
                finally { request.Dispose(); }
            };
            EditorApplication.update += poll;
        }
    }
}
