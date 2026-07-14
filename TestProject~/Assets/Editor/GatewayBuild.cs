using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class GatewayBuild
{
    public static void PerformBuild()
    {
        Directory.CreateDirectory("Assets/Generated");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject probe = new GameObject("Gateway compile probe");
        probe.AddComponent<GatewayCompileProbe>();

        const string scenePath = "Assets/Generated/GatewaySmoke.unity";
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), scenePath);

        BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
        string outputPath = Path.Combine("Build", OutputName(target));
        Directory.CreateDirectory("Build");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = outputPath,
            target = target,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new InvalidOperationException("Gateway player smoke build failed: " + report.summary.result);
        }
    }

    private static string OutputName(BuildTarget target)
    {
        switch (target)
        {
            case BuildTarget.StandaloneWindows64:
                return "GatewaySmoke.exe";
            case BuildTarget.StandaloneOSX:
                return "GatewaySmoke.app";
            default:
                return "GatewaySmoke";
        }
    }
}
