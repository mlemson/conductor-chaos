using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using ConductorChaos.Playground;
using ConductorChaos.Stations;
using ConductorChaos.Track;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

internal sealed class CountryRunSmoke : MonoBehaviour
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        var args = Environment.GetCommandLineArgs();
        bool server = args.Contains("-country-run-host");
        if (!server && !args.Contains("-country-run-client")) return;
        var host = FindAnyObjectByType<PlaygroundHost>();
        host.enabled = false;
        var probe = new GameObject("Country Run smoke test").AddComponent<CountryRunSmoke>();
        DontDestroyOnLoad(probe);
        probe.StartCoroutine(probe.Run(host, server));
    }

    private IEnumerator Run(PlaygroundHost host, bool server)
    {
        yield return null;
        var network = host.GetComponent<NetworkManager>();
        network.GetComponent<UnityTransport>().SetConnectionData("127.0.0.1", 47665, "127.0.0.1");
        network.ConnectionApprovalCallback = (_, response) =>
        {
            response.Approved = true;
            response.CreatePlayerObject = false;
        };
        if (server)
        {
            Require((bool)Call(host, "StartHost", PlaygroundHost.SessionPhase.Lobby, "Country Run smoke transport failed"), "loopback host started");
            yield return WaitFor(() => network.ConnectedClientsIds.Count == 2, "both peers connected", 40);
            yield return new WaitForSecondsRealtime(.8f);
            host.LoadServiceScene(ServiceRun.FirstLevelScene);
        }
        else Require(network.StartClient(), "loopback client started");
        yield return WaitFor(() => ServiceRun.Active && !ServiceRun.Active.Introduction &&
            (server ? ServiceRun.Active.Phase == ServiceRun.RunPhase.Briefing : ServiceRun.Active.Phase != ServiceRun.RunPhase.Loading), "Level 1 loaded for host and client", 50);
        var run = ServiceRun.Active;
        var train = Value<TrainDrive>(run, "train");
        float arrivalX = run.Arrival.TrainStop.bounds.center.x;
        float startX = train.ServiceRailPosition.x;
        Require(arrivalX - startX > 820 && arrivalX - startX < 1000, $"Puddleford is {arrivalX - startX:F0} m farther east.");
        if (!server)
        {
            yield return WaitFor(() => run.TimerRunning && run.TimeLeft > 400, "client gets extended duo timer");
            yield return WaitFor(() => train.ServiceRailPosition.x > startX + 550, "client sees train cross the bridge section", 95);
            yield return WaitFor(() => run.Phase == ServiceRun.RunPhase.Won && run.Progress == 3,
                "client sees the mission completed at Puddleford", 135);
            Debug.Log("COUNTRY RUN PASS CLIENT");
            Application.Quit(0);
            yield break;
        }

        foreach (var route in FindObjectsByType<TrackSwitch>())
        {
            var repair = route.GetComponent<RepairPoint>();
            while (repair.IsBroken)
            {
                repair.ServerHit(repair.MinImpactSpeed + .5f);
                yield return new WaitForSeconds(.25f);
            }
            Value<NetworkVariable<bool>>(route, "diverging").Value = false;
        }
        yield return new WaitForSecondsRealtime(1f);
        var objectives = Value<ServiceObjective[]>(run, "objectives");
        var cargo = objectives.OfType<ServiceCargo>().Single(c => c.Target == 3);
        Value<NetworkVariable<int>>(run, "selected").Value = Array.IndexOf(objectives, cargo);
        cargo.BeginOnServer();
        run.Continue();
        Vector3 platform = run.Departure.Platform.bounds.center;
        foreach (var client in network.ConnectedClientsList)
        {
            var body = client.PlayerObject.GetComponent<Rigidbody>();
            body.position = platform + Vector3.right * client.ClientId;
            client.PlayerObject.transform.position = body.position;
            body.constraints = RigidbodyConstraints.FreezeAll;
        }
        Physics.SyncTransforms();
        yield return WaitFor(() => run.TimerRunning && run.TimeLeft >= 418, "server starts the 7:00 duo timer");
        Require(Math.Abs(network.ServerTime.Time - run.MusicStartedAt) < 2, "music starts with elongated timer");
        Value<NetworkVariable<bool>>(run, "bellRung").Value = true;
        var pieces = Value<Rigidbody[]>(cargo, "pieces");
        var bay = Value<BoxCollider>(cargo, "cargoBay");
        for (int i = 0; i < pieces.Length; i++)
            pieces[i].position = bay.bounds.center + Vector3.right * (.7f * (i - 1));
        Physics.SyncTransforms();
        yield return WaitFor(() => run.CargoLoaded == 3 && run.DoorsClosed, "three crates aboard and wagon closed");
        Value<NetworkVariable<bool>>(train, "powered").Value = true;
        Time.timeScale = 4;
        Set(train, "leverOffset", .19f);
        float maxY = train.ServiceRailPosition.y;
        foreach (int distance in new[] { 160, 350, 600, 780 })
        {
            yield return WaitFor(() =>
            {
                maxY = Mathf.Max(maxY, train.ServiceRailPosition.y);
                return train.ServiceRailPosition.x > startX + distance || run.Phase == ServiceRun.RunPhase.Failed;
            }, $"host passes {distance} m; height {maxY:F1} m", 48);
            Require(run.Phase != ServiceRun.RunPhase.Failed, "train remains on the rails");
            if (distance == 600)
            {
                Set(train, "leverOffset", 0f);
                yield return WaitFor(() => train.ServiceSpeed < .05f, "train stops to take coal", 6);
                var fuel = Value<NetworkVariable<float>>(train, "fuel");
                float before = fuel.Value;
                var mouth = Value<Transform>(train, "fireboxFeedPoint");
                var stock = Value<System.Collections.Generic.List<Rigidbody>>(train, "coalBodies");
                Value<NetworkVariable<float>>(train, "doorAngle").Value = 90f;
                for (int i = 0; i < 3; i++)
                {
                    var coal = stock[i];
                    Call(train, "WakeCoal", i);
                    coal.position = mouth.position;
                    yield return new WaitForFixedUpdate();
                }
                yield return WaitFor(() => fuel.Value > before + 1.4f, "three coal chunks refill the firebox", 5);
                Set(train, "leverOffset", .19f);
            }
        }
        Require(maxY > 7.8f, "route reaches the 7.5 m ascent");
        yield return WaitFor(() => train.ServiceRailPosition.x >= arrivalX - 16, "train reaches the approach to Puddleford", 48);
        Set(train, "leverOffset", .07f);
        yield return WaitFor(() => train.ServiceRailPosition.x >= arrivalX - 1.7f, "train approaches Puddleford's stop marker", 14);
        Set(train, "leverOffset", 0f);
        yield return WaitFor(() => run.Phase == ServiceRun.RunPhase.Arrival, "train docks inside the arrival zone", 18);
        Require(run.CargoLoaded == 3 && run.CargoTravelled == 3, "all three crates physically remain aboard at the end");
        var drop = Value<BoxCollider>(cargo, "destination");
        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].position = drop.bounds.center + Vector3.right * (.65f * (i - 1));
            pieces[i].linearVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();
        yield return WaitFor(() => run.Phase == ServiceRun.RunPhase.Won && run.Progress == 3, "mission won after unloading at Puddleford");
        yield return new WaitForSecondsRealtime(1.4f);
        Debug.Log("COUNTRY RUN PASS HOST");
        Application.Quit(0);
    }

    private static T Value<T>(object obj, string field) => (T)obj.GetType().GetField(field, Members).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Members).SetValue(obj, value);
    private static object Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Members).Invoke(obj, args);

    private static void Require(bool condition, string step)
    {
        if (condition) return;
        Debug.LogError("COUNTRY RUN FAIL " + step);
        Application.Quit(1);
        throw new InvalidOperationException(step);
    }

    private static IEnumerator WaitFor(Func<bool> check, string step, int timeout = 20)
    {
        double until = Time.realtimeSinceStartupAsDouble + timeout;
        yield return new WaitUntil(() => check() || Time.realtimeSinceStartupAsDouble >= until);
        Require(check(), step);
        Debug.Log("COUNTRY RUN " + step);
    }
}