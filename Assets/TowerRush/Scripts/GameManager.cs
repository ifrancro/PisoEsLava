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
        public bool Paused => paused;
        public bool Finished => finished;
        public bool Won => won;
        public string Reason => reason;
        public float BestHeight => bestHeight;

        bool paused;
        bool finished;
        bool won;
        float bestHeight;
        string reason = "";
        string message = "";
        float messageUntil;
        GUIStyle label;
        GUIStyle small;
        GUIStyle value;
        GUIStyle centered;
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
            Time.timeScale = 1f;
        }

        void OnGUI()
        {
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 14, wordWrap = true };
                    small = new GUIStyle(label) { fontSize = 11, wordWrap = false };
                value = new GUIStyle(label) { fontSize = 19, fontStyle = FontStyle.Bold, wordWrap = false };
                centered = new GUIStyle(small) { alignment = TextAnchor.MiddleCenter };
            }
            if (HelpOpen) return;
            if (Online && (!IsPlaying || paused)) return;
            if (!showHud && !paused && !finished) return;

            if (paused || finished) return;

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
