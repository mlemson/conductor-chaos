using System;
using System.IO;
using System.Reflection;
using ConductorChaos.Playground;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
internal static class PlaygroundAimWalkProbe
{
    static readonly string trigger = Path.Combine("Temp", "aim-walk.trigger");
    static readonly string result = Path.Combine("Temp", "aim-walk.result");
    static PlaygroundPlayer player;
    static Rigidbody body;
    static Transform arm, leg;
    static Vector3 start;
    static Quaternion restLeg;
    static float started, flatY, upY, maxLegAngle;
    static string flatDetails, upDetails;
    static int phase;
    static PlaygroundAimWalkProbe() => EditorApplication.update += Tick;
    static void Set(string field, object value) =>
        typeof(PlaygroundPlayer).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, value);
    static float HandUp() => arm.parent.InverseTransformDirection(arm.TransformDirection(Vector3.down)).normalized.y;
    static string Details()
    {
        var rb = arm.GetComponent<Rigidbody>();
        Collider own = arm.GetComponent<Collider>();
        Collider[] contacts = Physics.OverlapSphere(own.bounds.center, 0.25f);
        int count = contacts.Length;
        string names = "";
        for (int i = 0; i < count; i++)
            if (contacts[i] != own && Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                contacts[i], contacts[i].transform.position, contacts[i].transform.rotation, out _, out float depth))
                names += contacts[i].name + ":" + depth.ToString("F3") + ",";
        return $"held={typeof(PlaygroundPlayer).GetField("leftHeld", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player)} pitch={typeof(PlaygroundPlayer).GetField("requestedPitch", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player)} kinematic={rb.isKinematic} rot={arm.localEulerAngles} velocity={rb.angularVelocity} pos={arm.position} contacts={names}";
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !File.Exists(trigger)) return;
        try
        {
            if (phase == 0)
            {
                player = UnityEngine.Object.FindAnyObjectByType<PlaygroundPlayer>();
                if (!player || !player.IsSpawned) return;
                Set("automatedInput", true);
                body = player.GetComponent<Rigidbody>();
                var all = player.GetComponentsInChildren<Transform>();
                arm = Array.Find(all, t => t.name == "Arm_L");
                leg = Array.Find(all, t => t.name == "Leg_L");
                Vector3 offset = new Vector3(10, 1, 10) - body.position;
                foreach (Rigidbody part in player.GetComponentsInChildren<Rigidbody>()) part.position += offset;
                Set("requestedYaw", 0f);
                started = Time.time;
                phase = 1;
            }
            else if (phase == 1)
            {
                Set("lastServerInput", Time.unscaledTime);
                if (Time.time - started < 1f) return;
                started = Time.time; phase = 2;
            }
            else if (phase == 2)
            {
                Set("lastServerInput", Time.unscaledTime);
                Set("leftHeld", true); Set("requestedPitch", 0f);
                if (Time.time - started < 1f) return;
                flatY = HandUp(); flatDetails = Details(); started = Time.time; phase = 3;
            }
            else if (phase == 3)
            {
                Set("lastServerInput", Time.unscaledTime);
                Set("leftHeld", true); Set("requestedPitch", -55f);
                if (Time.time - started < 1f) return;
                upY = HandUp(); upDetails = Details(); Set("leftHeld", false); Set("requestedPitch", 0f);
                restLeg = leg.localRotation; start = body.position; started = Time.time; phase = 4;
            }
            else if (phase == 4)
            {
                Set("lastServerInput", Time.unscaledTime);
                Set("requestedMove", Vector2.up);
                maxLegAngle = Mathf.Max(maxLegAngle, Quaternion.Angle(restLeg, leg.localRotation));
                if (Time.time - started < 1.5f) return;
                File.WriteAllText(result, $"flatHandY={flatY:F3} raisedHandY={upY:F3} upwardDelta={upY-flatY:F3} move={Vector3.Distance(start,body.position):F2} legAngle={maxLegAngle:F1}\nflat {flatDetails}\nup {upDetails}");
                File.Delete(trigger);
            }
        }
        catch (Exception e) { File.WriteAllText(result, e.ToString()); File.Delete(trigger); }
    }
}
