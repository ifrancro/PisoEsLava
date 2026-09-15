#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeatRise.Editor
{
    public static class HeatRiseValidation
    {
        const string Solo = "Assets/TowerRush/Scenes/HeatRise_Jugable.unity";
        const string Lan = "Assets/TowerRush/Scenes/HeatRise_LAN.unity";
        const string PlayerPrefab = "Assets/TowerRush/Prefabs/Jugador_LAN.prefab";

        [MenuItem("Heat Rise/Validar escenas y conexiones")]
        public static void ValidateScenes()
        {
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            Require(!EditorApplication.isPlayingOrWillChangePlaymode, "Validation requires Edit Mode.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Require(!SceneManager.GetSceneAt(i).isDirty, "Save open scenes before validation.");
            try
            {
                ValidateScene(Solo, false);
                ValidateScene(Lan, true);
                string[] enabled = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
                Require(enabled.Contains(Solo) && enabled.Contains(Lan), "Both game scenes must be enabled in Build Settings.");
                Require(enabled.Distinct().Count() == enabled.Length, "Duplicate scenes in Build Settings.");
                Require(PlayerSettings.runInBackground, "LAN player must run in background.");
                Debug.Log("HEAT_RISE_VALIDATION PASS: solo + LAN serialization, route, hazards, checkpoints, player prefab and build scenes.");
            }
            finally
            {
                if (previous.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        public static void BuildWindows()
        {
            ValidateScenes();
            string output = Path.GetFullPath("Builds/Validation/HeatRise.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Lan, Solo },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            Require(report.summary.result == BuildResult.Succeeded && File.Exists(output),
                "Windows build failed: " + report.summary.result + ", errors=" + report.summary.totalErrors);
            Debug.Log("HEAT_RISE_BUILD PASS: " + output + ", warnings=" + report.summary.totalWarnings);
        }

        static void ValidateScene(string path, bool online)
        {
            Require(File.Exists(path), "Missing scene: " + path);
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            GameObject[] roots = scene.GetRootGameObjects();
            foreach (GameObject rootObject in roots) ValidateReferences(rootObject);
            Transform root = roots.Single(o => o.name == "HEAT_RISE_MODELOS").transform;
            Transform routeRoot = root.Find("01_Recorrido");
            Require(routeRoot != null, path + ": route root missing.");
            Transform[] route = routeRoot.Cast<Transform>().SelectMany(sector => sector.Cast<Transform>())
                .Where(t => t.name.StartsWith("Plataforma_")).OrderBy(t => t.position.y).ToArray();
            Require(route.Length == 49, path + ": expected 49 route platforms, got " + route.Length);
            foreach (Transform platform in route)
            {
                Transform surface = platform.Find("Superficie");
                Require(surface != null && surface.GetComponentInChildren<Collider>() != null,
                    platform.name + ": missing solid surface.");
                Require(!surface.GetComponentInChildren<Collider>().isTrigger, platform.name + ": surface must be solid.");
                if (platform.name.EndsWith("_Movil"))
                    Require(surface.GetComponent<MovingPlatform>() != null, platform.name + ": missing MovingPlatform.");
                if (platform.name.EndsWith("_Fragil"))
                    Require(surface.GetComponent<FragilePlatform>() != null, platform.name + ": missing FragilePlatform.");
            }
            GameManager game = Single<GameManager>(roots);
            LavaRise lava = Single<LavaRise>(roots);
            FinishZone finish = Single<FinishZone>(roots);
            OrbitCamera camera = Single<OrbitCamera>(roots);
            Require(game.lava == lava, path + ": GameManager lava reference missing.");
            Require(camera.GetComponent<Camera>() != null && camera.CompareTag("MainCamera"), "Orbit camera must be MainCamera.");
            Require(camera.route != null && camera.route.SequenceEqual(route), "Camera route references/order differ from course.");
            Trigger(lava.gameObject);
            Trigger(finish.gameObject);

            HeavyBlock[] blocks = root.GetComponentsInChildren<HeavyBlock>(true);
            HammerSwing[] hammers = root.GetComponentsInChildren<HammerSwing>(true);
            RotatingObstacle[] bars = root.GetComponentsInChildren<RotatingObstacle>(true);
            Require(blocks.Length > 0 && hammers.Length > 0 && bars.Length > 0, "Required hazard groups missing.");
            foreach (HeavyBlock block in blocks)
                Require(block.destination != null && block.GetComponentInChildren<Collider>() != null,
                    block.name + ": missing block destination or collider.");
            foreach (HammerSwing hammer in hammers)
                Require(hammer.GetComponent<KnockbackObstacle>() != null
                    && hammer.GetComponent<KnockbackObstacle>().motionPoint != null,
                    hammer.name + ": hammer hit/motion connection missing.");
            foreach (RotatingObstacle bar in bars)
                Require(bar.GetComponentInChildren<KnockbackObstacle>(true) != null, bar.name + ": rotating hit missing.");

            Checkpoint[] checkpoints = root.GetComponentsInChildren<Checkpoint>(true).OrderBy(c => c.order).ToArray();
            Require(checkpoints.Length == 3, "Expected 3 checkpoints.");
            for (int i = 0; i < checkpoints.Length; i++)
            {
                Checkpoint checkpoint = checkpoints[i];
                Trigger(checkpoint.gameObject);
                Require(checkpoint.order == i + 1, "Checkpoint order must be 1, 2, 3.");
                Require(checkpoint.respawnPoints != null && checkpoint.respawnPoints.Length == 4
                    && checkpoint.respawnPoints.All(p => p != null), "Checkpoint requires four respawn points.");
                Require(checkpoint.respawnPoints.Distinct().Count() == 4, "Checkpoint respawn references must be distinct.");
                Physics.SyncTransforms();
                foreach (Transform point in checkpoint.respawnPoints)
                    Require(Physics.Raycast(point.position + Vector3.up * 0.1f, Vector3.down, 0.8f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore),
                        checkpoint.name + ": respawn point has no floor underneath: " + point.name);
            }

            if (online)
            {
                NetworkManager network = Single<NetworkManager>(roots);
                NetworkRace race = Single<NetworkRace>(roots);
                LanMenu menu = Single<LanMenu>(roots);
                Require(game.player == null && camera.player == null && root.GetComponentsInChildren<PlayerController>(true).Length == 0,
                    "LAN must spawn player prefab, with no scene player.");
                Require(network.NetworkConfig.PlayerPrefab == AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab), "Wrong LAN player prefab.");
                Require(network.NetworkConfig.NetworkTransport is UnityTransport && menu.network == network
                    && menu.transport == network.NetworkConfig.NetworkTransport, "LAN transport/menu references invalid.");
                Require(network.NetworkConfig.ConnectionApproval && network.NetworkConfig.EnableSceneManagement,
                    "Connection approval and scene management required.");
                Require(race.GetComponent<NetworkObject>() != null && race.spawnPoints.Length == 4
                    && race.spawnPoints.All(p => p != null), "Race network object/four starts missing.");
                Require(new HashSet<HeavyBlock>(race.blocks).SetEquals(blocks), "Network block registry differs from scene.");
                Require(new HashSet<FragilePlatform>(race.fragilePlatforms).SetEquals(root.GetComponentsInChildren<FragilePlatform>(true)),
                    "Network fragile registry differs from scene.");
                GameObject prefab = network.NetworkConfig.PlayerPrefab;
                ValidateReferences(prefab);
                ValidatePlayer(prefab.GetComponent<PlayerController>(), false);
                Require(prefab.GetComponent<NetworkPlayer>() != null && prefab.GetComponent<NetworkObject>() != null
                    && prefab.GetComponent<NetworkTransform>() != null, "Network player components missing.");
            }
            else
            {
                PlayerController player = Single<PlayerController>(roots);
                ValidatePlayer(player, true);
                Require(game.player == player && camera.player == player && player.view == camera.transform,
                    "Solo player/manager/camera references invalid.");
                Require(!All<NetworkRace>(roots).Any() && !All<NetworkManager>(roots).Any(), "Solo scene contains LAN components.");
            }
            Debug.Log("HEAT_RISE_SCENE PASS: " + path + " (49 platforms, 3 checkpoints)");
        }

        static void ValidatePlayer(PlayerController player, bool scenePlayer)
        {
            Require(player != null && player.GetComponent<CharacterController>() != null
                && player.GetComponent<PlayerCollision>() != null, "Player controller/collision missing.");
            Require(player.model != null && player.smallModel != null && player.largeModel != null, "All three player models required.");
            foreach (Transform model in new[] { player.model, player.smallModel, player.largeModel })
            {
                Require(model.IsChildOf(player.transform), "Model must belong to player.");
                Require(model.GetComponentsInChildren<Renderer>(true).Length > 0, "Player model has no renderer.");
                Require(model.GetComponentsInChildren<Collider>(true).All(c => !c.enabled), "Visual model colliders must be disabled.");
            }
            Require(player.model.gameObject.activeSelf && !player.smallModel.gameObject.activeSelf
                && !player.largeModel.gameObject.activeSelf, "Normal model must be initial visible form.");
            Require(!scenePlayer || player.view != null, "Solo view reference missing.");
        }

        static void Trigger(GameObject value)
        {
            Collider collider = value.GetComponent<Collider>();
            Rigidbody body = value.GetComponent<Rigidbody>();
            Require(collider != null && collider.isTrigger && body != null && body.isKinematic && !body.useGravity,
                value.name + ": trigger requires collider + kinematic rigidbody without gravity.");
        }

        static void ValidateReferences(GameObject root)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0,
                    child.name + ": missing script.");
                foreach (Component component in child.GetComponents<Component>())
                {
                    if (component == null) continue;
                    SerializedProperty property = new SerializedObject(component).GetIterator();
                    while (property.Next(true))
                        if (property.propertyType == SerializedPropertyType.ObjectReference)
                            Require(property.objectReferenceValue != null || property.objectReferenceInstanceIDValue == 0,
                                child.name + ": broken reference " + property.propertyPath);
                }
            }
        }

        static IEnumerable<T> All<T>(GameObject[] roots) where T : Component => roots.SelectMany(r => r.GetComponentsInChildren<T>(true));
        static T Single<T>(GameObject[] roots) where T : Component => All<T>(roots).Single();
        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("HEAT_RISE_VALIDATION FAIL: " + message);
        }
    }
}
#endif
