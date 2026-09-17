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
        public float startHeight = 2f;
        public float finishHeight = 55.76f;
        public bool showHud = true;

        public bool IsPlaying => Online ? NetworkRace.Instance.Racing : !paused && !finished;
        public bool MenuOpen => paused || HelpOpen;
        public bool HelpOpen { get; private set; }
        public float Elapsed { get; private set; }

        bool paused;
        bool finished;
        bool won;
        float bestHeight;
        string reason = "";
        string message = "";
        float messageUntil;
        GUIStyle label;
        GUIStyle title;
        GUIStyle small;
        GUIStyle value;
        GUIStyle centered;
        Vector2 helpScroll;
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
            helpScroll = Vector2.zero;
        }

        public void Restart()
        {
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
            Time.timeScale = 1f;
        }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                title = new GUIStyle(label) { fontSize = 23, fontStyle = FontStyle.Bold };
                small = new GUIStyle(label) { fontSize = 11, wordWrap = false };
                value = new GUIStyle(label) { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = false };
                centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            }
            if (HelpOpen)
            {
                DrawHelp();
                return;
            }
            if (Online && (!IsPlaying || paused)) return;
            if (!showHud && !paused && !finished) return;

            float width = Mathf.Min(430f, Screen.width - 32f);
            if (paused || finished)
            {
                Rect panel = new Rect((Screen.width - width) * 0.5f,
                    Mathf.Max(16f, (Screen.height - 316f) * 0.5f), width, Mathf.Min(316f, Screen.height - 32f));
                Panel(panel);
                GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 12f, width - 40f, panel.height - 24f));
                GUILayout.BeginHorizontal();
                GUILayout.Label(finished ? won ? "Llegaste a la cima" : "Fin del intento" : "Pausa", title);
                if (GUILayout.Button("?", GUILayout.Width(30f), GUILayout.Height(30f))) ShowHelp();
                GUILayout.EndHorizontal();
                GUILayout.Label(finished ? reason : "La lava y los obstaculos estan detenidos.", label);
                GUILayout.Label($"Tiempo: {Mathf.FloorToInt(Elapsed / 60f):00}:{Mathf.FloorToInt(Elapsed % 60f):00}", label);
                GUILayout.Label($"Altura maxima: {Mathf.Clamp(bestHeight - startHeight, 0f, finishHeight - startHeight):0.0} m", label);
                GUILayout.Space(12f);
                if (!finished && GUILayout.Button("Continuar", GUILayout.Height(32f))) SetPaused(false);
                if (GUILayout.Button("Reiniciar", GUILayout.Height(32f))) Restart();
                if (GUILayout.Button("Menu principal", GUILayout.Height(32f)))
                {
                    Time.timeScale = 1f;
                    SceneManager.LoadScene("HeatRise_LAN");
                }
                GUILayout.EndArea();
                return;
            }

            if (player == null) return;
            Rect safe = Screen.safeArea;
            float left = safe.xMin + 16f;
            float top = Screen.height - safe.yMax + 16f;
            float right = safe.xMax - 16f;
            float bottom = Screen.height - safe.yMin - 18f;
            float statWidth = Mathf.Min(100f, (safe.width - 112f) / 3f);
            Stat(new Rect(left, top, statWidth, 50f), "ALTURA",
                $"{Mathf.Max(0f, player.transform.position.y - startHeight):0.0} m", Mint);
            Stat(new Rect(left + statWidth + 6f, top, statWidth, 50f), "TIEMPO",
                $"{Mathf.FloorToInt(Elapsed / 60f):00}:{Mathf.FloorToInt(Elapsed % 60f):00}", Color.white);
            if (lava != null) Stat(new Rect(left + (statWidth + 6f) * 2f, top, statWidth, 50f), "LAVA A",
                $"{Mathf.Max(0f, player.transform.position.y - lava.SurfaceHeight):0.0} m", Orange);
            if (GUI.Button(new Rect(right - 60f, top, 60f, 28f), "Pausa")) SetPaused(true);
            if (safe.width >= 960f)
            {
                int sector = Mathf.Clamp(Mathf.FloorToInt(Mathf.InverseLerp(startHeight, finishHeight,
                    player.transform.position.y) * 6f), 0, 5);
                GUI.Label(new Rect(safe.center.x - 110f, top, 220f, 20f), $"SECTOR {sector + 1:00} / 06", centered);
                GUI.Label(new Rect(safe.center.x - 110f, top + 20f, 220f, 20f), SectorNames[sector], centered);
            }
            float abilityWidth = Mathf.Min(142f, (safe.width - 44f) / 3f);
            float abilitiesLeft = safe.center.x - (abilityWidth * 3f + 12f) * 0.5f;
            Ability(new Rect(abilitiesLeft, bottom - 48f, abilityWidth, 48f), PlayerController.Size.Small,
                "Q  PEQUEÑO", player.SmallCooldown, "Velocidad ×1.35", Cyan);
            Ability(new Rect(abilitiesLeft + abilityWidth + 6f, bottom - 48f, abilityWidth, 48f), PlayerController.Size.Normal,
                "R  NORMAL", 0f, "Salto equilibrado", Mint);
            Ability(new Rect(abilitiesLeft + (abilityWidth + 6f) * 2f, bottom - 48f, abilityWidth, 48f), PlayerController.Size.Large,
                "E  GIGANTE", player.LargeCooldown, "F: mover bloques", Orange);
            if (safe.width >= 1000f)
                GUI.Label(new Rect(left, bottom - 40f, 250f, 40f), "WASD: mover · Espacio: saltar\nC: siguiente salto · Esc/P: pausa", label);
            if (Online) DrawRaceMeter(new Rect(right - 88f, top + 82f, 88f,
                Mathf.Clamp(safe.height - 210f, 100f, 300f)));
            if (Time.unscaledTime < messageUntil)
            {
                float messageWidth = Mathf.Min(460f, safe.width - (Online ? 220f : 120f));
                Rect toast = new Rect(safe.center.x - messageWidth * 0.5f, top + 70f, messageWidth, 40f);
                Panel(toast);
                GUI.Label(new Rect(toast.x + 10f, toast.y + 4f, toast.width - 20f, toast.height - 8f), message, label);
            }
        }

        void Stat(Rect rect, string caption, string text, Color color)
        {
            Panel(rect);
            GUI.Label(new Rect(rect.x + 9f, rect.y + 4f, rect.width - 18f, 16f), caption, small);
            Color previous = GUI.contentColor;
            GUI.contentColor = color;
            GUI.Label(new Rect(rect.x + 9f, rect.y + 20f, rect.width - 18f, 26f), text, value);
            GUI.contentColor = previous;
        }

        void Ability(Rect rect, PlayerController.Size size, string caption, float cooldown, string hint, Color color)
        {
            Panel(rect);
            bool active = player.CurrentSize == size;
            Color previous = GUI.contentColor;
            GUI.contentColor = active ? color : Color.white;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 4f, rect.width - 20f, 20f), caption, small);
            GUI.contentColor = previous;
            string status = active && size != PlayerController.Size.Normal
                ? player.AbilityRemaining > 0f ? $"{player.AbilityRemaining:0.0} s restantes" : "Salí del conducto"
                : cooldown > 0f ? $"Recarga: {cooldown:0.0} s" : hint;
            GUI.Label(new Rect(rect.x + 10f, rect.y + 23f, rect.width - 20f, 19f), status, small);
            if (active) Fill(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), color);
        }

        void DrawRaceMeter(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y - 25f, rect.width, 20f), "META", centered);
            Panel(rect);
            for (int step = 1; step < 6; step++)
                Fill(new Rect(rect.x + 5f, rect.y + rect.height * step / 6f, rect.width - 10f, 1f),
                    new Color(0.75f, 0.9f, 0.86f, 0.25f));
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
            GUI.Label(new Rect(rect.x, rect.yMax + 4f, rect.width, 18f), "INICIO", centered);
        }

        void DrawHelp()
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.015f, 0.035f, 0.045f, 0.88f));
            float width = Mathf.Min(590f, Screen.width - 32f);
            float height = Mathf.Min(650f, Screen.height - 32f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            Panel(panel);
            GUILayout.BeginArea(new Rect(panel.x + 20f, panel.y + 16f, width - 40f, height - 32f));
            GUILayout.Label("Siempre hacia arriba", title);
            helpScroll = GUILayout.BeginScrollView(helpScroll);
            GUILayout.Label("Escalá los 6 sectores hasta la meta. La lava empieza a subir después de 8 segundos y acelera con el tiempo.", label);
            HelpSection("CONTROLES", "WASD / Flechas: moverse según la cámara.\nEspacio: saltar; un salto por pulsación.\nQ: pequeño, más rápido y capaz de cruzar conductos.\nE: gigante; F empuja bloques cercanos.\nR: volver al tamaño normal.\nF: guardar checkpoint sobre su botón verde.\nRatón: arrastrar con botón izquierdo, derecho o central para girar cámara.\nRueda: acercar o alejar.\nC: mirar hacia la siguiente plataforma.\nEsc / P: abrir pausa; Enter: continuar en modo solo.");
            HelpSection("TRANSFORMACIONES", "Pequeño y gigante duran 7 segundos. Recargan durante 4 segundos al terminar. Pequeño corre un 35 % más rápido; gigante camina más lento y resiste mejor los golpes. Si un techo u otro jugador impiden crecer, seguís pequeño hasta tener espacio.");
            HelpSection("COLORES Y OBSTÁCULOS", "Ámbar: plataformas frágiles; ceden 1,8 segundos después de pisarlas y reaparecen 5 segundos después.\nCian: plataformas móviles; calculá el salto y viajá sobre ellas.\nRojo: barras, martillos y prensas; evitá sus golpes.\nVioleta: bloques pesados; usá E y luego F para abrir paso.\nVerde: checkpoints y meta.");
            HelpSection("CHECKPOINTS Y CAÍDAS", "Hay 3 bases exteriores junto al recorrido. Parate sobre el botón verde y pulsá F para guardar. Cada jugador guarda su propio avance durante esa ronda. Caer por debajo de la plataforma desde la que saliste o tocar lava te devuelve al último checkpoint, si todavía está por encima de la lava. Sin un checkpoint seguro, termina tu intento.");
            HelpSection("CARRERA ONLINE · 2 A 4 JUGADORES", "Usen la misma versión del juego. En Internet, un jugador crea partida y comparte el código; los demás lo escriben y se unen, aunque estén en otras redes. El host debe mantener el juego abierto. Todos marcan Listo; el host inicia la carrera.\nLAN permite jugar en la misma red usando la IP local del host y puerto UDP 7777.\nCada jugador tiene un color: rosa, azul, verde o naranja. La barra derecha muestra J1–J4 según su altura, de INICIO a META; tu marcador lleva una raya blanca. Los eliminados quedan atenuados. El primero en llegar gana.\nAbrir pausa o ayuda no detiene la carrera online. Si el host sale, la partida se cierra para todos. No se puede entrar durante una carrera.");
            GUILayout.EndScrollView();
            GUILayout.Space(8f);
            if (GUILayout.Button("Entendido", GUILayout.Height(34f))) HelpOpen = false;
            GUILayout.EndArea();
        }

        void HelpSection(string heading, string text)
        {
            GUILayout.Space(14f);
            Color previous = GUI.contentColor;
            GUI.contentColor = Mint;
            GUILayout.Label(heading, label);
            GUI.contentColor = previous;
            GUILayout.Label(text, label);
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
            Time.timeScale = 1f;
        }
    }
}
