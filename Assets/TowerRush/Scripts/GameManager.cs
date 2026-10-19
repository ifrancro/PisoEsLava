using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeatRise
{
    [DefaultExecutionOrder(-100)]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }
        public static bool Playing => Instance == null || Instance.IsPlaying;
        public static bool Online => NetworkRace.Instance != null;
        public PlayerController player;
        public LavaRise lava;
        public Sprite controlsPanelSprite;
        public Sprite controlsBorderSprite;
        public Font controlsFont;
        public float startHeight = 2f;
        public float finishHeight = 55.76f;
        public bool showHud = true;
        public bool showPing = true;

        public bool IsPlaying => Online ? NetworkRace.Instance.Racing : !paused && !finished;
        public bool MenuOpen => paused || HelpOpen;
        public bool HelpOpen { get; private set; }
        public float Elapsed { get; private set; }
        public bool Paused => paused;
        public bool Finished => finished;
        public bool Won => won;
        public string Reason => reason;
        public float BestHeight => bestHeight;
        public float EruptionAt => Online ? NetworkRace.Instance.EruptionAt.Value : soloEruptionAt;
        public IReadOnlyList<Vector3> CrystalPositions => crystalPositions;
        public uint SoloCrystalMask => soloCrystalMask;

        bool paused;
        bool finished;
        bool won;
        float bestHeight;
        float soloEruptionAt = -1f;
        float eruptionUntil;
        uint soloCrystalMask;
        int soloCrystals;
        Material crystalMaterial;
        Material crownMaterial;
        Mesh crownMesh;
        GameObject crownObject;
        readonly List<Vector3> crystalPositions = new List<Vector3>(12);
        string reason = "";
        string message = "";
        float messageUntil;
        GUIStyle label;
        GUIStyle small;
        GUIStyle crystalStyle;
        GUIStyle sectorStyle;
        GUIStyle controlsStyle;
        GUIStyle controlsHeadingStyle;
        GUIStyle controlsPanelStyle;
        GUIStyle controlsBorderStyle;
        GUIStyle abilityTitleStyle;
        GUIStyle abilityHintStyle;
        GUIStyle abilityKeyStyle;
        GUIStyle rankValueStyle;
        GUIStyle leaderStyle;
        GUIStyle statValueStyle;
        GUIStyle spectatorDetailStyle;
        GUIStyle spectatorHintStyle;
        GUIStyle value;
        GUIStyle centered;
        GUIStyle eruptionStyle;
        GUIStyle spectatorStyle;
        GUIStyle toastStyle;
        GUIStyle checkpointStyle;
        static readonly Color Mint = new Color(0.72f, 0.98f, 0.51f);
        static readonly Color Cyan = new Color(0.47f, 0.89f, 0.94f);
        static readonly Color Orange = new Color(1f, 0.7f, 0.46f);
        static readonly string[] SectorNames = { "Primer ascenso", "Maquinaria en marcha", "Fuerza bruta",
            "A través del conducto", "Todo a la vez", "Último aliento" };

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            bestHeight = startHeight;
            Time.timeScale = 1f;
            PersistentMusic.Instance?.ReiniciarMusicaAmbiente();
        }

        void Start()
        {
            OrbitCamera orbit = Camera.main != null ? Camera.main.GetComponent<OrbitCamera>() : null;
            if (orbit == null || orbit.route == null || orbit.route.Length < 3) return;
            crystalMaterial = new Material(Shader.Find("Standard"));
            crystalMaterial.color = Cyan;
            crystalMaterial.EnableKeyword("_EMISSION");
            crystalMaterial.SetColor("_EmissionColor", Cyan * 0.5f);
            int count = Mathf.Min(12, orbit.route.Length - 2);
            for (int i = 0; i < count; i++)
            {
                Transform platform = orbit.route[(i + 1) * (orbit.route.Length - 1) / (count + 1)];
                if (platform == null) continue;
                Collider surface = platform.GetComponentInChildren<Collider>();
                Vector3 position = platform.position;
                position.y = (surface != null ? surface.bounds.max.y : platform.position.y) + 0.75f;
                GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
                crystal.name = "Cristal_" + i;
                crystal.layer = 2;
                crystal.transform.position = position;
                crystal.transform.localScale = Vector3.one * 0.5f;
                crystal.transform.SetParent(platform, true);
                crystal.transform.rotation = Quaternion.Euler(35f, 45f, 35f);
                crystal.GetComponent<Collider>().enabled = false;
                crystal.GetComponent<Renderer>().sharedMaterial = crystalMaterial;
                crystal.AddComponent<CrystalPickup>().index = crystalPositions.Count;
                crystalPositions.Add(position);
            }
        }

        void Update()
        {
            if (Online) Elapsed = NetworkRace.Instance.Elapsed;
            if (HelpOpen)
            {
                if (GameInput.Pause || GameInput.Confirm) HelpOpen = false;
                return;
            }
            if (Online)
            {
                if (GameInput.Pause) paused = !paused;
                if (player != null) bestHeight = Mathf.Max(bestHeight, player.transform.position.y);
                return;
            }
            if (finished)
            {
                if (GameInput.Confirm) Restart();
                return;
            }

            if (GameInput.Pause) SetPaused(!paused);
            if (paused)
            {
                if (GameInput.Confirm) SetPaused(false);
                return;
            }

            Elapsed += Time.deltaTime;
            if (player != null) bestHeight = Mathf.Max(bestHeight, player.transform.position.y);
            if (soloEruptionAt < 0f && player != null
                && player.transform.position.y >= Mathf.Lerp(startHeight, finishHeight, 5f / 6f))
            {
                soloEruptionAt = Elapsed;
                ShowEruption();
            }
        }

        public void ShowEruption()
        {
            eruptionUntil = Time.unscaledTime + 3f;
        }

        public void CollectCrystal(int index)
        {
            if (Online || index < 0 || index >= crystalPositions.Count) return;
            if ((soloCrystalMask & (1u << index)) != 0) return;
            soloCrystalMask |= 1u << index;
            soloCrystals++;
        }

        public void ShowMessage(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 3f;
        }

        public void Lose(string text)
        {
            if (Online) return;
            if (finished) return;
            finished = true;
            won = false;
            reason = text;
            Time.timeScale = 0f;
        }

        public void Win()
        {
            if (Online) return;
            if (finished) return;
            finished = true;
            won = true;
            bestHeight = finishHeight;
            reason = "Llegaste a la cima antes que la lava.";
            player?.Celebrate();
            PersistentMusic.Instance?.ReproducirVictoria();
            Time.timeScale = 0f;
        }

        public void SetPaused(bool value)
        {
            if (finished) return;
            if (!value) HelpOpen = false;
            paused = value;
            Time.timeScale = Online ? 1f : value ? 0f : 1f;
        }

        public void ShowHelp()
        {
            if (IsPlaying) SetPaused(true);
            HelpOpen = true;
        }

        public void CloseHelp()
        {
            HelpOpen = false;
        }

        public void Restart()
        {
            PersistentMusic.Instance?.ReiniciarMusicaAmbiente();
            if (Online)
            {
                NetworkRace.Instance.ReturnToLobby();
                return;
            }
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        void OnApplicationFocus(bool focused)
        {
            if (!focused && IsPlaying) SetPaused(true);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            if (crystalMaterial != null) Destroy(crystalMaterial);
            if (crownMaterial != null) Destroy(crownMaterial);
            if (crownMesh != null) Destroy(crownMesh);
            Time.timeScale = 1f;
        }

        void LateUpdate()
        {
            NetworkPlayer leader = Online && IsPlaying && !paused ? NetworkRace.Instance.Leader : null;
            if (leader == null)
            {
                if (crownObject != null) crownObject.SetActive(false);
                return;
            }

            if (crownObject == null) CreateLeaderCrown();
            crownObject.SetActive(true);
            crownObject.transform.position = leader.transform.position
                + Vector3.up * (leader.Player.Height + 0.22f);
            crownObject.transform.rotation = Quaternion.Euler(0f, Time.unscaledTime * 45f, 0f);
        }

        void CreateLeaderCrown()
        {
            const int segments = 12;
            Vector3[] vertices = new Vector3[segments * 3];
            int[] triangles = new int[segments * 12];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float x = Mathf.Cos(angle);
                float z = Mathf.Sin(angle);
                vertices[i] = new Vector3(x * 0.38f, 0f, z * 0.38f);
                vertices[segments + i] = new Vector3(x * 0.42f, 0.10f, z * 0.42f);
                vertices[segments * 2 + i] = new Vector3(x * 0.37f,
                    i % 2 == 0 ? 0.38f : 0.18f, z * 0.37f);
                int next = (i + 1) % segments;
                int index = i * 12;
                triangles[index] = i;
                triangles[index + 1] = segments + i;
                triangles[index + 2] = next;
                triangles[index + 3] = next;
                triangles[index + 4] = segments + i;
                triangles[index + 5] = segments + next;
                triangles[index + 6] = segments + i;
                triangles[index + 7] = segments * 2 + i;
                triangles[index + 8] = segments + next;
                triangles[index + 9] = segments + next;
                triangles[index + 10] = segments * 2 + i;
                triangles[index + 11] = segments * 2 + next;
            }

            crownMesh = new Mesh { name = "Leader Crown" };
            crownMesh.vertices = vertices;
            crownMesh.triangles = triangles;
            crownMesh.RecalculateNormals();
            crownObject = new GameObject("Leader Crown");
            crownObject.transform.SetParent(transform);
            crownObject.AddComponent<MeshFilter>().sharedMesh = crownMesh;
            MeshRenderer renderer = crownObject.AddComponent<MeshRenderer>();
            crownMaterial = new Material(Shader.Find("Standard"));
            crownMaterial.color = new Color32(0xff, 0xc7, 0x4b, 0xff);
            crownMaterial.SetFloat("_Metallic", 0.65f);
            crownMaterial.SetFloat("_Glossiness", 0.75f);
            crownMaterial.EnableKeyword("_EMISSION");
            crownMaterial.SetColor("_EmissionColor", new Color(0.28f, 0.11f, 0.01f));
            renderer.sharedMaterial = crownMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                small = new GUIStyle(label) { fontSize = 11, wordWrap = false };
                crystalStyle = new GUIStyle(label) { font = controlsFont, fontSize = 18,
                    fontStyle = FontStyle.Bold, wordWrap = false };
                crystalStyle.normal.textColor = Cyan;
                value = new GUIStyle(label) { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = false };
                sectorStyle = new GUIStyle(value) { alignment = TextAnchor.MiddleCenter, fontSize = 17 };
                controlsStyle = new GUIStyle(label) { font = controlsFont, fontSize = 15,
                    alignment = TextAnchor.MiddleLeft, wordWrap = false };
                controlsStyle.normal.textColor = new Color32(0xf2, 0xd9, 0xc8, 0xff);
                controlsHeadingStyle = new GUIStyle(controlsStyle) { fontSize = 12, fontStyle = FontStyle.Bold };
                controlsHeadingStyle.normal.textColor = new Color32(0xff, 0xcf, 0x9a, 0xff);
                abilityTitleStyle = new GUIStyle(controlsStyle) { fontSize = 16, fontStyle = FontStyle.Bold };
                abilityHintStyle = new GUIStyle(controlsStyle) { fontSize = 12 };
                abilityKeyStyle = new GUIStyle(controlsStyle) { fontSize = 20, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter };
                rankValueStyle = new GUIStyle(controlsStyle) { fontSize = 26, fontStyle = FontStyle.Bold };
                statValueStyle = new GUIStyle(controlsStyle) { fontSize = 21, fontStyle = FontStyle.Bold };
                statValueStyle.normal.textColor = Color.white;
                leaderStyle = new GUIStyle(controlsHeadingStyle) { fontSize = 12,
                    alignment = TextAnchor.MiddleCenter };
                if (controlsPanelSprite != null && controlsBorderSprite != null)
                {
                    controlsPanelStyle = new GUIStyle(GUI.skin.box);
                    controlsPanelStyle.normal.background = controlsPanelSprite.texture;
                    controlsPanelStyle.border = new RectOffset(15, 15, 15, 15);
                    controlsBorderStyle = new GUIStyle(GUI.skin.box);
                    controlsBorderStyle.normal.background = controlsBorderSprite.texture;
                    controlsBorderStyle.border = new RectOffset(15, 15, 15, 15);
                }
                centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
                eruptionStyle = new GUIStyle(value) { alignment = TextAnchor.MiddleCenter, fontSize = 22 };
                spectatorStyle = new GUIStyle(controlsStyle) { alignment = TextAnchor.MiddleCenter,
                    fontSize = 29, fontStyle = FontStyle.Bold };
                spectatorStyle.normal.textColor = new Color32(0xff, 0x8b, 0x73, 0xff);
                spectatorDetailStyle = new GUIStyle(controlsStyle) { alignment = TextAnchor.MiddleCenter,
                    fontSize = 15, fontStyle = FontStyle.Bold };
                spectatorHintStyle = new GUIStyle(controlsHeadingStyle) { alignment = TextAnchor.MiddleCenter,
                    fontSize = 12 };
                toastStyle = new GUIStyle(label) { alignment = TextAnchor.MiddleCenter, fontSize = 16,
                    fontStyle = FontStyle.Bold };
                toastStyle.normal.textColor = new Color32(0xf2, 0xd9, 0xc8, 0xff);
                checkpointStyle = new GUIStyle(toastStyle);
                checkpointStyle.normal.textColor = Mint;
            }
            if (HelpOpen) return;
            if (Online && (!IsPlaying || paused))
            {
                if (IsPlaying) DrawEruption();
                return;
            }
            if (Online && NetworkRace.Instance.LocalPlayer != null
                && !NetworkRace.Instance.LocalPlayer.Alive.Value)
            {
                DrawSpectator();
                return;
            }
            if (Online && NetworkRace.Instance.LocalPlayer != null
                && NetworkRace.Instance.LocalPlayer.Finished.Value)
            {
                Rect screen = Screen.safeArea;
                Rect banner = new Rect(screen.center.x - 175f, Screen.height - screen.yMax + 16f, 350f, 44f);
                HudCard(banner, new Color32(0x7a, 0x3a, 0x1a, 0xff));
                GUI.Label(banner, "META · ESPERANDO AL RESTO", spectatorDetailStyle);
                DrawEruption();
                return;
            }
            if (!showHud && !paused && !finished) return;

            if (paused || finished) return;

            if (player == null) return;
            Rect safe = Screen.safeArea;
            float left = safe.xMin + 16f;
            float top = Screen.height - safe.yMax + 16f;
            float right = safe.xMax - 16f;
            float bottom = Screen.height - safe.yMin - 18f;
            float statWidth = Mathf.Min(112f, (safe.width - 112f) / 3f);
            Stat(new Rect(left, top, statWidth, 62f), "ALTURA",
                $"{Mathf.Max(0f, player.transform.position.y - startHeight):0.0} m", Mint);
            Stat(new Rect(left + statWidth + 8f, top, statWidth, 62f), "TIEMPO",
                $"{Mathf.FloorToInt(Elapsed / 60f):00}:{Mathf.FloorToInt(Elapsed % 60f):00}", Color.white);
            if (lava != null) Stat(new Rect(left + (statWidth + 8f) * 2f, top, statWidth, 62f), "LAVA A",
                $"{Mathf.Max(0f, player.transform.position.y - lava.SurfaceHeight):0.0} m", Orange);
            int crystals = Online ? NetworkRace.Instance.LocalPlayer?.Crystals.Value ?? 0
                : soloCrystals;
            GUI.Label(new Rect(left + 2f, top + 76f, 220f, 28f),
                $"CRISTALES {crystals}/{crystalPositions.Count}", crystalStyle);
            if (Online && NetworkRace.Instance.LocalPlayer != null)
            {
                Rect rank = new Rect(right - 144f, top + 29f, 144f, 66f);
                HudCard(rank, new Color32(0xb5, 0x62, 0x29, 0xff));
                GUI.Label(new Rect(rank.x + 14f, rank.y + 8f, rank.width - 28f, 18f),
                    "PUESTO", controlsHeadingStyle);
                Color previous = GUI.contentColor;
                GUI.contentColor = new Color32(0xff, 0xd7, 0x6e, 0xff);
                GUI.Label(new Rect(rank.x + 14f, rank.y + 26f, rank.width - 28f, 34f),
                    $"{NetworkRace.Instance.PositionOf(NetworkRace.Instance.LocalPlayer)}.º / {NetworkRace.Instance.Players.Count}",
                    rankValueStyle);
                GUI.contentColor = previous;
            }
            if (Cursor.lockState != CursorLockMode.Locked
                && GUI.Button(new Rect(right - 60f, top, 60f, 28f), "Pausa"))
            {
                PersistentMusic.Instance?.ReproducirBoton();
                SetPaused(true);
            }
            if (showPing)
            {
                string ping = PingLabel();
                if (ping.Length > 0) GUI.Label(new Rect(right - 216f, top + 4f, 150f, 20f), ping, label);
            }
            if (safe.width >= 960f)
            {
                int sector = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(startHeight, finishHeight,
                    player.transform.position.y) * 6f), 0, 5);
                GUI.Label(new Rect(safe.center.x - 140f, top, 280f, 26f), $"SECTOR {sector + 1:00} / 06", sectorStyle);
                GUI.Label(new Rect(safe.center.x - 140f, top + 27f, 280f, 26f), SectorNames[sector], sectorStyle);
            }
            float abilityWidth = Mathf.Min(176f, (safe.width - 44f) / 3f);
            float abilitiesLeft = safe.center.x - (abilityWidth * 3f + 16f) * 0.5f;
            Ability(new Rect(abilitiesLeft, bottom - 68f, abilityWidth, 68f), PlayerController.Size.Small,
                "Q", "PEQUEÑO", player.SmallCooldown, "Velocidad ×1.35", Cyan);
            Ability(new Rect(abilitiesLeft + abilityWidth + 8f, bottom - 68f, abilityWidth, 68f), PlayerController.Size.Normal,
                "R", "NORMAL", 0f, "Salto equilibrado", Mint);
            Ability(new Rect(abilitiesLeft + (abilityWidth + 8f) * 2f, bottom - 68f, abilityWidth, 68f), PlayerController.Size.Large,
                "E", "GIGANTE", player.LargeCooldown, "F: mover bloques", Orange);
            if (safe.width >= 1140f)
            {
                Rect controls = new Rect(left, bottom - 88f, Mathf.Min(292f, abilitiesLeft - left - 8f), 88f);
                HudCard(controls, new Color32(0x7a, 0x3a, 0x1a, 0xff));
                GUI.Label(new Rect(controls.x + 14f, controls.y + 8f, controls.width - 28f, 17f),
                    "CONTROLES", controlsHeadingStyle);
                Fill(new Rect(controls.x + 14f, controls.y + 29f, controls.width - 28f, 1f),
                    new Color32(0x7a, 0x3a, 0x1a, 0x99));
                GUI.Label(new Rect(controls.x + 14f, controls.y + 34f, controls.width - 28f, 45f),
                    "WASD mover  ·  ESPACIO salto\nC orientar  ·  ESC/P pausa", controlsStyle);
            }
            if (Online) DrawRaceMeter(new Rect(right - 88f, top + 133f, 88f,
                Mathf.Clamp(safe.height - 263f, 100f, 247f)));
            if (Time.unscaledTime < messageUntil)
            {
                float messageWidth = Mathf.Min(460f, safe.width - 32f);
                Rect toast = new Rect(safe.center.x - messageWidth * 0.5f, top + 70f, messageWidth, 48f);
                Fill(toast, new Color32(0x7a, 0x3a, 0x1a, 0xff));
                Fill(new Rect(toast.x + 2f, toast.y + 2f, toast.width - 4f, toast.height - 4f),
                    new Color32(0x17, 0x0a, 0x08, 0xf0));
                bool checkpoint = message == "CHECKPOINT GUARDADO";
                if (checkpoint) Fill(new Rect(toast.x + 2f, toast.yMax - 4f, toast.width - 4f, 2f), Mint);
                GUI.Label(new Rect(toast.x + 12f, toast.y + 4f, toast.width - 24f, toast.height - 8f),
                    message, checkpoint ? checkpointStyle : toastStyle);
            }
            DrawEruption();
        }

        void DrawEruption()
        {
            if (Time.unscaledTime >= eruptionUntil) return;
            Rect safe = Screen.safeArea;
            Rect banner = new Rect(safe.center.x - 190f, Screen.height - safe.yMax + 120f, 380f, 70f);
            Panel(banner);
            Color previous = GUI.contentColor;
            GUI.contentColor = Orange;
            GUI.Label(banner, "¡ERUPCIÓN!  LA LAVA ACELERA", eruptionStyle);
            GUI.contentColor = previous;
        }

        void DrawSpectator()
        {
            OrbitCamera orbit = Camera.main != null ? Camera.main.GetComponent<OrbitCamera>() : null;
            NetworkRace race = NetworkRace.Instance;
            Rect safe = Screen.safeArea;
            float width = Mathf.Min(430f, safe.width - 32f);
            Rect banner = new Rect(safe.center.x - width * 0.5f,
                Screen.height - safe.yMax + 20f, width, 98f);
            HudCard(banner, new Color32(0xbd, 0x55, 0x32, 0xff));
            Fill(new Rect(banner.x + 18f, banner.y + 50f, banner.width - 36f, 1f),
                new Color32(0x7a, 0x3a, 0x1a, 0xff));
            GUI.Label(new Rect(banner.x + 16f, banner.y + 8f, banner.width - 32f, 38f),
                "ELIMINADO", spectatorStyle);
            string target = orbit != null && orbit.SpectatedPlayer != null
                ? $"OBSERVANDO A {orbit.SpectatedPlayer.Label.ToUpperInvariant()}  ·  {race.PositionOf(orbit.SpectatedPlayer)}.º / {race.Players.Count}"
                : "Esperando resultado";
            GUI.Label(new Rect(banner.x + 16f, banner.y + 53f, banner.width - 32f, 22f),
                target, spectatorDetailStyle);
            GUI.Label(new Rect(banner.x + 16f, banner.y + 76f, banner.width - 32f, 18f),
                "A / D  CAMBIAR JUGADOR     ·     ESC  MENÚ", spectatorHintStyle);
            DrawEruption();
        }

        static string PingLabel()
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null || !manager.IsClient || manager.IsServer) return "";
            UnityTransport transport = manager.NetworkConfig.NetworkTransport as UnityTransport;
            if (transport == null) return "";
            string mode = LanMenu.Instance != null && LanMenu.Instance.useInternet
                ? " · " + LanMenu.Instance.RelayConnection.ToUpperInvariant() : "";
            return "RTT " + transport.GetCurrentRtt(NetworkManager.ServerClientId) + " ms" + mode;
        }

        void Stat(Rect rect, string caption, string text, Color color)
        {
            HudCard(rect, new Color32(0x7a, 0x3a, 0x1a, 0xff));
            GUI.Label(new Rect(rect.x + 12f, rect.y + 8f, rect.width - 24f, 18f),
                caption, controlsHeadingStyle);
            Color previous = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(new Rect(rect.x + 12f, rect.y + 26f, rect.width - 24f, 30f),
                text, statValueStyle);
            GUI.contentColor = previous;
            Fill(new Rect(rect.x + 12f, rect.yMax - 5f, rect.width - 24f, 2f), color);
        }

        void Ability(Rect rect, PlayerController.Size size, string key, string caption, float cooldown, string hint, Color color)
        {
            bool active = player.CurrentSize == size;
            HudCard(rect, active ? color : new Color32(0x7a, 0x3a, 0x1a, 0xff));
            float detailX = rect.width < 150f ? 38f : 52f;
            abilityTitleStyle.fontSize = rect.width < 150f ? 13 : 16;
            abilityHintStyle.fontSize = rect.width < 150f ? 10 : 12;
            Color previous = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 10f, detailX - 13f, 37f), key, abilityKeyStyle);
            GUI.contentColor = active ? color : Color.white;
            GUI.Label(new Rect(rect.x + detailX, rect.y + 9f, rect.width - detailX - 8f, 25f),
                caption, abilityTitleStyle);
            GUI.contentColor = previous;
            string status = active && size != PlayerController.Size.Normal
                ? player.AbilityRemaining > 0f ? $"{player.AbilityRemaining:0.0} s restantes" : "Salí del conducto"
                : cooldown > 0f ? $"Recarga: {cooldown:0.0} s" : hint;
            GUI.Label(new Rect(rect.x + detailX, rect.y + 35f, rect.width - detailX - 8f, 22f),
                status, abilityHintStyle);
            Fill(new Rect(rect.x + detailX - 7f, rect.y + 13f, 1f, 42f),
                new Color32(0x7a, 0x3a, 0x1a, 0xaa));
            if (active) Fill(new Rect(rect.x + 12f, rect.yMax - 5f, rect.width - 24f, 3f), color);
        }

        void DrawRaceMeter(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y - 25f, rect.width, 20f), "META", leaderStyle);
            HudCard(rect, new Color32(0x7a, 0x3a, 0x1a, 0xff));
            for (int step = 1; step < 6; step++)
                Fill(new Rect(rect.x + 5f, rect.y + rect.height * step / 6f, rect.width - 10f, 1f),
                    new Color32(0xff, 0xcf, 0x9a, 0x55));
            if (lava != null)
            {
                float lavaY = Mathf.Lerp(rect.yMax - 10f, rect.y + 10f,
                    Mathf.InverseLerp(startHeight, finishHeight, lava.SurfaceHeight));
                Fill(new Rect(rect.x + 3f, lavaY, rect.width - 6f, 2f), Orange);
            }
            foreach (NetworkPlayer runner in NetworkRace.Instance.Players)
            {
                if (runner == null || !runner.IsSpawned) continue;
                float y = Mathf.Lerp(rect.yMax - 10f, rect.y + 10f,
                    Mathf.InverseLerp(startHeight, finishHeight, runner.transform.position.y));
                Rect marker = new Rect(rect.x + 4f + Mathf.Clamp(runner.Slot.Value, 0, 3) * 20f, y - 9f, 20f, 18f);
                Color color = runner.RaceColor;
                if (!runner.Alive.Value) color.a = 0.35f;
                Fill(marker, color);
                Color previous = GUI.contentColor;
                GUI.contentColor = new Color(0.02f, 0.04f, 0.05f);
                GUI.Label(marker, "J" + (runner.Slot.Value + 1), centered);
                GUI.contentColor = previous;
                if (runner.IsOwner) Fill(new Rect(marker.x + 2f, marker.yMax - 2f, marker.width - 4f, 2f), Color.white);
            }
            GUI.Label(new Rect(rect.x, rect.yMax + 4f, rect.width, 18f), "INICIO", leaderStyle);
        }

        void HudCard(Rect rect, Color border)
        {
            if (controlsPanelStyle == null)
            {
                Fill(rect, border);
                Fill(new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.height - 4f),
                    new Color32(0x17, 0x0a, 0x08, 0xee));
                return;
            }
            Color previous = GUI.color;
            GUI.color = new Color32(0x0a, 0x05, 0x04, 0x88);
            GUI.Box(new Rect(rect.x, rect.y + 3f, rect.width, rect.height), GUIContent.none, controlsPanelStyle);
            GUI.color = new Color32(0x17, 0x0a, 0x08, 0xee);
            GUI.Box(rect, GUIContent.none, controlsPanelStyle);
            GUI.color = border;
            GUI.Box(rect, GUIContent.none, controlsBorderStyle);
            GUI.color = previous;
        }

        static void Panel(Rect rect)
        {
            Fill(rect, new Color(0.035f, 0.08f, 0.095f, 0.88f));
        }

        static void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        public void ResetNetworkView()
        {
            paused = finished = won = false;
            HelpOpen = false;
            Elapsed = 0f;
            bestHeight = startHeight;
            reason = message = "";
            messageUntil = 0f;
            eruptionUntil = 0f;
            soloEruptionAt = -1f;
            soloCrystalMask = 0;
            soloCrystals = 0;
            Time.timeScale = 1f;
        }
    }
}
