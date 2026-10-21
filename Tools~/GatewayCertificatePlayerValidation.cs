// Dedicated no-credential player probe for a locally supplied untrusted TLS listener.
using System;
using System.IO;
using System.Threading.Tasks;
using MuxyGateway;
using UnityEngine;

public sealed class GatewayCertificatePlayerValidation : MonoBehaviour
{
    [Serializable] private class Report
    {
        public string unityVersion, backend, status = "failed", exceptionType, failureDetail;
        public bool rejected, configurationInitialized, disconnected, stopCompleted;
    }
    private string endpoint, output;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string[] args = Environment.GetCommandLineArgs();
        int endpointIndex = Array.IndexOf(args, "-gatewayCertificateEndpoint");
        int outputIndex = Array.IndexOf(args, "-gatewayCertificateResult");
        if (endpointIndex < 0 || outputIndex < 0) return;
        var owner = new GameObject("Gateway certificate validation");
        var probe = owner.AddComponent<GatewayCertificatePlayerValidation>();
        probe.endpoint = args[endpointIndex + 1]; probe.output = args[outputIndex + 1];
    }
    private async void Start()
    {
        var report = new Report { unityVersion = Application.unityVersion };
#if ENABLE_IL2CPP
        report.backend = "IL2CPP";
#else
        report.backend = "Mono";
#endif
        using (var transport = new WebsocketTransport())
        {
            try
            {
                Task connect = transport.Open(endpoint);
                if (await Task.WhenAny(connect, Task.Delay(15000)) != connect) throw new TimeoutException();
                try { await connect; }
                catch (Exception error)
                {
                    report.exceptionType = error.GetType().Name;
                    report.failureDetail = error.ToString();
                    report.rejected = error is System.Security.Authentication.AuthenticationException;
                    report.configurationInitialized = true;
                    for (Exception nested = error; nested != null; nested = nested.InnerException)
                        if (nested is TypeInitializationException || nested is MissingMemberException)
                            report.configurationInitialized = false;
                }
                Task stop = transport.StopAsync();
                if (await Task.WhenAny(stop, Task.Delay(2000)) != stop) throw new TimeoutException();
                await stop;
                report.stopCompleted = true; report.disconnected = !transport.IsConnected;
                if (report.rejected && report.configurationInitialized && report.stopCompleted && report.disconnected) report.status = "passed";
            }
            catch (Exception error) { report.exceptionType = error.GetType().Name; report.failureDetail = error.ToString(); }
        }
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        Application.Quit(report.status == "passed" ? 0 : 1);
    }
}
