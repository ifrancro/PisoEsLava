#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HeatRise.Characters;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeatRise.EditorTools
{
    public static class MaleCharacterIntegrationChecks
    {
        public const string Solo = "Assets/TowerRush/Scenes/HeatRise_Jugable.unity";
        public const string Lan = "Assets/TowerRush/Scenes/HeatRise_LAN.unity";
        public const string NetworkPrefab = "Assets/TowerRush/Prefabs/Jugador_LAN.prefab";

        [MenuItem("Heat Rise/Personajes/Validar integracion del personaje")]
        public static void ValidateInstallation()
        {
            var previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(Solo);
                var player = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PlayerController>(true)).Single();
                CheckPlayer(player, false);
                Require(UnityEngine.Object.FindObjectOfType<GameManager>().player == player, "Solo manager lost player reference.");
                Require(UnityEngine.Object.FindObjectOfType<OrbitCamera>().player == player, "Solo camera lost player reference.");
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefab);
                CheckPlayer(prefab.GetComponent<PlayerController>(), true);
                scene = EditorSceneManager.OpenScene(Lan);
                var manager = UnityEngine.Object.FindObjectOfType<Unity.Netcode.NetworkManager>();
                Require(manager.NetworkConfig.PlayerPrefab == prefab, "LAN uses a different player prefab.");
                Require(UnityEngine.Object.FindObjectsOfType<PlayerController>().Length == 0, "LAN has a duplicate scene player.");
                Debug.Log("CHARACTER_INSTALLATION PASS: solo + network prefab, capsule, visual rig, animation sync and camera references.");
            }
            finally { if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous); }
        }

        static void CheckPlayer(PlayerController player, bool online)
        {
            Require(player != null && player.model != null, "Player/model missing.");
            var view = player.model.GetComponentInChildren<MaleCharacterView>(true);
            Require(view != null, player.name + ": new male character is not installed.");
            Require(player.smallModel == null && player.largeModel == null, "One rig must handle all three sizes.");
            var animator = view.GetComponentInChildren<Animator>(true);
            Require(animator != null && animator.runtimeAnimatorController != null && !animator.applyRootMotion, "Animator setup invalid.");
            Require(player.model.localPosition.sqrMagnitude < .0001f && (player.model.localScale - Vector3.one).sqrMagnitude < .0001f,
                "Visual feet origin or initial scale changed.");
            var capsule = player.GetComponent<CharacterController>();
            Require(Mathf.Abs(capsule.height - 1.75f) < .001f && Mathf.Abs(capsule.radius - .29f) < .001f,
                "Gameplay capsule changed.");
            Require(player.model.GetComponentsInChildren<Renderer>(true).Length == 6, "Expected six visual meshes.");
            Require(player.model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Visual colliders affect gameplay.");
            Require(player.model.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == 2), "Visuals must stay out of camera rays.");
            if (online)
            {
                var sync = player.GetComponent<NetworkAnimator>();
                Require(sync != null && sync.Animator == animator, "Owner animation replication is not configured.");
            }
        }

        public static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException("CHARACTER_INTEGRATION FAIL: " + message);
        }
    }
}
#endif
