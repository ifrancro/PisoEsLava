using UnityEngine;

namespace HeatRise
{
    [DefaultExecutionOrder(-50)]
    public sealed class FragilePlatform : MonoBehaviour
    {
        public float delay = 1.8f;
        public float respawnDelay = 5f;
        public int NetworkIndex { get; set; }
        public bool Activated { get; private set; }
        public float RemainingSeconds => Activated && !broken ? Mathf.Max(0f, breakAt - Elapsed) : 0f;

        float Elapsed => GameManager.Instance != null ? GameManager.Instance.Elapsed : Time.time;
        float breakAt;
        bool broken;
        bool flashing;
        Collider[] colliders;
        Renderer[] renderers;
        MaterialPropertyBlock[] originalProperties;
        MaterialPropertyBlock[] warningProperties;
        Camera viewCamera;
        Vector3 labelAnchor;
        GUIStyle warningStyle;

        void Awake()
        {
            colliders = GetComponentsInChildren<Collider>(true);
            renderers = GetComponentsInChildren<Renderer>(true);
            originalProperties = new MaterialPropertyBlock[renderers.Length];
            warningProperties = new MaterialPropertyBlock[renderers.Length];
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            for (int i = 0; i < renderers.Length; i++)
            {
                originalProperties[i] = new MaterialPropertyBlock();
                warningProperties[i] = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(originalProperties[i]);
                renderers[i].GetPropertyBlock(warningProperties[i]);
                warningProperties[i].SetColor("_Color", new Color(1f, 0.9f, 0.3f));
                if (i == 0) bounds = renderers[i].bounds;
                else bounds.Encapsulate(renderers[i].bounds);
            }
            labelAnchor = transform.InverseTransformPoint(new Vector3(bounds.center.x, bounds.max.y + 0.45f, bounds.center.z));
        }

        void Start()
        {
            viewCamera = Camera.main;
        }

        public void Activate()
        {
            if (Activated || !GameManager.Playing) return;
            if (GameManager.Online) NetworkRace.Instance.ActivatePlatform(this);
            else BeginBreak(Elapsed + delay);
        }

        public void BeginBreak(float time)
        {
            Activated = true;
            breakAt = time;
            SetWarning(true);
        }

        public void ResetPlatform()
        {
            Activated = broken = false;
            SetWarning(false);
            SetVisible(true);
        }

        void Update()
        {
            if (!Activated) return;
            float time = Elapsed;
            if (time >= breakAt + respawnDelay)
            {
                ResetPlatform();
                return;
            }
            if (broken) return;
            if (time < breakAt)
            {
                SetWarning(Mathf.Sin((time - breakAt + delay) * 22f) >= 0f);
                return;
            }
            broken = true;
            SetWarning(false);
            SetVisible(false);
        }

        void SetWarning(bool value)
        {
            if (flashing == value) return;
            flashing = value;
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].SetPropertyBlock(value ? warningProperties[i] : originalProperties[i]);
        }

        void OnGUI()
        {
            GameManager game = GameManager.Instance;
            if (RemainingSeconds <= 0f || viewCamera == null || !GameManager.Playing
                || game != null && (game.MenuOpen || !game.showHud)) return;
            NetworkPlayer local = GameManager.Online ? NetworkRace.Instance.LocalPlayer : null;
            if (GameManager.Online && (local == null || !local.Alive.Value)) return;
            Vector3 point = viewCamera.WorldToViewportPoint(transform.TransformPoint(labelAnchor));
            if (point.z <= 0f || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f) return;
            Rect viewport = viewCamera.pixelRect;
            Rect safe = Screen.safeArea;
            float left = Mathf.Max(safe.xMin, viewport.xMin) + 8f;
            float right = Mathf.Min(safe.xMax, viewport.xMax) - 8f;
            float top = Screen.height - Mathf.Min(safe.yMax, viewport.yMax) + 8f;
            float bottom = Screen.height - Mathf.Max(safe.yMin, viewport.yMin) - 8f;
            const float width = 198f, height = 26f;
            if (right - left < width || bottom - top < height) return;
            Rect rect = new Rect(Mathf.Clamp(viewport.x + point.x * viewport.width - width * 0.5f, left, right - width),
                Mathf.Clamp(Screen.height - viewport.y - point.y * viewport.height - height, top, bottom - height), width, height);
            if (warningStyle == null)
            {
                warningStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter };
                warningStyle.normal.textColor = new Color(1f, 0.9f, 0.3f);
            }
            Color previous = GUI.color;
            GUI.color = new Color(0.12f, 0.06f, 0.02f, 0.94f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
            float seconds = Mathf.Ceil(Mathf.Min(delay, RemainingSeconds) * 10f) / 10f;
            GUI.Label(rect, $"DESAPARECE EN {seconds:0.0} s", warningStyle);
        }

        void SetVisible(bool value)
        {
            foreach (Collider part in colliders) part.enabled = value;
            foreach (Renderer part in renderers) part.enabled = value;
        }
    }
}
