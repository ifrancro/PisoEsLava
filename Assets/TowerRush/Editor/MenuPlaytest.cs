using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeatRise.EditorTools
{
    [InitializeOnLoad]
    public static class MenuPlaytest
    {
        const string ScenePath = "Assets/TowerRush/Scenes/HeatRise_LAN.unity";
        const string StageKey = "HeatRise_MenuPlaytestStage";
        static double stageStartTime;

        static MenuPlaytest()
        {
            EditorApplication.update += OnUpdate;
        }

        public static void Start()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            SessionState.SetInt(StageKey, 1);
            EditorApplication.isPlaying = true;
        }

        static void OnUpdate()
        {
            int stage = SessionState.GetInt(StageKey, 0);
            switch (stage)
            {
                case 1:
                    if (EditorApplication.isPlaying && !EditorApplication.isPaused)
                    {
                        stageStartTime = EditorApplication.timeSinceStartup;
                        SessionState.SetInt(StageKey, 2);
                    }
                    break;
                case 2:
                    if (!EditorApplication.isPlaying) { SessionState.SetInt(StageKey, 5); break; }
                    if (EditorApplication.timeSinceStartup - stageStartTime > 2.5)
                    {
                        string path = Path.GetFullPath("menu_playtest_screenshot.png");
                        ScreenCapture.CaptureScreenshot(path);
                        Debug.Log("[MenuPlaytest] Screenshot requested at " + path);
                        stageStartTime = EditorApplication.timeSinceStartup;
                        SessionState.SetInt(StageKey, 3);
                    }
                    break;
                case 3:
                    if (EditorApplication.timeSinceStartup - stageStartTime > 1.0)
                    {
                        Debug.Log("[MenuPlaytest] Exiting Play Mode.");
                        SessionState.SetInt(StageKey, 4);
                        EditorApplication.isPlaying = false;
                    }
                    break;
                case 4:
                    if (!EditorApplication.isPlaying) SessionState.SetInt(StageKey, 5);
                    break;
                case 5:
                    SessionState.SetInt(StageKey, 0);
                    EditorApplication.update -= OnUpdate;
                    Debug.Log("[MenuPlaytest] DONE");
                    EditorApplication.Exit(0);
                    break;
            }
        }
    }
}
