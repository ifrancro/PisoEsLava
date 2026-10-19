#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace HeatRise.Characters.Development
{
    // Opt-in local integration test; absent from release builds and inert without this argument.
    public sealed class CharacterNetworkSmoke : MonoBehaviour
    {
        string role;
        string output;
        readonly HashSet<string> local = new HashSet<string>();
        readonly HashSet<string> remote = new HashSet<string>();
        readonly HashSet<byte> remoteForms = new HashSet<byte>();
        readonly List<string> errors = new List<string>();
        static readonly string[] Required = { "Idle", "Run", "Jump", "Fall", "Land", "Grow", "Shrink", "ReturnNormal", "Hit", "Victory" };
        bool finished;
        float deadline;

        [Serializable] sealed class Report
        {
            public bool passed;
            public string role;
            public string[] localStates, remoteStates;
            public byte[] remoteForms;
            public List<string> errors;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "--character-network-smoke");
            if (option < 0 || option + 2 >= args.Length) return;
            var host = new GameObject("CharacterNetworkSmoke");
            DontDestroyOnLoad(host);
            var test = host.AddComponent<CharacterNetworkSmoke>();
            test.role = args[option + 1];
            test.output = args[option + 2];
            test.deadline = Time.realtimeSinceStartup + 60f;
            Application.runInBackground = true;
            Application.logMessageReceived += test.LogError;
            test.StartCoroutine(test.Run());
        }

        void Update()
        {
            if (!finished && Time.realtimeSinceStartup > deadline) Finish("Timed out waiting for host/client race.");
        }

        IEnumerator Run()
        {
            yield return null;
            LanMenu menu = LanMenu.Instance;
            menu.useInternet = false;
            // Loopback only: no Relay, external network or user account is involved.
            menu.transport.SetConnectionData("127.0.0.1", 17879, "127.0.0.1");
            bool started = role == "host" ? menu.network.StartHost() : menu.network.StartClient();
            if (!started) { Finish("Network start failed."); yield break; }
            NetworkRace race = NetworkRace.Instance;
            while (race.LocalPlayer == null || !race.LocalPlayer.IsSpawned) yield return null;
            NetworkPlayer own = race.LocalPlayer;
            own.SetReadyRpc(true);
            while (!race.Racing)
            {
                if (role == "host" && race.AllReady) race.StartRace();
                yield return null;
            }

            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "NetworkSmokeFloor";
            floor.transform.position = new Vector3(100f, 79.75f, 100f);
            floor.transform.localScale = new Vector3(50f, .5f, 50f);
            PlayerController player = own.Player;
            player.enabled = false;
            Vector3 origin = new Vector3(100f + own.Slot.Value * 4f, 80.02f, 100f);
            player.ResetState(origin);
            player.enabled = false;
            own.GetComponent<OwnerNetworkTransform>().Teleport(origin, Quaternion.identity, Vector3.one);
            var tick = typeof(PlayerController).GetMethod("SimulateMovement", BindingFlags.Instance | BindingFlags.NonPublic);
            if (tick == null) { Finish("Movement entry missing."); yield break; }
            float startTime = Time.realtimeSinceStartup - race.Elapsed;
            bool jumped = false, grew = false, normal1 = false, shrank = false, normal2 = false, hit = false, reset = false, won = false;
            while (Time.realtimeSinceStartup - startTime < 13f)
            {
                float t = Time.realtimeSinceStartup - startTime;
                byte actions = 0;
                if (t >= 2f && !jumped) { jumped = true; actions = 1; }
                if (t >= 3.5f && !grew) { grew = true; if (!player.ChangeSize(PlayerController.Size.Large)) errors.Add("Grow rejected."); }
                if (t >= 4.9f && !normal1) { normal1 = true; player.ChangeSize(PlayerController.Size.Normal); }
                if (t >= 6f && !shrank) { shrank = true; if (!player.ChangeSize(PlayerController.Size.Small)) errors.Add("Shrink rejected."); }
                if (t >= 7.2f && !normal2) { normal2 = true; player.ChangeSize(PlayerController.Size.Normal); }
                if (t >= 8f && !hit) { hit = true; player.Knockback(Vector3.forward, 3f); }
                if (t >= 9f && !reset)
                {
                    reset = true;
                    player.ResetState(origin); player.enabled = false;
                    own.GetComponent<OwnerNetworkTransform>().Teleport(origin, Quaternion.identity, Vector3.one);
                }
                if (t >= 10f && !won) { won = true; own.RequestFinish(); }
                if (race.Racing && !own.Finished.Value)
                {
                    Vector3 direction = t >= 1f && t < 2f ? Vector3.right : Vector3.zero;
                    tick.Invoke(player, new object[] { Mathf.Min(Time.deltaTime, .05f), direction, actions });
                }
                foreach (NetworkPlayer actor in race.Players)
                {
                    if (actor == null || !actor.IsSpawned) continue;
                    Animator animator = actor.Player.CharacterView.GetComponentInChildren<Animator>(true);
                    var states = actor.IsOwner ? local : remote;
                    foreach (string state in Required)
                        if (animator.GetCurrentAnimatorStateInfo(0).IsName(state)
                            || animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(state)) states.Add(state);
                    if (!actor.IsOwner) remoteForms.Add(actor.Form.Value);
                }
                yield return null;
            }
            foreach (string state in Required)
            {
                if (!local.Contains(state)) errors.Add("Local state missing: " + state);
                if (!remote.Contains(state)) errors.Add("Remote state missing: " + state);
            }
            if (!remoteForms.IsSupersetOf(new byte[] { 0, 1, 2 })) errors.Add("Remote forms were not synchronized.");
            foreach (var actor in race.Players) CheckClothing(actor);
            Finish(null);
        }

        void CheckClothing(NetworkPlayer actor)
        {
            var block = new MaterialPropertyBlock();
            foreach (var renderer in actor.Player.model.GetComponentsInChildren<Renderer>(true))
                for (int i = 0; i < renderer.sharedMaterials.Length; i++)
                {
                    string material = renderer.sharedMaterials[i].name;
                    block.Clear(); renderer.GetPropertyBlock(block, i);
                    if (material.Contains("Clothing_Shirt"))
                    {
                        if (Vector4.Distance(block.GetColor("_Color"), actor.RaceColor) > .001f)
                            errors.Add("Race shirt color missing: " + actor.Label + ", actual=" + block.GetColor("_Color")
                                + ", expected=" + actor.RaceColor + ", empty=" + block.isEmpty
                                + ", shader=" + renderer.sharedMaterials[i].shader.name);
                    }
                    else if (!material.Contains("Clothing_Pants") && !block.isEmpty) errors.Add("Network recolored skin/hair/eyes.");
                }
        }

        void LogError(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(message);
        }

        void Finish(string error)
        {
            if (finished) return;
            finished = true;
            if (error != null) errors.Add(error);
            Application.logMessageReceived -= LogError;
            Directory.CreateDirectory(output);
            var report = new Report { passed = errors.Count == 0, role = role, localStates = local.OrderBy(s => s).ToArray(),
                remoteStates = remote.OrderBy(s => s).ToArray(), remoteForms = remoteForms.OrderBy(b => b).ToArray(), errors = errors };
            File.WriteAllText(Path.Combine(output, "network_" + role + ".json"), JsonUtility.ToJson(report, true));
            Debug.Log("CHARACTER_NETWORK " + role + " " + (report.passed ? "PASS" : "FAIL"));
            Application.Quit(report.passed ? 0 : 1);
        }
    }
}
#endif
