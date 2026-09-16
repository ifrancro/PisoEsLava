#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeatRise.Editor
{
    public static class HeatRiseGameView
    {
        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.playModeStateChanged += state =>
            {
                if (!Application.isBatchMode && state == PlayModeStateChange.EnteredPlayMode
                    && SceneManager.GetActiveScene().name.StartsWith("HeatRise_"))
                    EditorApplication.delayCall += Fit;
            };
        }

        [MenuItem("Heat Rise/Ajustar pantalla de juego")]
        public static void Fit()
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var scale = type?.GetProperty("minScale", flags);
            var zoom = type?.GetMethod("SnapZoom", flags);
            var constrain = type?.GetMethod("EnforceZoomAreaConstraints", flags);
            if (scale == null || zoom == null || constrain == null)
            {
                Debug.LogWarning("Ajustá Scale al mínimo en la pestaña Game para ver toda la pantalla.");
                return;
            }
            EditorWindow view = EditorWindow.GetWindow(type);
            zoom.Invoke(view, new[] { scale.GetValue(view) });
            constrain.Invoke(view, null);
            view.Repaint();
        }
    }
}
#endif
