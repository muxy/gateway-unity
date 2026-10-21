// Invoke in an owned Editor fixture with -gatewayCertificateEndpoint wss://localhost:PORT/.
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using MuxyGateway;
using UnityEditor;
using UnityEngine;

public static class GatewayCertificateValidation
{
    [Serializable] private class Report { public string unityVersion, status, exceptionType; public bool rejected, disconnected, tasksJoined; }
    public static async void Run()
    {
        var report = new Report { unityVersion = Application.unityVersion, status = "failed" };
        using (var transport = new WebsocketTransport())
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                string endpoint = args[Array.IndexOf(args, "-gatewayCertificateEndpoint") + 1];
                Task connection = transport.Open(endpoint);
                if (await Task.WhenAny(connection, Task.Delay(15000)) != connection) throw new TimeoutException();
                try { await connection; }
                catch (Exception error)
                {
                    report.exceptionType = error.GetType().Name;
                    // The listener must independently confirm it served its certificate.
                    report.rejected = error is System.Security.Authentication.AuthenticationException;
                }
                Task stop = transport.StopAsync();
                if (await Task.WhenAny(stop, Task.Delay(2000)) != stop) throw new TimeoutException();
                await stop;
                report.disconnected = !transport.IsConnected;
                report.tasksJoined = typeof(WebsocketTransport).GetField("Current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transport) == null;
                if (report.rejected && report.disconnected && report.tasksJoined) report.status = "passed";
            }
            catch (Exception error) { report.exceptionType = error.GetType().Name; }
        }
        File.WriteAllText(Path.Combine(Application.dataPath, "../certificate-result.json"), JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.status == "passed" ? 0 : 1);
    }
}
