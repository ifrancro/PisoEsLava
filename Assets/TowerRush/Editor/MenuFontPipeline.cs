using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace HeatRise.EditorTools
{
    public static class MenuFontPipeline
    {
        const string TtfDir = "Assets/TowerRush/Fonts/TTF";
        const string OutDir = "Assets/TowerRush/Fonts/TMP";

        static readonly (string ttf, string assetName)[] Fonts =
        {
            ("fredoka-latin-600-normal.ttf", "Fredoka-SemiBold SDF"),
            ("fredoka-latin-700-normal.ttf", "Fredoka-Bold SDF"),
            ("nunitosans-latin-600-normal.ttf", "NunitoSans-SemiBold SDF"),
            ("nunitosans-latin-700-normal.ttf", "NunitoSans-Bold SDF"),
            ("nunitosans-latin-800-normal.ttf", "NunitoSans-ExtraBold SDF"),
        };

        [MenuItem("HeatRise/Menu/1 Import TMP Essentials")]
        public static void ImportTmpEssentials()
        {
            if (File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
            {
                Debug.Log("[MenuFontPipeline] TMP Essentials already present, skipping.");
                return;
            }

            string packagePath = FindTmpPackageCachePath();
            if (packagePath == null)
            {
                Debug.LogError("[MenuFontPipeline] Could not locate com.unity.textmeshpro in Library/PackageCache.");
                return;
            }
            string unityPackage = packagePath + "/Package Resources/TMP Essential Resources.unitypackage";
            if (!File.Exists(unityPackage))
            {
                Debug.LogError("[MenuFontPipeline] Missing TMP Essential Resources.unitypackage at: " + unityPackage);
                return;
            }
            AssetDatabase.ImportPackage(unityPackage, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static string FindTmpPackageCachePath()
        {
            string cacheDir = Path.GetFullPath("Library/PackageCache");
            if (!Directory.Exists(cacheDir)) return null;
            foreach (string dir in Directory.GetDirectories(cacheDir, "com.unity.textmeshpro@*"))
                return dir.Replace('\\', '/');
            return null;
        }

        [MenuItem("HeatRise/Menu/2 Generate TMP Font Assets")]
        public static void GenerateFontAssets()
        {
            Directory.CreateDirectory(OutDir);
            foreach ((string ttf, string assetName) in Fonts)
            {
                string srcPath = TtfDir + "/" + ttf;
                Font font = AssetDatabase.LoadAssetAtPath<Font>(srcPath);
                if (font == null)
                {
                    Debug.LogError("[MenuFontPipeline] Missing source font: " + srcPath);
                    continue;
                }

                string outPath = OutDir + "/" + assetName + ".asset";
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null)
                {
                    Debug.Log("[MenuFontPipeline] Already exists, skipping: " + outPath);
                    continue;
                }

                TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                    font, 90, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
                if (fontAsset == null)
                {
                    Debug.LogError("[MenuFontPipeline] CreateFontAsset failed for " + ttf);
                    continue;
                }
                fontAsset.name = assetName;
                AssetDatabase.CreateAsset(fontAsset, outPath);
                if (fontAsset.atlasTextures != null)
                    foreach (Texture2D atlas in fontAsset.atlasTextures)
                        AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                if (fontAsset.material != null)
                {
                    fontAsset.material.name = assetName + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }
                EditorUtility.SetDirty(fontAsset);
                Debug.Log("[MenuFontPipeline] Created " + outPath);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}
