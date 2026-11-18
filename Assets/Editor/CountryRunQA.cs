using System.Diagnostics;
using UnityEditor;
using UnityEditor.Build.Reporting;
using Debug = UnityEngine.Debug;

internal static class CountryRunQA
{
    [MenuItem("Tools/Conductor Chaos/QA/Run Country Run Smoke Test")]
    private static void Build()
    {
        const string dir = "C:/Users/mauri/Documents/Codex/2026-11-15/bij-x20/work/countryrun";
        var exe = dir + "/ConductorChaos.exe";
        var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/TrainPlayground.unity", "Assets/Scenes/ServiceIntroduction.unity", "Assets/Scenes/ServiceLevel01.unity" },
            locationPathName = exe,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        Debug.Log("COUNTRY RUN FUEL TEST BUILD " + result.summary.result);
        if (result.summary.result != BuildResult.Succeeded) return;
        foreach (var role in new[] { "host", "client" })
            Process.Start(new ProcessStartInfo(exe, $"-batchmode -nographics -country-run-{role} -logFile \"{dir}/{role}.log\"")
                { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
    }
}