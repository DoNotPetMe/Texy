using UnityEngine;

namespace Texy
{
    /// <summary>Thin logging wrapper so every Texy message is consistently tagged and filterable.</summary>
    internal static class TexyLog
    {
        private const string Tag = "<b>[Texy]</b> ";

        public static void Info(string message) => Debug.Log(Tag + message);
        public static void Warn(string message) => Debug.LogWarning(Tag + message);
        public static void Error(string message) => Debug.LogError(Tag + message);
    }
}
