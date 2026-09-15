#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HeatRise.Editor
{
    public static class HeatRisePreview
    {
        public static void Capture()
        {
            EditorSceneManager.OpenScene("Assets/TowerRush/Scenes/HeatRise_Jugable.unity");
            PlayerController player = Object.FindObjectOfType<PlayerController>();
            Checkpoint checkpoint = Object.FindObjectsOfType<Checkpoint>().OrderBy(c => c.order).First();
            OrbitCamera orbit = Object.FindObjectOfType<OrbitCamera>();
            Camera camera = new GameObject("Preview Camera", typeof(Camera)).GetComponent<Camera>();
            camera.gameObject.hideFlags = HideFlags.HideAndDontSave;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.075f, 0.09f, 0.11f);
            camera.nearClipPlane = 0.03f;
            camera.farClipPlane = 500f;
            camera.fieldOfView = 55f;
            camera.allowHDR = false;
            camera.enabled = false;
            player.SetModelVisible(true);
            Vector3 start = player.transform.position;
            Quaternion rotation = player.transform.rotation;
            Directory.CreateDirectory("Logs/Integration");
            try
            {
                Vector3 back = start - orbit.route[1].position;
                back.y = 0f;
                Render(camera, start + back.normalized * 9f + Vector3.up * 6f,
                    start + Vector3.up * 1.2f, "gameplay");
                Render(camera, new Vector3(62f, 46f, 62f), new Vector3(0f, 28f, 0f), "overview");
                player.transform.SetPositionAndRotation(checkpoint.respawnPoints[0].position,
                    checkpoint.transform.rotation * Quaternion.Euler(0f, 180f, 0f));
                Render(camera, checkpoint.transform.TransformPoint(new Vector3(9f, 7f, -8f)),
                    checkpoint.transform.TransformPoint(new Vector3(0f, 0.8f, -1.4f)), "checkpoint");
                Debug.Log("HEAT_RISE_PREVIEW PASS: Logs/Integration/{gameplay,overview,checkpoint}.png");
            }
            finally
            {
                player.transform.SetPositionAndRotation(start, rotation);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        static void Render(Camera camera, Vector3 position, Vector3 target, string name)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture output = RenderTexture.GetTemporary(1440, 1080, 24, RenderTextureFormat.ARGB32);
            Texture2D image = new Texture2D(1440, 1080, TextureFormat.RGB24, false);
            try
            {
                camera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
                camera.targetTexture = output;
                camera.Render();
                RenderTexture.active = output;
                image.ReadPixels(new Rect(0f, 0f, output.width, output.height), 0, 0);
                image.Apply();
                File.WriteAllBytes("Logs/Integration/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(output);
                Object.DestroyImmediate(image);
            }
        }
    }
}
#endif
