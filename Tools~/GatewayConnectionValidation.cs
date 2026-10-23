// Credential-free production WSS check through the SDK's public connection API.
using System;
using System.IO;
using System.Threading.Tasks;
using MuxyGateway;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class GatewayConnectionValidation : MonoBehaviour
{
    [Serializable] private class Report
    {
        public string unityVersion, backend, timestampUtc, status = "failed", failure;
        public string scope = "unauthenticated-production-WSS-connect-stop-reopen";
        public bool connected, stopped, reopened, disposed;
    }

    private static async Task<bool> Probe(string path)
    {
        var report = new Report { unityVersion = Application.unityVersion, timestampUtc = DateTime.UtcNow.ToString("o") };
#if ENABLE_IL2CPP
        report.backend = "IL2CPP";
#else
        report.backend = "Mono";
#endif
        SDK sdk = null;
        try
        {
            sdk = new SDK("gamelink-testing");
            await sdk.RunInProductionAsync(); report.connected = sdk.IsConnected;
            await sdk.StopWebsocketTransportAsync(); report.stopped = !sdk.IsConnected;
            await sdk.RunInProductionAsync(); report.reopened = sdk.IsConnected;
            sdk.Dispose(); report.disposed = sdk.IsDisposed && !sdk.IsConnected;
            if (report.connected && report.stopped && report.reopened && report.disposed) report.status = "passed";
        }
        catch (Exception error) { report.failure = error.ToString(); }
        finally { sdk?.Dispose(); }
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        return report.status == "passed";
    }

#if UNITY_EDITOR
    public static async void Run()
    {
        bool passed = await Probe(Path.GetFullPath(Path.Combine(Application.dataPath, "../connection-result.json")));
        EditorApplication.Exit(passed ? 0 : 1);
    }
#endif

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gatewayConnectionResult") < 0) return;
        new GameObject("Gateway connection validation").AddComponent<GatewayConnectionValidation>();
    }

    private async void Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-gatewayConnectionResult");
        bool passed = await Probe(args[index + 1]);
        Application.Quit(passed ? 0 : 1);
    }
}
