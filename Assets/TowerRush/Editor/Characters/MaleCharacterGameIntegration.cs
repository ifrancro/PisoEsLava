#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HeatRise.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HeatRise.EditorTools
{
    public static class MaleCharacterGameIntegration
    {
        const string VisualPrefab = "Assets/TowerRush/Models/MaleCharacter/Unity/MaleCharacter.prefab";

        [MenuItem("Heat Rise/Personajes/Integrar personaje en el juego")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("La integracion requiere salir del modo Play.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Guarda las escenas abiertas antes de integrar el personaje.");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            string backup = Path.Combine("Logs/CharacterIntegration/Backups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backup);
            File.Copy(MaleCharacterIntegrationChecks.Solo, Path.Combine(backup, "HeatRise_Jugable.unity"));
            File.Copy(MaleCharacterIntegrationChecks.NetworkPrefab, Path.Combine(backup, "Jugador_LAN.prefab"));
            try
            {
                PrepareMaterials();
                GameObject visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefab);
                if (visual == null) throw new InvalidOperationException("Falta el prefab visual masculino.");
                GameObject contents = PrefabUtility.LoadPrefabContents(MaleCharacterIntegrationChecks.NetworkPrefab);
                try
                {
                    PlayerController player = contents.GetComponent<PlayerController>();
                    ReplaceVisual(player, visual);
                    OwnerNetworkAnimator sync = contents.GetComponent<OwnerNetworkAnimator>();
                    if (sync == null) sync = contents.AddComponent<OwnerNetworkAnimator>();
                    sync.Animator = player.model.GetComponentInChildren<Animator>(true);
                    PrefabUtility.SaveAsPrefabAsset(contents, MaleCharacterIntegrationChecks.NetworkPrefab);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }

                Scene scene = EditorSceneManager.OpenScene(MaleCharacterIntegrationChecks.Solo);
                string before = MapSignature(scene);
                var solo = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).Single();
                ReplaceVisual(solo, visual);
                if (MapSignature(scene) != before) throw new InvalidOperationException("Se alteraron objetos del mapa durante la integracion.");
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                MaleCharacterIntegrationChecks.ValidateInstallation();
                Debug.Log("CHARACTER_INTEGRATION INSTALLED: solo + Jugador_LAN. Backup: " + backup);
            }
            finally { if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }

        static void PrepareMaterials()
        {
            const string model = "Assets/TowerRush/Models/MaleCharacter/MaleCharacter.fbx";
            const string folder = "Assets/TowerRush/Models/MaleCharacter/Unity/Materials";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/TowerRush/Models/MaleCharacter/Unity", "Materials");
            var importer = (ModelImporter)AssetImporter.GetAtPath(model);
            foreach (Material embedded in AssetDatabase.LoadAllAssetsAtPath(model).OfType<Material>())
            {
                string path = folder + "/" + embedded.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(embedded);
                    material.name = embedded.name;
                    // Blender's plain palette is authored in linear RGB. Unity Standard's
                    // color property uses the artist-facing sRGB palette in this project.
                    material.color = embedded.color.gamma;
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Glossiness", .22f);
                    // A restrained ambient contribution keeps the face readable inside the tower.
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", material.color * .12f);
                    AssetDatabase.CreateAsset(material, path);
                }
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), embedded.name), material);
            }
            importer.SaveAndReimport();
        }

        static void ReplaceVisual(PlayerController player, GameObject source)
        {
            if (player.model != null && player.model.GetComponentInChildren<MaleCharacterView>(true) != null)
                return; // Re-running the installer preserves later visual edits.
            var old = new HashSet<GameObject>();
            foreach (Transform model in new[] { player.model, player.smallModel, player.largeModel })
                if (model != null) old.Add(model.gameObject);
            player.model = player.smallModel = player.largeModel = null;
            foreach (GameObject model in old) Object.DestroyImmediate(model);
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(source, player.gameObject.scene);
            visual.name = "Personaje_Masculino";
            visual.transform.SetParent(player.transform, false);
            foreach (Transform part in visual.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            Animator animator = visual.GetComponentInChildren<Animator>(true);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            player.model = visual.transform;
            var serialized = new SerializedObject(player);
            serialized.FindProperty("characterView").objectReferenceValue = visual.GetComponent<MaleCharacterView>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(player);
        }

        static string MapSignature(Scene scene)
        {
            var text = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects().Where(o => o.name == "HEAT_RISE_MODELOS"))
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.GetComponentInParent<PlayerController>() != null) continue;
                    text.Append(t.name).Append('|').Append(JsonUtility.ToJson(new TransformData(t))).AppendLine();
                }
            using (var sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text.ToString())));
        }

        [Serializable] sealed class TransformData
        {
            public Vector3 position, scale;
            public Quaternion rotation;
            public TransformData(Transform t) { position = t.localPosition; scale = t.localScale; rotation = t.localRotation; }
        }
    }
}
#endif
