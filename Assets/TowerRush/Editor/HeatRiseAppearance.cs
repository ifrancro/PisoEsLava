#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeatRise.Editor
{
    public static class HeatRiseAppearance
    {
        const string Root = "Assets/TowerRush/";
        static Material gray, concrete, metal, lava, fragile, moving, danger, block, player, green;

        [MenuItem("Heat Rise/Aplicar materiales y colores")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                AddMissingUVs();
                gray = Load("Gris_Modelado");
                concrete = Load("concreto_material");
                metal = Load("metal_model");
                lava = Load("lava_model");
                green = Load("Checkpoint_Verde");
                fragile = Tint("Fragil_Ambar", new Color(1f, 0.65f, 0.16f));
                moving = Tint("Movil_Cian", new Color(0.2f, 0.82f, 0.93f));
                danger = Tint("Peligro_Rojo", new Color(0.95f, 0.19f, 0.26f));
                block = Tint("Bloque_Violeta", new Color(0.65f, 0.35f, 0.95f));
                player = Tint("Jugador_Rosa", new Color(0.96f, 0.34f, 0.65f));

                Scene lan = EditorSceneManager.OpenScene(Root + "Scenes/HeatRise_LAN.unity");
                Dictionary<string, Material[]> authored = new Dictionary<string, Material[]>();
                foreach (GameObject root in lan.GetRootGameObjects())
                    foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                        if (renderer.sharedMaterial != null && renderer.sharedMaterial != gray)
                            authored[Path(renderer.transform)] = renderer.sharedMaterials;
                ApplyScene(lan, null);

                Scene solo = EditorSceneManager.OpenScene(Root + "Scenes/HeatRise_Jugable.unity");
                ApplyScene(solo, authored);

                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Root + "Prefabs" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                    try
                    {
                        Paint(prefab, null);
                        PrefabUtility.SaveAsPrefabAsset(prefab, path);
                    }
                    finally { PrefabUtility.UnloadPrefabContents(prefab); }
                }
                AssetDatabase.SaveAssets();
                Debug.Log("HEAT_RISE_APPEARANCE PASS: UV completas, materiales conservados y colores aplicados a LAN, solo y prefabs.");
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        static void ApplyScene(Scene scene, Dictionary<string, Material[]> authored)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) Paint(root, authored);
            LavaRise surface = UnityEngine.Object.FindObjectOfType<LavaRise>();
            if (surface == null || surface.GetComponent<MeshRenderer>().sharedMaterial != lava)
                throw new InvalidOperationException("La superficie debe conservar lava_model: " + scene.name);
            if (lava.mainTexture == null || concrete.mainTexture == null || metal.mainTexture == null)
                throw new InvalidOperationException("Los materiales originales deben conservar sus texturas.");
            CheckColor<FragilePlatform>("Plataforma_Con_Collider", fragile);
            CheckColor<HeavyBlock>("Bloque_Con_Collider", block);
            CheckColor<HammerSwing>("Cabeza", danger);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("No se guardo " + scene.path);
        }

        static void CheckColor<T>(string part, Material expected) where T : Component
        {
            foreach (T item in UnityEngine.Object.FindObjectsOfType<T>(true))
            {
                bool found = false;
                foreach (MeshRenderer renderer in item.GetComponentsInChildren<MeshRenderer>(true))
                    if (renderer.name == part)
                    {
                        found = true;
                        if (renderer.sharedMaterial != expected)
                            throw new InvalidOperationException("Color incorrecto: " + Path(renderer.transform));
                    }
                if (!found) throw new InvalidOperationException("Falta superficie visible: " + item.name);
            }
        }

        static void Paint(GameObject root, Dictionary<string, Material[]> authored)
        {
            foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                string path = Path(renderer.transform);
                if (renderer.sharedMaterial != gray && renderer.sharedMaterial != null) continue;
                if (authored != null && authored.TryGetValue(path, out Material[] original))
                {
                    renderer.sharedMaterials = original;
                    continue;
                }
                Material material = MaterialFor(renderer, path);
                if (material == null) continue;
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] == gray || slots[i] == null) slots[i] = material;
                renderer.sharedMaterials = slots;
            }
        }

        static Material MaterialFor(MeshRenderer renderer, string path)
        {
            string name = renderer.name;
            if (path.Contains("Personaje_") || renderer.GetComponentInParent<PlayerController>() != null) return player;
            if (path.Contains("Superficie_Lava")) return lava;
            if (path.Contains("Checkpoint"))
                return name == "Plataforma_Exterior" || name == "Pasarela" ? concrete : metal;
            if (path.Contains("Portal_Meta")) return name == "Zona_Meta" ? null : green;
            if (path.Contains("Plataforma_") && name == "Plataforma_Con_Collider")
                return path.Contains("_Fragil/") ? fragile : path.Contains("_Movil/") ? moving : concrete;
            if (path.Contains("Martillo")) return name == "Cabeza" ? danger : metal;
            if (path.Contains("Barra")) return name == "Barra" ? danger : metal;
            if (path.Contains("Prensa")) return name == "Pieza_00" || name == "Pieza_03" ? danger : metal;
            if (path.Contains("Bloque")) return name == "Bloque_Con_Collider" ? block : metal;
            if (path.Contains("Conducto")) return name.StartsWith("Detalle_") ? moving : metal;
            if (path.Contains("06_Entorno") || path.Contains("Nucleo_Hexagonal")) return concrete;
            if (path.Contains("07_Referencias")) return null;
            return metal;
        }

        static string Path(Transform value)
        {
            string result = value.name;
            for (Transform parent = value.parent; parent != null; parent = parent.parent)
                result = parent.name + "/" + result;
            return result;
        }

        static Material Load(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(Root + "Materials/" + name + ".mat");
            if (material == null) throw new InvalidOperationException("Falta material " + name);
            return material;
        }

        static Material Tint(string name, Color color)
        {
            string path = Root + "Materials/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Standard")) { name = name, color = color };
            material.SetFloat("_Glossiness", 0.2f);
            material.SetFloat("_Metallic", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void AddMissingUVs()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Mesh", new[] { Root + "Meshes" }))
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));
                if (mesh.uv.Length == mesh.vertexCount) continue;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;
                if (normals.Length != vertices.Length) throw new InvalidOperationException("Faltan normales: " + mesh.name);
                Vector2[] uv = new Vector2[vertices.Length];
                Bounds bounds = mesh.bounds;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = vertices[i] - bounds.min;
                    p = new Vector3(p.x / Mathf.Max(bounds.size.x, 0.001f),
                        p.y / Mathf.Max(bounds.size.y, 0.001f), p.z / Mathf.Max(bounds.size.z, 0.001f));
                    Vector3 n = normals[i];
                    uv[i] = Mathf.Abs(n.y) >= Mathf.Max(Mathf.Abs(n.x), Mathf.Abs(n.z))
                        ? new Vector2(p.x, p.z) : Mathf.Abs(n.x) >= Mathf.Abs(n.z)
                            ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y);
                }
                mesh.uv = uv;
                mesh.RecalculateTangents();
                EditorUtility.SetDirty(mesh);
                if (mesh.uv.Length != mesh.vertexCount) throw new InvalidOperationException("UV incompletas: " + mesh.name);
                bool varied = false;
                for (int i = 1; i < uv.Length; i++) varied |= (uv[i] - uv[0]).sqrMagnitude > 0.001f;
                if (!varied) throw new InvalidOperationException("UV sin superficie: " + mesh.name);
                Debug.Log("HEAT_RISE_UV " + mesh.name + ": " + mesh.vertexCount + " vertices");
            }
        }
    }
}
#endif
