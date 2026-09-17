using System.Reflection;
using HeatRise.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeatRise.EditorTools
{
    public static class MenuVerify
    {
        const string LanScene = "Assets/TowerRush/Scenes/HeatRise_LAN.unity";
        const string GameScene = "Assets/TowerRush/Scenes/HeatRise_Jugable.unity";

        public static void Run()
        {
            int missing = 0;

            EditorSceneManager.OpenScene(LanScene, OpenSceneMode.Single);
            missing += CheckOn<LanMenuView>("MenuRoot");
            missing += CheckOn<LobbyView>("LobbyCanvas");
            missing += CheckOn<HelpView>("HelpCanvas");
            Debug.Log("[MenuVerify] HeatRise_LAN | LanMenuCanvas: " + (GameObject.Find("LanMenuCanvas") != null)
                + " | LobbyCanvas: " + (GameObject.Find("LobbyCanvas") != null)
                + " | HelpCanvas: " + (GameObject.Find("HelpCanvas") != null)
                + " | EventSystem: " + (GameObject.Find("EventSystem") != null));

            EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Single);
            missing += CheckOn<PauseMenuView>("PauseCanvas");
            missing += CheckOn<HelpView>("HelpCanvas");
            Debug.Log("[MenuVerify] HeatRise_Jugable | PauseCanvas: " + (GameObject.Find("PauseCanvas") != null)
                + " | HelpCanvas: " + (GameObject.Find("HelpCanvas") != null));

            if (missing == 0) Debug.Log("[MenuVerify] PASS - todas las referencias de UI estan asignadas.");
            else Debug.LogError("[MenuVerify] FAIL - " + missing + " referencia(s) sin asignar.");
        }

        static int CheckOn<T>(string objectName) where T : MonoBehaviour
        {
            GameObject host = GameObject.Find(objectName);
            if (host == null)
            {
                Debug.LogError("[MenuVerify] No se encontro el objeto " + objectName + ".");
                return 1;
            }
            T view = host.GetComponent<T>();
            if (view == null)
            {
                Debug.LogError("[MenuVerify] " + objectName + " no tiene el componente " + typeof(T).Name + ".");
                return 1;
            }

            int missing = 0;
            foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (typeof(Object).IsAssignableFrom(field.FieldType))
                {
                    if (field.GetValue(view) as Object == null)
                    {
                        Debug.LogError("[MenuVerify] " + typeof(T).Name + "." + field.Name + " SIN ASIGNAR.");
                        missing++;
                    }
                    continue;
                }

                if (!field.FieldType.IsArray || !typeof(Object).IsAssignableFrom(field.FieldType.GetElementType())) continue;
                Object[] values = field.GetValue(view) as Object[];
                if (values == null || values.Length == 0)
                {
                    Debug.LogError("[MenuVerify] " + typeof(T).Name + "." + field.Name + " VACIO.");
                    missing++;
                    continue;
                }
                for (int i = 0; i < values.Length; i++)
                {
                    if (values[i] != null) continue;
                    Debug.LogError("[MenuVerify] " + typeof(T).Name + "." + field.Name + "[" + i + "] SIN ASIGNAR.");
                    missing++;
                }
            }
            return missing;
        }
    }
}
