#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using HeatRise.Characters;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeatRise.EditorTools
{
    /// <summary>Explicit opt-in asset preparation; never replaces a scene player.</summary>
    public static partial class MaleCharacterSetup
    {
        const string ModelPath = "Assets/TowerRush/Models/MaleCharacter/MaleCharacter.fbx";
        const string OutputFolder = "Assets/TowerRush/Models/MaleCharacter/Unity";
        const string ControllerPath = OutputFolder + "/MaleCharacter.controller";
        const string PrefabPath = OutputFolder + "/MaleCharacter.prefab";
        static readonly string[] Clips = { "Idle", "Run", "Jump", "Fall", "Land", "Grow", "Shrink", "ReturnNormal", "Hit", "Victory" };
        static readonly string[] Triggers = { "Jump", "Land", "Grow", "Shrink", "ReturnNormal", "Hit", "Victory" };

        // Batch entry: -executeMethod HeatRise.EditorTools.MaleCharacterSetup.BuildAssets
        [MenuItem("Heat Rise/Personajes/Preparar personaje masculino")]
        public static void BuildAssets()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Sal del modo Play antes de preparar el personaje.");
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("No se encontro " + ModelPath);

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.optimizeGameObjects = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
            var configured = new List<ModelImporterClipAnimation>();
            var found = new HashSet<string>();
            foreach (ModelImporterClipAnimation take in takes)
            {
                string name = NormalizeName(take.name);
                if (Array.IndexOf(Clips, name) < 0 || !found.Add(name)) continue;
                take.name = name;
                take.loopTime = name == "Idle" || name == "Run" || name == "Fall" || name == "Victory";
                take.loopPose = take.loopTime;
                take.lockRootRotation = true;
                take.lockRootHeightY = true;
                take.lockRootPositionXZ = true;
                configured.Add(take);
            }
            foreach (string name in Clips)
                if (!found.Contains(name)) throw new InvalidOperationException("Falta la accion FBX: " + name);
            importer.clipAnimations = configured.ToArray();
            importer.SaveAndReimport();

            var animations = new Dictionary<string, AnimationClip>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
                if (asset is AnimationClip clip && Array.IndexOf(Clips, clip.name) >= 0)
                    animations[clip.name] = clip;
            foreach (string name in Clips)
                if (!animations.ContainsKey(name)) throw new InvalidOperationException("No se pudo importar el clip: " + name);

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/TowerRush/Models/MaleCharacter", "Unity");
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            // Keep subsequent user changes to the generated controller and prefab.
            if (controller == null) controller = CreateController(animations);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) prefab = CreatePrefab(controller);
            AssetDatabase.SaveAssets();
            if (!Application.isBatchMode)
            {
                Selection.activeObject = prefab;
                EditorGUIUtility.PingObject(prefab);
            }
            Debug.Log("Personaje preparado: " + PrefabPath + ". Las escenas existentes no se modificaron.");
        }

        static string NormalizeName(string name)
        {
            foreach (string expected in Clips)
                if (name.Equals(expected, StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith("|" + expected, StringComparison.OrdinalIgnoreCase)
                    || name.EndsWith("_" + expected, StringComparison.OrdinalIgnoreCase)) return expected;
            return name;
        }

        static AnimatorController CreateController(Dictionary<string, AnimationClip> clips)
        {
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter(new AnimatorControllerParameter
                { name = "Grounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
            foreach (string trigger in Triggers) controller.AddParameter(trigger, AnimatorControllerParameterType.Trigger);

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            var states = new Dictionary<string, AnimatorState>();
            for (int i = 0; i < Clips.Length; i++)
            {
                string name = Clips[i];
                AnimatorState state = machine.AddState(name, new Vector3(260 + (i % 3) * 230, 70 + (i / 3) * 100));
                state.motion = clips[name];
                states[name] = state;
            }
            machine.defaultState = states["Idle"];
            Transition(states["Idle"], states["Run"]).AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            Transition(states["Run"], states["Idle"]).AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
            foreach (string name in new[] { "Idle", "Run", "Jump" })
            {
                AnimatorStateTransition fall = Transition(states[name], states["Fall"]);
                fall.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                fall.AddCondition(AnimatorConditionMode.Less, 0.01f, "VerticalSpeed");
            }
            // Covers spawning airborne and visual changes that begin after the jump event.
            foreach (string name in new[] { "Idle", "Run" })
            {
                AnimatorStateTransition jump = Transition(states[name], states["Jump"]);
                jump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                jump.AddCondition(AnimatorConditionMode.Greater, 0f, "VerticalSpeed");
            }
            foreach (string trigger in Triggers)
            {
                AnimatorStateTransition transition = machine.AddAnyStateTransition(states[trigger]);
                transition.hasExitTime = false;
                transition.duration = 0.08f;
                transition.hasFixedDuration = true;
                transition.canTransitionToSelf = false;
                transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            }
            foreach (string name in new[] { "Jump", "Fall" })
            {
                AnimatorStateTransition land = Transition(states[name], states["Land"]);
                land.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
            }
            foreach (string name in new[] { "Land", "Grow", "Shrink", "ReturnNormal", "Hit" })
            {
                AnimatorStateTransition idle = Transition(states[name], states["Idle"], true);
                idle.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
                idle.AddCondition(AnimatorConditionMode.IfNot, 0f, "Moving");
                AnimatorStateTransition run = Transition(states[name], states["Run"], true);
                run.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
                run.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
                AnimatorStateTransition jump = Transition(states[name], states["Jump"], true);
                jump.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
                jump.AddCondition(AnimatorConditionMode.Greater, 0f, "VerticalSpeed");
                // Evaluated after the ascending branch; includes vertical speed exactly zero.
                AnimatorStateTransition fall = Transition(states[name], states["Fall"], true);
                fall.AddCondition(AnimatorConditionMode.IfNot, 0f, "Grounded");
            }
            EditorUtility.SetDirty(controller);
            return controller;
        }

        static AnimatorStateTransition Transition(AnimatorState from, AnimatorState to, bool wait = false)
        {
            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = wait;
            transition.exitTime = 0.9f;
            transition.hasFixedDuration = true;
            transition.duration = 0.08f;
            return transition;
        }

        static GameObject CreatePrefab(AnimatorController controller)
        {
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("MaleCharacter");
                SceneManager.MoveGameObjectToScene(root, preview);
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(source, preview);
                model.transform.SetParent(root.transform, false);
                Animator animator = model.GetComponent<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                MaleCharacterView view = root.AddComponent<MaleCharacterView>();
                var settings = new SerializedObject(view);
                settings.FindProperty("animator").objectReferenceValue = animator;
                foreach (Transform part in model.GetComponentsInChildren<Transform>(true))
                    if (part.name.Equals("Hat", StringComparison.OrdinalIgnoreCase)
                        || part.name.Equals("Cosmetic_Hat", StringComparison.OrdinalIgnoreCase))
                    {
                        settings.FindProperty("hat").objectReferenceValue = part.gameObject;
                        break;
                    }
                settings.ApplyModifiedPropertiesWithoutUndo();
                return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { EditorSceneManager.ClosePreviewScene(preview); }
        }
    }
}
#endif
