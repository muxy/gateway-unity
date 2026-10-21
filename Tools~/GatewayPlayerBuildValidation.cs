using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class GatewayPlayerBuildValidation
{
    [Serializable] public class Settings { public string outputRoot, restoreScene; public string[] backends; }
    [Serializable] private class Report
    {
        public string unityVersion, backend, timestampUtc, status, apiProfile, stripping, failureType;
        public int errors, warnings; public ulong bytes; public double seconds;
    }
    private static bool running, batch;
    static GatewayPlayerBuildValidation() { EditorApplication.delayCall += Watch; }
    private static string DefaultSettings { get { return Path.GetFullPath(Path.Combine(Application.dataPath, "../GatewayPlayerBuildJobs.json")); } }
    private static void Watch()
    {
        if (!Application.isBatchMode && File.Exists(DefaultSettings)) Build(DefaultSettings);
    }
    public static void Run()
    {
        batch = true;
        string[] args = Environment.GetCommandLineArgs(); int i = Array.IndexOf(args, "-gatewayBuildSettings");
        if (i < 0 || i + 1 >= args.Length) throw new ArgumentException("Missing build settings.");
        Build(args[i + 1]);
    }
    private static void Build(string settingsPath)
    {
        if (running) return;
        Settings settings = JsonUtility.FromJson<Settings>(File.ReadAllText(settingsPath));
        Directory.CreateDirectory(settings.outputRoot);
        string marker = Path.Combine(settings.outputRoot, "builds-completed.json");
        if (File.Exists(marker)) return;
        running = true;
        var group = BuildTargetGroup.Standalone;
        var backend = PlayerSettings.GetScriptingBackend(group);
        var stripping = PlayerSettings.GetManagedStrippingLevel(group);
        string company = PlayerSettings.companyName, product = PlayerSettings.productName;
        bool success = true;
        string step = "scene-create";
        SceneSetup[] originalSetup = EditorSceneManager.GetSceneManagerSetup();
        bool restoreScenes = !batch;
        for (int index = 0; index < SceneManager.sceneCount; index++)
            if (SceneManager.GetSceneAt(index).isDirty || String.IsNullOrEmpty(SceneManager.GetSceneAt(index).path)) restoreScenes = false;
        try
        {
            string scenePath = "Assets/GatewayPlayerValidation/Fixture.unity";
            Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
            Scene original = SceneManager.GetActiveScene();
            bool emptyScene = original.IsValid() && !original.isDirty && String.IsNullOrEmpty(original.path) && SceneManager.sceneCount == 1;
            Scene fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, batch || emptyScene ? NewSceneMode.Single : NewSceneMode.Additive);
            EditorSceneManager.SaveScene(fixture, scenePath);
            step = "scene-close";
            if (!batch && !emptyScene && original.IsValid() && original.isLoaded) EditorSceneManager.CloseScene(fixture, true);
            if (original.IsValid() && original.isLoaded) SceneManager.SetActiveScene(original);
            PlayerSettings.companyName = "GatewayCompatibilityValidation";
            PlayerSettings.productName = "GatewayPlayerValidation";
            foreach (string requested in settings.backends)
            {
                var report = new Report { unityVersion = Application.unityVersion, backend = requested, timestampUtc = DateTime.UtcNow.ToString("o"), apiProfile = PlayerSettings.GetApiCompatibilityLevel(group).ToString(), stripping = "High" };
                string output = Path.Combine(settings.outputRoot, requested);
                Directory.CreateDirectory(output);
                try
                {
                    step = "backend-" + requested;
                    PlayerSettings.SetScriptingBackend(group, requested == "IL2CPP" ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x);
                    PlayerSettings.SetManagedStrippingLevel(group, ManagedStrippingLevel.High);
                    var build = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scenePath }, locationPathName = Path.Combine(output, "GatewayValidation.exe"), target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
                    report.status = build.summary.result == BuildResult.Succeeded ? "passed" : "failed";
                    report.errors = Convert.ToInt32(build.summary.totalErrors); report.warnings = Convert.ToInt32(build.summary.totalWarnings);
                    report.seconds = build.summary.totalTime.TotalSeconds; report.bytes = build.summary.totalSize;
                    success &= report.status == "passed";
                }
                catch (Exception error) { report.status = "failed"; report.failureType = error.GetType().Name; success = false; }
                File.WriteAllText(Path.Combine(output, "build-result.json"), JsonUtility.ToJson(report, true));
            }
        }
        catch (Exception error)
        {
            success = false;
            File.WriteAllText(Path.Combine(settings.outputRoot, "build-failure.json"), JsonUtility.ToJson(new Failure { step = step, exceptionType = error.GetType().Name, detail = error.Message }, true));
        }
        finally
        {
            PlayerSettings.SetScriptingBackend(group, backend);
            PlayerSettings.SetManagedStrippingLevel(group, stripping);
            PlayerSettings.companyName = company; PlayerSettings.productName = product;
            if (restoreScenes && originalSetup.Length != 0) EditorSceneManager.RestoreSceneManagerSetup(originalSetup);
            else if (!batch && !String.IsNullOrEmpty(settings.restoreScene))
            {
                bool onlyFixtureScenes = true;
                for (int index = 0; index < SceneManager.sceneCount; index++)
                {
                    Scene scene = SceneManager.GetSceneAt(index);
                    onlyFixtureScenes &= !scene.isDirty && (String.IsNullOrEmpty(scene.path) || scene.path.StartsWith("Assets/GatewayPlayerValidation/", StringComparison.Ordinal));
                }
                if (onlyFixtureScenes) EditorSceneManager.OpenScene(settings.restoreScene, OpenSceneMode.Single);
            }
            File.WriteAllText(marker, "{\"status\":\"" + (success ? "passed" : "failed") + "\"}");
            if (batch) EditorApplication.Exit(success ? 0 : 1);
        }
    }
    [Serializable] private class Failure { public string step, exceptionType, detail; }
}
