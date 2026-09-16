#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Netcode.Components;
using Unity.Netcode.Transports.UTP;

namespace HeatRise.EditorTools
{
    public static class HeatRiseGameplaySetup
    {
        public static void PrepareProject()
        {
            EditorSceneManager.OpenScene("Assets/TowerRush/Scenes/HeatRise_Modelos.unity");
            Prepare();
            PrepareLan();
            ConfigurePrefabs();
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Heat Rise/Preparar escena jugable")]
        static void Prepare()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            Transform root = FindRoot(scene);
            if (root != null && root.GetComponentInChildren<PlayerController>(true) != null)
            {
                Debug.Log("Esta escena ya tiene el jugador configurado.");
                return;
            }

            if (root == null || root.Find("04_Personaje_Normal") == null
                || root.Find("01_Recorrido") == null || root.Find("02_Obstaculos") == null
                || root.Find("03_Lava/Superficie_Lava") == null || root.Find("05_Portal_Meta/Zona_Meta") == null)
            {
                Debug.LogError("Abri primero HeatRise_Modelos.unity o la escena reconstruida con los modelos originales.");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/TowerRush/Scenes");
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/TowerRush/Scenes/HeatRise_Jugable.unity");
            if (!EditorSceneManager.SaveScene(scene, path, true)) return;
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            root = FindRoot(scene);

            Transform model = root.Find("04_Personaje_Normal");
            GameObject playerObject = new GameObject("Jugador");
            playerObject.transform.SetParent(root, false);
            playerObject.transform.position = model.position;
            playerObject.layer = 2;
            playerObject.tag = "Player";
            model.SetParent(playerObject.transform, true);
            foreach (Transform part in model.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            foreach (Collider collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;

            CharacterController controller = playerObject.AddComponent<CharacterController>();
            controller.height = 1.75f;
            controller.radius = 0.29f;
            controller.center = new Vector3(0f, 0.875f, 0f);
            controller.skinWidth = 0.025f;
            controller.stepOffset = 0.1f;
            controller.slopeLimit = 45f;
            controller.minMoveDistance = 0f;
            PlayerController player = playerObject.AddComponent<PlayerController>();
            player.model = model;
            model.localRotation = Quaternion.identity;
            player.smallModel = AddPlayerModel(playerObject.transform, "Personaje_Pequeno");
            player.largeModel = AddPlayerModel(playerObject.transform, "Personaje_Gigante");
            playerObject.AddComponent<PlayerCollision>();

            GameObject managerObject = new GameObject("Partida");
            managerObject.transform.SetParent(root, false);
            GameManager game = managerObject.AddComponent<GameManager>();
            game.player = player;
            game.startHeight = playerObject.transform.position.y;

            List<Transform> route = new List<Transform>();
            foreach (Transform sector in root.Find("01_Recorrido"))
            {
                foreach (Transform platform in sector)
                {
                    if (!platform.name.StartsWith("Plataforma_")) continue;
                    route.Add(platform);
                    ConfigurePlatform(platform);
                }
            }
            route.Sort((a, b) => a.position.y.CompareTo(b.position.y));
            if (route.Count > 0) game.finishHeight = route[route.Count - 1].position.y;
            CreateCheckpoints(root, route);

            foreach (Transform obstacle in root.Find("02_Obstaculos"))
                ConfigureObstacle(obstacle);

            Transform lava = root.Find("03_Lava/Superficie_Lava");
            game.lava = lava.gameObject.AddComponent<LavaRise>();
            lava.GetComponent<Collider>().isTrigger = true;
            AddBody(lava.gameObject);

            Transform finish = root.Find("05_Portal_Meta/Zona_Meta");
            finish.gameObject.AddComponent<FinishZone>();
            finish.GetComponent<BoxCollider>().isTrigger = true;
            AddBody(finish.gameObject);

            Camera camera = FindCamera(scene);
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                camera = cameraObject.AddComponent<Camera>();
            }
            camera.gameObject.tag = "MainCamera";
            if (camera.GetComponent<AudioListener>() == null) camera.gameObject.AddComponent<AudioListener>();
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 60f;
            player.view = camera.transform;
            OrbitCamera orbit = camera.gameObject.AddComponent<OrbitCamera>();
            orbit.player = player;
            orbit.route = route.ToArray();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Selection.activeGameObject = playerObject;
            Debug.Log("Escena jugable guardada en " + path + ". Pulsa Play. La escena de modelos se conserva.");
        }

        [MenuItem("Heat Rise/Corregir martillos en una copia")]
        static void RepairCopy()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            Transform root = FindRoot(scene);
            if (root == null)
            {
                Debug.LogError("Abri una escena de Heat Rise.");
                return;
            }
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/TowerRush/Scenes");
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/TowerRush/Scenes/HeatRise_Corregida.unity");
            if (!EditorSceneManager.SaveScene(scene, path, true)) return;
            scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            Repair(FindRoot(scene));
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Martillos corregidos en " + path + ". La escena anterior se conserva.");
        }

        [MenuItem("Heat Rise/Preparar multijugador LAN")]
        static void PrepareLan()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Scene scene = SceneManager.GetActiveScene();
            Transform root = FindRoot(scene);
            if (root == null)
            {
                Debug.LogError("Abri HeatRise_Modelos.unity o tu escena jugable de Heat Rise.");
                return;
            }
            if (root.GetComponentInChildren<NetworkRace>(true) != null)
            {
                Debug.Log("Esta escena ya tiene LAN configurada. Pulsa Play para crear o unirte a una partida.");
                return;
            }
            if (root.GetComponentInChildren<PlayerController>(true) == null)
            {
                Prepare();
                scene = SceneManager.GetActiveScene();
                root = FindRoot(scene);
            }
            PlayerController original = root != null ? root.GetComponentInChildren<PlayerController>(true) : null;
            GameManager game = root != null ? root.GetComponentInChildren<GameManager>(true) : null;
            if (original == null || game == null) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EnsureFolder("Assets/TowerRush/Scenes");
            EnsureFolder("Assets/TowerRush/Prefabs");
            string scenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/TowerRush/Scenes/HeatRise_LAN.unity");
            if (!EditorSceneManager.SaveScene(scene, scenePath, true)) return;
            scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            root = FindRoot(scene);
            Repair(root);
            original = root.GetComponentInChildren<PlayerController>(true);
            game = root.GetComponentInChildren<GameManager>(true);
            Vector3 start = original.transform.position;

            GameObject template = Object.Instantiate(original.gameObject);
            template.name = "Jugador_LAN";
            template.transform.SetParent(null);
            template.transform.position = Vector3.zero;
            template.transform.rotation = Quaternion.identity;
            PlayerController templatePlayer = template.GetComponent<PlayerController>();
            templatePlayer.view = null;
            templatePlayer.model.localRotation = Quaternion.identity;
            template.AddComponent<NetworkObject>();
            NetworkTransform sync = template.AddComponent<NetworkTransform>();
            sync.Interpolate = true;
            sync.UseUnreliableDeltas = true;
            sync.PositionThreshold = 0.01f;
            sync.RotAngleThreshold = 1f;
            sync.SyncScaleX = sync.SyncScaleY = sync.SyncScaleZ = false;
            template.AddComponent<NetworkPlayer>();
            string prefabPath = AssetDatabase.GenerateUniqueAssetPath("Assets/TowerRush/Prefabs/Jugador_LAN.prefab");
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, prefabPath);
            Object.DestroyImmediate(template);
            if (prefab == null) return;

            game.player = null;
            Camera camera = FindCamera(scene);
            if (camera != null)
            {
                OrbitCamera orbit = camera.GetComponent<OrbitCamera>();
                if (orbit != null) orbit.player = null;
            }
            Object.DestroyImmediate(original.gameObject);
            game.gameObject.AddComponent<NetworkObject>();
            NetworkRace race = game.gameObject.AddComponent<NetworkRace>();
            race.blocks = root.GetComponentsInChildren<HeavyBlock>(true);
            race.fragilePlatforms = root.GetComponentsInChildren<FragilePlatform>(true);
            race.spawnPoints = new Transform[4];
            GameObject starts = new GameObject("Salidas_LAN");
            starts.transform.SetParent(root, false);
            for (int i = 0; i < race.spawnPoints.Length; i++)
            {
                Transform point = new GameObject("Salida_" + (i + 1)).transform;
                point.SetParent(starts.transform, false);
                point.position = start + new Vector3(i % 2 == 0 ? -0.8f : 0.8f, 0f, i < 2 ? -0.8f : 0.8f);
                race.spawnPoints[i] = point;
            }

            GameObject networkObject = new GameObject("Red_LAN");
            NetworkManager network = networkObject.AddComponent<NetworkManager>();
            UnityTransport transport = networkObject.AddComponent<UnityTransport>();
            network.NetworkConfig.NetworkTransport = transport;
            network.NetworkConfig.PlayerPrefab = prefab;
            network.NetworkConfig.TickRate = 60;
            network.NetworkConfig.EnableSceneManagement = true;
            network.NetworkConfig.ConnectionApproval = true;
            GameObject menuObject = new GameObject("Menu_LAN");
            LanMenu menu = menuObject.AddComponent<LanMenu>();
            menu.network = network;
            menu.transport = transport;
            PlayerSettings.runInBackground = true;
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(scenePath, true)
            };
            foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
                if (entry.path != scenePath) scenes.Add(entry);
            EditorBuildSettings.scenes = scenes.ToArray();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = menuObject;
            Debug.Log("LAN configurada en " + scenePath + ". Crea un build y usa Crear partida o Unirse por IP.");
        }

        static Transform AddPlayerModel(Transform player, string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TowerRush/Prefabs/" + name + ".prefab");
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(player, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            foreach (Transform part in instance.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = 2;
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            instance.SetActive(false);
            return instance.transform;
        }

        static T Component<T>(GameObject go) where T : UnityEngine.Component
        {
            T value = go.GetComponent<T>();
            return value != null ? value : go.AddComponent<T>();
        }

        static void ConfigurePlatform(Transform platform)
        {
            Transform surface = platform.Find("Superficie");
            if (surface == null) return;
            if (platform.name.EndsWith("_Movil"))
            {
                Component<MovingPlatform>(surface.gameObject);
                AddBody(surface.gameObject);
            }
            else if (platform.name.EndsWith("_Fragil")) Component<FragilePlatform>(surface.gameObject);
        }

        static void ConfigureObstacle(Transform obstacle)
        {
            if (obstacle.name.StartsWith("Bloque"))
            {
                Transform block = obstacle.Find("Bloque_Desplazable");
                Component<HeavyBlock>(block.gameObject).destination = obstacle.Find("Destino_Desplazamiento");
                AddBody(block.gameObject);
            }
            else if (obstacle.name.StartsWith("Barra"))
            {
                Transform pivot = obstacle.Find("Pivot_Rotacion_Y");
                Component<RotatingObstacle>(pivot.gameObject).speed = obstacle.position.y > 46f ? 108f : 72f;
                AddHazard(pivot.Find("Barra").gameObject, true);
            }
            else if (obstacle.name.StartsWith("Martillo")) ConfigureHammer(obstacle);
            else if (obstacle.name.StartsWith("Prensa"))
            {
                foreach (string name in new[] { "Mitad_Izquierda", "Mitad_Derecha" })
                {
                    Transform half = obstacle.Find(name);
                    MovingPlatform movement = Component<MovingPlatform>(half.gameObject);
                    movement.offset = new Vector3(0f, 0f, name == "Mitad_Izquierda" ? -0.9f : 0.9f);
                    movement.speed = 1.5f;
                    AddBody(half.gameObject);
                    Component<KnockbackObstacle>(half.gameObject);
                }
            }
        }

        static void ConfigurePrefabs()
        {
            foreach (string name in new[] { "Plataforma_Movil", "Plataforma_Fragil", "Bloque", "Barra", "Martillo", "Prensa", "Portal_Meta" })
            {
                string path = "Assets/TowerRush/Prefabs/" + name + ".prefab";
                GameObject prefab = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (name.StartsWith("Plataforma")) ConfigurePlatform(prefab.transform);
                    else if (name == "Portal_Meta")
                    {
                        GameObject zone = prefab.transform.Find("Zona_Meta").gameObject;
                        Component<FinishZone>(zone);
                        zone.GetComponent<BoxCollider>().isTrigger = true;
                        AddBody(zone);
                    }
                    else ConfigureObstacle(prefab.transform);
                    PrefabUtility.SaveAsPrefabAsset(prefab, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(prefab); }
            }
        }

        static void CreateCheckpoints(Transform root, List<Transform> route)
        {
            Material gray = AssetDatabase.LoadAssetAtPath<Material>("Assets/TowerRush/Materials/Gris_Modelado.mat");
            const string materialPath = "Assets/TowerRush/Materials/Checkpoint_Verde.mat";
            Material green = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (green == null)
            {
                green = new Material(Shader.Find("Standard")) { name = "Checkpoint_Verde", color = new Color(0.15f, 0.9f, 0.65f) };
                green.EnableKeyword("_EMISSION");
                green.SetColor("_EmissionColor", new Color(0.05f, 0.45f, 0.25f));
                AssetDatabase.CreateAsset(green, materialPath);
            }
            GameObject template = new GameObject("Checkpoint_Base");
            Part(template.transform, "Plataforma_Exterior", PrimitiveType.Cube, new Vector3(0f, -0.22f, 0f), new Vector3(5f, 0.44f, 4.4f), gray, true);
            Part(template.transform, "Pasarela", PrimitiveType.Cube, new Vector3(0f, -0.22f, -2.5f), new Vector3(2.6f, 0.44f, 2f), gray, true);
            Part(template.transform, "Base_Pulsador", PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), new Vector3(1.8f, 0.02f, 1.8f), gray, false);
            Part(template.transform, "Boton_Guardar", PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(1.4f, 0.015f, 1.4f), green, false);
            for (int side = -1; side <= 1; side += 2)
                Part(template.transform, "Borde_Luminoso", PrimitiveType.Cube, new Vector3(side * 2.42f, 0.025f, 0f), new Vector3(0.1f, 0.05f, 4.3f), green, false);
            TextMesh label = new GameObject("Indicacion_F").AddComponent<TextMesh>();
            label.transform.SetParent(template.transform, false);
            label.transform.localPosition = new Vector3(0f, 1.4f, 1.7f);
            label.transform.localRotation = Quaternion.identity;
            label.text = "CHECKPOINT\nF - GUARDAR";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 48;
            label.characterSize = 0.07f;
            label.color = green.color;

            Checkpoint checkpoint = template.AddComponent<Checkpoint>();
            BoxCollider zone = template.GetComponent<BoxCollider>();
            zone.center = new Vector3(0f, 1.1f, 0f);
            zone.size = new Vector3(3.8f, 2.4f, 3.8f);
            zone.isTrigger = true;
            AddBody(template);
            for (int i = 0; i < 4; i++)
            {
                Transform point = new GameObject("Reaparicion_" + (i + 1)).transform;
                point.SetParent(template.transform, false);
                point.localPosition = new Vector3(i % 2 == 0 ? -1f : 1f, 0.04f, i < 2 ? -1.1f : 1.1f);
                point.localRotation = Quaternion.Euler(0f, 180f, 0f);
                checkpoint.respawnPoints[i] = point;
            }
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(template, "Assets/TowerRush/Prefabs/Checkpoint_Base.prefab");
            Object.DestroyImmediate(template);
            Transform checkpoints = new GameObject("08_Checkpoints").transform;
            checkpoints.SetParent(root, false);
            int order = 1;
            foreach (int index in new[] { 8, 24, 40 })
            {
                Transform platform = route[index];
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Checkpoint_" + order;
                instance.transform.SetParent(checkpoints, false);
                instance.transform.SetPositionAndRotation(platform.TransformPoint(new Vector3(0f, 0f, 5.1f)), platform.rotation);
                instance.GetComponent<Checkpoint>().order = order++;
            }
        }

        static void Part(Transform parent, string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, bool solid)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!solid) Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        static void Repair(Transform root)
        {
            Transform obstacles = root.Find("02_Obstaculos");
            if (obstacles != null)
                foreach (Transform obstacle in obstacles)
                    if (obstacle.name.StartsWith("Martillo_")) ConfigureHammer(obstacle);
            foreach (PlayerController player in root.GetComponentsInChildren<PlayerController>(true))
            {
                if (player.GetComponent<PlayerCollision>() == null) player.gameObject.AddComponent<PlayerCollision>();
                if (player.model != null) player.model.localRotation = Quaternion.identity;
            }
        }

        static void ConfigureHammer(Transform obstacle)
        {
            Transform pivot = obstacle.Find("Pivot_Balanceo_X");
            if (pivot == null) return;
            RotatingObstacle old = pivot.GetComponent<RotatingObstacle>();
            if (old != null) Object.DestroyImmediate(old);
            pivot.localPosition = new Vector3(0f, 4.2f, 0f);
            pivot.localRotation = Quaternion.identity;
            SetPart(pivot.Find("Brazo"), new Vector3(0f, -1.7f, 0f), new Vector3(0.18f, 3.4f, 0.18f));
            SetPart(pivot.Find("Cabeza"), new Vector3(0f, -3.4f, 0f), new Vector3(1.1f, 0.9f, 1.1f));
            SetPart(pivot.Find("Refuerzo"), new Vector3(0f, -3.4f, 0f), new Vector3(1.17f, 0.18f, 1.17f));
            foreach (Rigidbody body in pivot.GetComponentsInChildren<Rigidbody>(true))
                if (body.transform != pivot) Object.DestroyImmediate(body);
            AddBody(pivot.gameObject);
            KnockbackObstacle hit = pivot.GetComponent<KnockbackObstacle>();
            if (hit == null) hit = pivot.gameObject.AddComponent<KnockbackObstacle>();
            Transform head = pivot.Find("Cabeza");
            hit.motionPoint = head;
            if (head != null)
            {
                BoxCollider collider = head.GetComponent<BoxCollider>();
                if (collider == null) collider = head.gameObject.AddComponent<BoxCollider>();
                collider.center = Vector3.zero;
                collider.size = Vector3.one;
                collider.isTrigger = true;
                KnockbackObstacle oldHit = head.GetComponent<KnockbackObstacle>();
                if (oldHit != null) Object.DestroyImmediate(oldHit);
            }
            HammerSwing swing = pivot.GetComponent<HammerSwing>();
            if (swing == null) swing = pivot.gameObject.AddComponent<HammerSwing>();
            swing.angle = 24f;
            swing.angularFrequency = 1.8f;
            swing.phase = obstacle.name.EndsWith("38") ? 2f : 0f;
        }

        static void SetPart(Transform part, Vector3 position, Vector3 scale)
        {
            if (part == null) return;
            part.localPosition = position;
            part.localRotation = Quaternion.identity;
            part.localScale = scale;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int separator = path.LastIndexOf('/');
            string parent = path.Substring(0, separator);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(separator + 1));
        }

        static void AddBody(GameObject go)
        {
            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
        }

        static void AddHazard(GameObject go, bool trigger)
        {
            Component<KnockbackObstacle>(go);
            go.GetComponent<Collider>().isTrigger = trigger;
            AddBody(go);
        }

        static Transform FindRoot(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == "HEAT_RISE_MODELOS") return root.transform;
            return null;
        }

        static Camera FindCamera(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Camera camera = root.GetComponentInChildren<Camera>();
                if (camera != null) return camera;
            }
            return null;
        }
    }
}
#endif
