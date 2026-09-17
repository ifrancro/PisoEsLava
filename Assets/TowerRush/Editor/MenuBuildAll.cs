using UnityEditor;
using UnityEditor.SceneManagement;

namespace HeatRise.EditorTools
{
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
            LobbyMenuBuilder.Build();
            HelpMenuBuilder.Build();
        }

        /// <summary>Solo los paneles nuevos: sala online y ayuda.</summary>
        public static void BuildPanels()
        {
            LobbyMenuBuilder.Build();
            HelpMenuBuilder.Build();
        }
    }
}
