using UnityEditor;
using UnityEngine;

namespace Texy
{
    /// <summary>Lazily-built GUI styles so the window stays visually consistent and tidy.</summary>
    internal static class TexyStyles
    {
        private static GUIStyle _header;
        private static GUIStyle _sectionTitle;
        private static GUIStyle _hint;
        private static GUIStyle _card;

        public static GUIStyle Header => _header ?? (_header = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 16,
            margin = new RectOffset(2, 2, 6, 8)
        });

        public static GUIStyle SectionTitle => _sectionTitle ?? (_sectionTitle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 12,
            margin = new RectOffset(2, 2, 8, 2)
        });

        public static GUIStyle Hint => _hint ?? (_hint = new GUIStyle(EditorStyles.miniLabel)
        {
            wordWrap = true,
            fontStyle = FontStyle.Italic,
            normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
        });

        public static GUIStyle Card => _card ?? (_card = new GUIStyle(EditorStyles.helpBox)
        {
            padding = new RectOffset(10, 10, 8, 8),
            margin = new RectOffset(2, 2, 4, 4)
        });
    }
}
