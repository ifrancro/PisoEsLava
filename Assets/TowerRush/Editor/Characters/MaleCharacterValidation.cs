#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HeatRise.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeatRise.EditorTools
{
    public static partial class MaleCharacterSetup
    {
        [Serializable]
        sealed class ValidationReport
        {
            public bool passed;
            public string unityVersion;
            public string prefab;
            public int skinnedMeshes;
            public int vertices;
            public int unweightedVertices;
            public int invalidWeights;
            public int nonfiniteVertices;
            public bool validGenericAvatar;
            public bool rootMotionDisabled;
            public bool hatAssigned;
            public float bindHeight;
            public float idleHeight;
            public List<ClipReport> clips = new List<ClipReport>();
            public List<string> errors = new List<string>();
        }

        [Serializable]
        sealed class ClipReport
        {
            public string name;
            public float duration;
            public bool looping;
            public int samples;
            public int movingBones;
            public float maxBonePositionDelta;
            public float maxBoneRotationDegrees;
            public float maxBoneScaleDelta;
            public float maxRootPositionDelta;
            public float maxRootRotationDegrees;
            public float maxRootScaleDelta;
        }

        struct LocalPose
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public LocalPose(Transform transform)
            {
                position = transform.localPosition;
                rotation = transform.localRotation;
                scale = transform.localScale;
            }
            public void Restore(Transform transform)
            {
                transform.localPosition = position;
                transform.localRotation = rotation;
                transform.localScale = scale;
            }
        }

        public static void BuildAndValidateAssets()
        {
            BuildAssets();
            ValidateAssets();
        }

        public static void DiagnoseScale()
        {
            var text = new StringBuilder();
            ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            text.AppendLine("Importer fileScale=" + importer.fileScale + " globalScale=" + importer.globalScale);
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), preview);
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                animator.enabled = false;
                foreach (Transform transform in instance.GetComponentsInChildren<Transform>(true))
                    text.AppendLine("TRANSFORM " + transform.name + " localPos=" + transform.localPosition.ToString("F5")
                        + " localScale=" + transform.localScale.ToString("F5") + " worldScale=" + transform.lossyScale.ToString("F5"));
                AnimationClip idle = null;
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                    if (asset is AnimationClip clip && clip.name == "Idle") idle = clip;
                foreach (string pose in new[] { "Bind", "Idle" })
                {
                    if (pose == "Idle" && idle != null) idle.SampleAnimation(animator.gameObject, 0f);
                    foreach (SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        text.AppendLine(pose + " SKIN " + skin.name + " rendererBounds=" + skin.bounds + " localBounds=" + skin.localBounds
                            + " meshBounds=" + skin.sharedMesh.bounds + " worldScale=" + skin.transform.lossyScale.ToString("F5"));
                        foreach (bool useScale in new[] { false, true })
                        {
                            var mesh = new Mesh();
                            try
                            {
                                skin.BakeMesh(mesh, useScale);
                                var transformed = new Bounds();
                                bool first = true;
                                foreach (Vector3 vertex in mesh.vertices)
                                {
                                    Vector3 point = skin.transform.TransformPoint(vertex);
                                    if (first) { transformed = new Bounds(point, Vector3.zero); first = false; }
                                    else transformed.Encapsulate(point);
                                }
                                text.AppendLine("  useScale=" + useScale + " bakedBounds=" + mesh.bounds + " transformedBounds=" + transformed);
                            }
                            finally { UnityEngine.Object.DestroyImmediate(mesh); }
                        }
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ArtSource/Characters/Male/unity_scale_diagnostic.txt");
            File.WriteAllText(path, text.ToString());
            Debug.Log(text.ToString());
        }

        // Batch entry: -executeMethod HeatRise.EditorTools.MaleCharacterSetup.ValidateAssets
        [MenuItem("Heat Rise/Personajes/Validar personaje masculino")]
        public static void ValidateAssets()
        {
            var report = new ValidationReport { unityVersion = Application.unityVersion, prefab = PrefabPath };
            Scene preview = default;
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Sal del modo Play antes de validar.");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (prefab == null) throw new InvalidOperationException("Ejecuta BuildAssets antes de ValidateAssets.");
                preview = EditorSceneManager.NewPreviewScene();
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null) throw new InvalidOperationException("El prefab no tiene Animator.");
                animator.enabled = false;
                report.validGenericAvatar = animator.avatar != null && animator.avatar.isValid && !animator.avatar.isHuman;
                report.rootMotionDisabled = !animator.applyRootMotion;
                Check(report.validGenericAvatar, "Avatar Generic invalido o ausente.", report);
                Check(report.rootMotionDisabled, "Root motion esta activado.", report);
                Check(animator.runtimeAnimatorController != null, "Falta AnimatorController.", report);

                MaleCharacterView view = instance.GetComponent<MaleCharacterView>();
                report.hatAssigned = view != null && new SerializedObject(view).FindProperty("hat").objectReferenceValue != null;
                Check(report.hatAssigned, "El componente visual no tiene Hat asignado.", report);
                SkinnedMeshRenderer[] skins = instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                report.skinnedMeshes = skins.Length;
                Check(skins.Length == 6, "Se esperaban 6 SkinnedMeshRenderer; encontrados " + skins.Length + ".", report);
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    foreach (Material material in renderer.sharedMaterials)
                        Check(material != null, renderer.name + ": material null.", report);
                var bones = new HashSet<Transform>();
                foreach (SkinnedMeshRenderer skin in skins) ValidateSkin(skin, bones, report);
                Check(report.unweightedVertices == 0, "Hay vertices sin peso.", report);
                Check(report.invalidWeights == 0, "Hay pesos o indices de huesos invalidos.", report);

                Transform rootBone = null;
                Transform[] transforms = instance.GetComponentsInChildren<Transform>(true);
                var bindPoses = new Dictionary<Transform, LocalPose>();
                foreach (Transform transform in transforms)
                {
                    bindPoses[transform] = new LocalPose(transform);
                    if (transform.name == "Root") rootBone = transform;
                }
                Check(rootBone != null, "No se encontro el hueso estacionario Root.", report);
                report.bindHeight = BakeBounds(skins, instance.transform, report).size.y;
                Check(report.bindHeight >= 1.65f && report.bindHeight <= 1.85f,
                    "Altura de bind fuera de 1.75 +/- 0.10 m: " + report.bindHeight, report);

                var clips = new Dictionary<string, AnimationClip>();
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                    if (asset is AnimationClip clip && Array.IndexOf(Clips, clip.name) >= 0) clips[clip.name] = clip;
                Check(clips.Count == 10, "Se esperaban los 10 clips importados.", report);
                foreach (string name in Clips)
                {
                    if (!clips.TryGetValue(name, out AnimationClip clip))
                    {
                        report.errors.Add("Falta el clip " + name + ".");
                        continue;
                    }
                    foreach (var pose in bindPoses) pose.Value.Restore(pose.Key);
                    SampleClip(clip, animator, rootBone, bones, skins, instance.transform, report);
                }
                Check(report.nonfiniteVertices == 0, "Se detectaron vertices NaN/Infinity al deformar la malla.", report);
                Check(report.idleHeight >= 1.65f && report.idleHeight <= 1.85f,
                    "Altura de Idle fuera de 1.75 +/- 0.10 m: " + report.idleHeight, report);
            }
            catch (Exception exception) { report.errors.Add(exception.GetType().Name + ": " + exception.Message); }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                report.passed = report.errors.Count == 0;
                string reportPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                    "ArtSource/Characters/Male/unity_validation.json");
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
            }
            if (!report.passed) throw new InvalidOperationException("MaleCharacter validation FAIL: " + string.Join("; ", report.errors));
            Debug.Log("MaleCharacter validation PASS: 6 skinned meshes, 10 clips, pesos, materiales, Generic avatar, Hat, altura y muestreo de animaciones. ArtSource/Characters/Male/unity_validation.json");
        }

        static void ValidateSkin(SkinnedMeshRenderer skin, HashSet<Transform> bones, ValidationReport report)
        {
            Mesh mesh = skin.sharedMesh;
            if (mesh == null) { report.errors.Add(skin.name + ": falta Mesh."); return; }
            report.vertices += mesh.vertexCount;
            Check(mesh.vertexCount > 0, skin.name + ": malla vacia.", report);
            Check(mesh.uv.Length == mesh.vertexCount, skin.name + ": UV0 ausentes o incompletas.", report);
            Material[] materials = skin.sharedMaterials;
            Check(materials.Length >= mesh.subMeshCount, skin.name + ": faltan slots de material.", report);
            foreach (Material material in materials) Check(material != null, skin.name + ": material null.", report);
            Transform[] skinBones = skin.bones;
            Check(skinBones.Length > 0, skin.name + ": no tiene huesos.", report);
            foreach (Transform bone in skinBones)
            {
                Check(bone != null, skin.name + ": referencia de hueso null.", report);
                if (bone != null) bones.Add(bone);
            }
            // Editor-side inspection: no runtime Read/Write setting is required.
            BoneWeight[] weights = mesh.boneWeights;
            Check(weights.Length == mesh.vertexCount, skin.name + ": cantidad de pesos distinta a vertices.", report);
            foreach (BoneWeight weight in weights)
            {
                float sum = weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3;
                if (sum <= 0.00001f) report.unweightedVertices++;
                if (!Finite(sum) || Mathf.Abs(sum - 1f) > 0.01f
                    || !ValidWeight(weight.weight0, weight.boneIndex0, skinBones.Length)
                    || !ValidWeight(weight.weight1, weight.boneIndex1, skinBones.Length)
                    || !ValidWeight(weight.weight2, weight.boneIndex2, skinBones.Length)
                    || !ValidWeight(weight.weight3, weight.boneIndex3, skinBones.Length)) report.invalidWeights++;
            }
        }

        static void SampleClip(AnimationClip clip, Animator animator, Transform rootBone, HashSet<Transform> bones,
            SkinnedMeshRenderer[] skins, Transform instance, ValidationReport report)
        {
            var result = new ClipReport { name = clip.name, duration = clip.length, looping = clip.isLooping, samples = 9 };
            report.clips.Add(result);
            Check(clip.length > 0.05f && !clip.empty, clip.name + ": clip vacio o sin duracion.", report);
            LocalPose modelRoot = new LocalPose(animator.transform);
            LocalPose skeletonRoot = rootBone != null ? new LocalPose(rootBone) : default;
            var firstBonePoses = new Dictionary<Transform, LocalPose>();
            var changedBones = new HashSet<Transform>();
            for (int sample = 0; sample < result.samples; sample++)
            {
                clip.SampleAnimation(animator.gameObject, clip.length * sample / (result.samples - 1));
                RecordRootDelta(animator.transform, modelRoot, result);
                if (rootBone != null) RecordRootDelta(rootBone, skeletonRoot, result);
                foreach (Transform bone in bones)
                {
                    if (sample == 0) { firstBonePoses[bone] = new LocalPose(bone); continue; }
                    LocalPose initial = firstBonePoses[bone];
                    float position = Vector3.Distance(initial.position, bone.localPosition);
                    float rotation = Quaternion.Angle(initial.rotation, bone.localRotation);
                    float scale = Vector3.Distance(initial.scale, bone.localScale);
                    result.maxBonePositionDelta = Mathf.Max(result.maxBonePositionDelta, position);
                    result.maxBoneRotationDegrees = Mathf.Max(result.maxBoneRotationDegrees, rotation);
                    result.maxBoneScaleDelta = Mathf.Max(result.maxBoneScaleDelta, scale);
                    if (position > 0.00001f || rotation > 0.01f || scale > 0.00001f) changedBones.Add(bone);
                }
                Bounds bounds = BakeBounds(skins, instance, report);
                if (clip.name == "Idle" && sample == 0) report.idleHeight = bounds.size.y;
            }
            result.movingBones = changedBones.Count;
            Check(result.movingBones > 0, clip.name + ": no se detecto movimiento de huesos al muestrear.", report);
            Check(result.maxRootPositionDelta < 0.001f && result.maxRootRotationDegrees < 0.05f && result.maxRootScaleDelta < 0.001f,
                clip.name + ": Root o el objeto del modelo se mueve/rota/escala.", report);
        }

        static void RecordRootDelta(Transform root, LocalPose initial, ClipReport result)
        {
            result.maxRootPositionDelta = Mathf.Max(result.maxRootPositionDelta, Vector3.Distance(initial.position, root.localPosition));
            result.maxRootRotationDegrees = Mathf.Max(result.maxRootRotationDegrees, Quaternion.Angle(initial.rotation, root.localRotation));
            result.maxRootScaleDelta = Mathf.Max(result.maxRootScaleDelta, Vector3.Distance(initial.scale, root.localScale));
        }

        static Bounds BakeBounds(SkinnedMeshRenderer[] skins, Transform root, ValidationReport report)
        {
            var mesh = new Mesh();
            var bounds = new Bounds();
            bool initialized = false;
            try
            {
                foreach (SkinnedMeshRenderer skin in skins)
                {
                    if (skin.sharedMesh == null) continue;
                    mesh.Clear();
                    // Match the renderer's scaled local space before TransformPoint.
                    // FBX centimetre compensation otherwise gets applied a second time.
                    skin.BakeMesh(mesh, true);
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Vector3 point = root.InverseTransformPoint(skin.transform.TransformPoint(vertex));
                        if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) { report.nonfiniteVertices++; continue; }
                        if (!initialized) { bounds = new Bounds(point, Vector3.zero); initialized = true; }
                        else bounds.Encapsulate(point);
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
            return bounds;
        }

        static bool ValidWeight(float weight, int index, int count) => Finite(weight) && weight >= 0f && (weight == 0f || index >= 0 && index < count);
        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static void Check(bool condition, string message, ValidationReport report) { if (!condition) report.errors.Add(message); }
    }
}
#endif
