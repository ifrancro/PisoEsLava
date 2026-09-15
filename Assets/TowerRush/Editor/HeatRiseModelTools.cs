#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HeatRise.EditorTools
{
    public static class HeatRiseModelTools
    {
        private const string BasePath = "Assets/TowerRush";
        [Serializable] private class Layout { public NodeData[] nodes; public MeshData[] meshes; }
        [Serializable] private class MeshData { public string key; public string name; public float[] data; }
        [Serializable] private class NodeData
        {
            public string name, mesh, colliderType;
            public int parent;
            public float[] position, rotation, scale, colliderSize, colliderCenter;
            public bool active, trigger, camera, light;
        }
        private static Vector3 V(float[] v) { return new Vector3(v[0], v[1], v[2]); }
        private static Quaternion Q(float[] q) { return new Quaternion(q[0], q[1], q[2], q[3]); }

        [MenuItem("Heat Rise/Abrir escena de modelos")]
        private static void OpenScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(BasePath + "/Scenes/HeatRise_Modelos.unity");
        }

        [MenuItem("Heat Rise/Ajustar material gris al proyecto")]
        private static void AdaptMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(BasePath + "/Materials/Gris_Modelado.mat");
            if (material == null) { Debug.LogError("No se encontro Gris_Modelado.mat."); return; }
            Shader shader = AppropriateShader();
            if (shader == null) { Debug.LogError("No se encontro un shader compatible."); return; }
            Undo.RecordObject(material, "Ajustar material Heat Rise");
            material.shader = shader;
            SetGray(material);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            Debug.Log("Material gris ajustado a " + shader.name + ".");
        }
        [MenuItem("Heat Rise/Reconstruir modelos en una escena nueva")]
        private static void Rebuild()
        {
            TextAsset source = AssetDatabase.LoadAssetAtPath<TextAsset>(BasePath + "/Source/Modelo_Original.json");
            if (source == null) { Debug.LogError("Falta Source/Modelo_Original.json."); return; }
            Layout layout = JsonUtility.FromJson<Layout>(source.text);
            if (layout == null || layout.nodes == null || layout.meshes == null)
            { Debug.LogError("Los datos del modelo no son validos."); return; }

            string folderName = "Reconstruido_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            AssetDatabase.CreateFolder(BasePath, folderName);
            string folder = BasePath + "/" + folderName;
            Shader shader = AppropriateShader();
            if (shader == null) { Debug.LogError("No se encontro un shader para los modelos."); return; }
            Material material = new Material(shader) { name = "Gris_Modelado" };
            SetGray(material);
            AssetDatabase.CreateAsset(material, folder + "/Gris_Modelado.mat");

            Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
            foreach (MeshData data in layout.meshes)
            {
                int count = data.data.Length / 6;
                Vector3[] vertices = new Vector3[count];
                Vector3[] normals = new Vector3[count];
                int[] triangles = new int[count];
                for (int i = 0; i < count; i++)
                {
                    int o = i * 6;
                    vertices[i] = new Vector3(data.data[o], data.data[o + 1], data.data[o + 2]);
                    normals[i] = new Vector3(data.data[o + 3], data.data[o + 4], data.data[o + 5]);
                    triangles[i] = i;
                }
                Mesh mesh = new Mesh { name = data.name };
                mesh.vertices = vertices;
                mesh.normals = normals;
                mesh.triangles = triangles;
                mesh.RecalculateBounds();
                AssetDatabase.CreateAsset(mesh, folder + "/" + data.name + ".asset");
                meshes.Add(data.key, mesh);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            GameObject[] objects = new GameObject[layout.nodes.Length];
            for (int i = 0; i < layout.nodes.Length; i++)
            {
                NodeData data = layout.nodes[i];
                GameObject go = new GameObject(data.name);
                SceneManager.MoveGameObjectToScene(go, scene);
                objects[i] = go;
                if (data.parent >= 0) go.transform.SetParent(objects[data.parent].transform, false);
                go.transform.localPosition = V(data.position);
                go.transform.localRotation = Q(data.rotation);
                go.transform.localScale = V(data.scale);
                go.SetActive(data.active);
                Mesh mesh = null;
                if (!string.IsNullOrEmpty(data.mesh))
                {
                    mesh = meshes[data.mesh];
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    go.AddComponent<MeshRenderer>().sharedMaterial = material;
                }
                if (data.colliderType == "box")
                {
                    BoxCollider collider = go.AddComponent<BoxCollider>();
                    collider.size = V(data.colliderSize);
                    collider.center = V(data.colliderCenter);
                    collider.isTrigger = data.trigger;
                }
                else if (data.colliderType == "mesh" && mesh != null)
                    go.AddComponent<MeshCollider>().sharedMesh = mesh;
                if (data.camera)
                {
                    go.tag = "MainCamera";
                    Camera camera = go.AddComponent<Camera>();
                    camera.fieldOfView = 50;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 600;
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f);
                }
                if (data.light)
                {
                    Light light = go.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.color = Color.white;
                    light.intensity = 1;
                    light.shadows = LightShadows.Soft;
                }
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.5f);
            RenderSettings.fog = false;
            string scenePath = folder + "/HeatRise_Modelos.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = objects[0];
            if (SceneView.lastActiveSceneView != null) SceneView.lastActiveSceneView.FrameSelected();
            Debug.Log("Modelos reconstruidos: " + scenePath + ". La escena anterior se conserva abierta.");
        }

        private static Shader AppropriateShader()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            Shader shader = null;
            if (pipeline != null)
            {
                string type = pipeline.GetType().Name;
                if (type.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0)
                    shader = Shader.Find("Universal Render Pipeline/Lit");
                else if (type.IndexOf("HDRender", StringComparison.OrdinalIgnoreCase) >= 0)
                    shader = Shader.Find("HDRP/Lit");
            }
            else shader = Shader.Find("Standard");
            return shader != null ? shader : Shader.Find("HeatRise/Gris Modelado");
        }
        private static void SetGray(Material material)
        {
            Color gray = new Color(0.66f, 0.66f, 0.66f, 1);
            if (material.HasProperty("_Color")) material.SetColor("_Color", gray);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", gray);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0);
        }
    }
}
#endif
