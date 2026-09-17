using System.Reflection;
using HeatRise.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeatRise.EditorTools
{
    public static class MenuVerify
    {
        const string ScenePath = "Assets/TowerRush/Scenes/HeatRise_LAN.unity";

        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject menuRoot = GameObject.Find("MenuRoot");
            if (menuRoot == null) { Debug.LogError("[MenuVerify] MenuRoot not found."); return; }
            LanMenuView view = menuRoot.GetComponent<LanMenuView>();
            if (view == null) { Debug.LogError("[MenuVerify] LanMenuView component not found on MenuRoot."); return; }

            int missing = 0;
            foreach (FieldInfo field in typeof(LanMenuView).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!typeof(Object).IsAssignableFrom(field.FieldType)) continue;
                Object value = field.GetValue(view) as Object;
                if (value == null)
                {
                    Debug.LogError("[MenuVerify] LanMenuView." + field.Name + " is UNASSIGNED (null).");
                    missing++;
                }
            }

            GameObject canvas = GameObject.Find("LanMenuCanvas");
            GameObject eventSystem = GameObject.Find("EventSystem");
            Debug.Log("[MenuVerify] Canvas found: " + (canvas != null) + " | EventSystem found: " + (eventSystem != null));

            if (missing == 0) Debug.Log("[MenuVerify] PASS - all LanMenuView object references are wired.");
            else Debug.LogError("[MenuVerify] FAIL - " + missing + " unassigned reference(s) on LanMenuView.");
        }
    }
}
