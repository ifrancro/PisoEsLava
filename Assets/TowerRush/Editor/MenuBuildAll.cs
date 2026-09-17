using UnityEditor;
using UnityEditor.SceneManagement;

namespace HeatRise.EditorTools
{
    /// Single entry point for `-executeMethod`: runs the whole LAN menu build pipeline in one
    /// Editor session (TMP essentials -> font assets -> sprites -> hierarchy -> save scene).
    public static class MenuBuildAll
    {
        const string ScenePath = "Assets/TowerRush/Scenes/HeatRise_LAN.unity";

        public static void Run()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MenuFontPipeline.ImportTmpEssentials();
            AssetDatabase.Refresh();
            MenuFontPipeline.GenerateFontAssets();
            MenuAssetGenerator.GenerateAll();
            LanMenuBuilder.Build();
        }
    }
}
