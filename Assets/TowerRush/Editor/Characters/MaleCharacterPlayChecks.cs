#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HeatRise.EditorTools
{
    [InitializeOnLoad]
    public static class MaleCharacterPlayChecks
    {
        const string Key = "HR_CharacterPlayChecks";
        const string Output = "Logs/CharacterIntegration";
        static IEnumerator routine;
        static int lastFrame = -1;
        static Report report;
        static readonly MethodInfo Tick = typeof(PlayerController).GetMethod("SimulateMovement", BindingFlags.Instance | BindingFlags.NonPublic);
        static PlayerController player;
        static Animator animator;
        static GameObject floor;
        static readonly Vector3 Arena = new Vector3(120f, 90f, 120f);

        [Serializable] sealed class Report
        {
            public bool passed;
            public List<string> checks = new List<string>();
            public List<string> errors = new List<string>();
        }

        static MaleCharacterPlayChecks() { EditorApplication.update += Update; }

        public static void Start()
        {
            MaleCharacterIntegrationChecks.ValidateInstallation();
            EditorSceneManager.OpenScene(MaleCharacterIntegrationChecks.Solo);
            SessionState.SetInt(Key, 1);
            SessionState.SetFloat(Key + "Start", (float)EditorApplication.timeSinceStartup);
            EditorApplication.isPlaying = true;
        }

        static void Update()
        {
            int stage = SessionState.GetInt(Key, 0);
            if (stage == 0) return;
            if (stage == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                EditorApplication.Exit(SessionState.GetInt(Key + "Exit", 1));
                return;
            }
            if (EditorApplication.timeSinceStartup - SessionState.GetFloat(Key + "Start", 0f) > 120)
            {
                Finish(new TimeoutException("Play Mode did not complete within 120 seconds."));
                return;
            }
            EditorApplication.QueuePlayerLoopUpdate();
            if (!EditorApplication.isPlaying || EditorApplication.isPaused || lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            try
            {
                if (stage == 1)
                {
                    report = new Report();
                    Application.logMessageReceived += CaptureError;
                    routine = Run();
                    SessionState.SetInt(Key, 2);
                }
                if (stage <= 2 && !routine.MoveNext()) Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        static void CaptureError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                report.errors.Add(message + "\n" + stack);
        }

        static void Finish(Exception error)
        {
            if (SessionState.GetInt(Key, 0) == 3) return;
            if (report == null) report = new Report();
            if (error != null) report.errors.Add(error.ToString());
            report.passed = report.errors.Count == 0;
            Application.logMessageReceived -= CaptureError;
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/playmode.json", JsonUtility.ToJson(report, true));
            SessionState.SetInt(Key + "Exit", report.passed ? 0 : 1);
            SessionState.SetInt(Key, 3);
            Debug.Log("CHARACTER_PLAYMODE " + (report.passed ? "PASS" : "FAIL") + ": " + string.Join("; ", report.checks));
            Time.timeScale = 1f;
            Time.captureFramerate = 0;
            EditorApplication.isPlaying = false;
        }

        static IEnumerator Run()
        {
            yield return null;
            Time.captureFramerate = 60;
            GameManager.Instance.SetPaused(false);
            player = Object.FindObjectOfType<PlayerController>();
            animator = player.CharacterView.GetComponentInChildren<Animator>();
            player.enabled = false; // Drive the same movement step deterministically instead of keyboard input.
            Require(Tick != null, "Movement step missing.");
            Require(player.CharacterView != null, "New rig not initialized.");
            Vector3 spawn = player.transform.position;
            Capture("idle", spawn);

            floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "CharacterCheck_Floor";
            floor.transform.position = Arena - Vector3.up * .25f;
            floor.transform.localScale = new Vector3(10f, .5f, 10f);
            Reset(Arena + Vector3.up * .02f);
            Step(30, Vector3.zero);
            Require(player.IsGrounded && State("Idle"), "Standing player must be Idle.");
            Record("grounded Idle");

            Step(20, Vector3.right);
            Require(State("Run") && player.HorizontalSpeed > 4f, "Real horizontal movement must play Run.");
            Record("Run follows movement");

            var seen = new HashSet<string>();
            Step(1, Vector3.zero, 1);
            for (int i = 0; i < 105; i++)
            {
                Step(1, Vector3.zero);
                foreach (string state in new[] { "Jump", "Fall", "Land" }) if (State(state)) seen.Add(state);
            }
            Require(seen.SetEquals(new[] { "Jump", "Fall", "Land" }) && player.IsGrounded,
                "Jump must ascend, descend with Fall, and land. Seen: " + string.Join(",", seen));
            Record("Jump -> Fall -> Land");

            Reset(Arena + new Vector3(4.7f, .02f, 0));
            Step(15, Vector3.zero);
            Step(35, Vector3.right);
            Require(!player.IsGrounded && player.VerticalSpeed < 0 && State("Fall"), "Walking off an edge must play Fall without a jump.");
            Record("walk-off-edge Fall");

            Reset(Arena + Vector3.up * .02f);
            Step(25, Vector3.zero);
            Vector3 before = player.transform.position;
            for (int i = 0; i < 25; i++)
            {
                floor.transform.position += Vector3.right * .02f;
                Step(1, Vector3.zero);
            }
            Require(player.transform.position.x - before.x > .35f && player.HorizontalSpeed < .1f && State("Idle"),
                "Platform carry must not masquerade as running.");
            floor.transform.position = Arena - Vector3.up * .25f;
            Record("moving support carries Idle player");

            Reset(Arena + Vector3.up * .02f); Step(20, Vector3.zero);
            Require(player.ChangeSize(PlayerController.Size.Large), "Grow rejected in open space.");
            Step(10, Vector3.zero);
            Require(State("Grow") && Mathf.Abs(player.Height - 1.75f * 1.55f) < .001f
                && Mathf.Abs(player.model.localScale.y - 1.55f) < .001f, "Grow animation or physical scale incorrect.");
            Require(player.ChangeSize(PlayerController.Size.Normal), "Return to normal rejected.");
            Step(12, Vector3.zero); Require(State("ReturnNormal"), "Missing ReturnNormal.");
            Require(player.ChangeSize(PlayerController.Size.Small), "Shrink rejected.");
            Step(12, Vector3.zero);
            Require(State("Shrink") && Mathf.Abs(player.Height - 1.75f * .46f) < .001f
                && Mathf.Abs(player.model.localScale.y - .46f) < .001f, "Shrink animation or scale incorrect.");
            Record("Grow / ReturnNormal / Shrink with matching capsules");

            Reset(Arena + Vector3.up * .02f); Step(15, Vector3.zero);
            Require(player.ChangeSize(PlayerController.Size.Small), "Cannot set up ceiling check.");
            Step(70, Vector3.zero);
            var ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ceiling.transform.position = player.transform.position + Vector3.up * 1.15f;
            ceiling.transform.localScale = new Vector3(2f, .2f, 2f);
            Physics.SyncTransforms();
            Require(!player.ChangeSize(PlayerController.Size.Large) && player.CurrentSize == PlayerController.Size.Small,
                "A blocked growth attempt must retain Small.");
            Step(12, Vector3.zero); Require(!State("Grow"), "Blocked growth fired an animation.");
            Object.DestroyImmediate(ceiling);
            Record("blocked growth leaves scale and animation unchanged");

            Reset(Arena + Vector3.up * .02f); Step(15, Vector3.zero);
            player.Knockback(Vector3.right, 5f); Step(12, Vector3.zero);
            Require(State("Hit"), "Accepted impact must play Hit.");
            player.ResetState(Arena); player.enabled = false; animator.Update(.01f);
            Require(State("Idle") && player.CurrentSize == PlayerController.Size.Normal, "Reset must clear Hit and size.");
            Record("Hit and reset to Idle");

            CheckColors();
            Record("clothing recolors without changing skin or hair");
            player.RestartAnimation(); animator.Update(.01f);
            CheckColors(false);
            Record("clothing colors survive animation reset");
            var checkpoint = Object.FindObjectsOfType<Checkpoint>().OrderBy(c => c.order).First();
            player.SaveCheckpoint(checkpoint);
            Require(player.CheckpointOrder == checkpoint.order, "Checkpoint not saved.");
            player.ResetState(new Vector3(0, GameManager.Instance.lava.SurfaceHeight - .2f, 7.6f), false);
            player.enabled = false;
            Step(1, Vector3.zero);
            Require(Vector3.Distance(player.transform.position, checkpoint.respawnPoints[0].position) < .2f
                && player.CurrentSize == PlayerController.Size.Normal, "Lava did not respawn at the saved checkpoint.");
            Record("lava checkpoint respawn resets pose and size");

            Reset(spawn); Step(30, Vector3.zero);
            player.ChangeSize(PlayerController.Size.Large); animator.Update(.25f); yield return null; Capture("grow", spawn);
            player.ResetState(spawn); player.enabled = false;
            player.ChangeSize(PlayerController.Size.Small); animator.Update(.25f); yield return null; Capture("small", spawn);
            player.ResetState(spawn); player.enabled = false;
            player.CharacterView.SetLocomotion(0f, false, -4f); animator.Update(.25f); yield return null; Capture("fall", spawn);
            player.ResetState(spawn); player.enabled = false;
            GameManager.Instance.Win();
            for (int i = 0; i < 10; i++) yield return null;
            Require(Time.timeScale == 0f && State("Victory") && animator.updateMode == AnimatorUpdateMode.UnscaledTime,
                "Solo victory must play even while the course is frozen. timeScale=" + Time.timeScale
                + ", won=" + GameManager.Instance.Won + ", mode=" + animator.updateMode
                + ", state=" + animator.GetCurrentAnimatorStateInfo(0).shortNameHash
                + ", next=" + animator.GetNextAnimatorStateInfo(0).shortNameHash
                + ", transition=" + animator.IsInTransition(0)
                + ", victoryTrigger=" + animator.GetBool("Victory"));
            Transform head = player.model.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
            Quaternion old = head.rotation;
            for (int i = 0; i < 15; i++) yield return null;
            Require(Quaternion.Angle(old, head.rotation) > .1f, "Victory bones are frozen.");
            Capture("victory", spawn);
            Record("unscaled Victory while solo course is frozen");
            yield return null;
        }

        static void Reset(Vector3 position)
        {
            player.ResetState(position);
            player.enabled = false;
            animator.Update(.01f);
        }

        static void Step(int count, Vector3 direction, byte actions = 0)
        {
            for (int i = 0; i < count; i++)
            {
                Tick.Invoke(player, new object[] { 1f / 60f, direction, i == 0 ? actions : (byte)0 });
                animator.Update(1f / 60f);
            }
        }

        static bool State(string name) => animator.GetCurrentAnimatorStateInfo(0).IsName(name)
            || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(name);
        static void Require(bool value, string message) => MaleCharacterIntegrationChecks.Require(value, message);
        static void Record(string check) { report.checks.Add(check); Debug.Log("CHARACTER_CHECK PASS: " + check); }

        static void CheckColors(bool apply = true)
        {
            Color shirt = new Color(.2f, .6f, .9f), pants = new Color(.1f, .2f, .3f);
            if (apply) player.CharacterView.SetClothingColors(shirt, pants);
            var block = new MaterialPropertyBlock();
            int changed = 0;
            foreach (var skin in player.model.GetComponentsInChildren<Renderer>(true))
                for (int i = 0; i < skin.sharedMaterials.Length; i++)
                {
                    string material = skin.sharedMaterials[i].name;
                    block.Clear(); skin.GetPropertyBlock(block, i);
                    if (material.Contains("Clothing_Shirt") || material.Contains("Clothing_Pants"))
                    {
                        Color expected = material.Contains("Clothing_Shirt") ? shirt : pants;
                        Require(Vector4.Distance(block.GetColor("_Color"), expected) < .001f, "Clothing tint missing.");
                        changed++;
                    }
                    else Require(block.isEmpty, "Skin/hair/eyes received a clothing tint.");
                }
            Require(changed >= 2, "Shirt and pants slots not found.");
        }

        static void Capture(string name, Vector3 position)
        {
            Directory.CreateDirectory(Output);
            var camera = Camera.main;
            Vector3 oldPosition = camera.transform.position;
            Quaternion oldRotation = camera.transform.rotation;
            float oldFov = camera.fieldOfView;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var rt = RenderTexture.GetTemporary(960, 720, 24);
            var image = new Texture2D(960, 720, TextureFormat.RGB24, false);
            try
            {
                player.transform.rotation = Quaternion.identity;
                camera.transform.position = position + new Vector3(3.2f, 2.3f, 4.3f);
                camera.transform.LookAt(position + Vector3.up * 1.1f);
                camera.fieldOfView = 35;
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, 960, 720), 0, 0); image.Apply();
                File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
            }
            finally
            {
                camera.transform.SetPositionAndRotation(oldPosition, oldRotation); camera.fieldOfView = oldFov;
                camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
                RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image);
            }
        }
    }
}
#endif
