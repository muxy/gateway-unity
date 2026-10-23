// Isolated runtime probe: no Gateway SDK, protocol implementation, or remote server.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

public static class GatewayClientWebSocketValidation
{
    [Serializable] private class Observation
    {
        public string scheme, outcome, exceptionType;
        public int requestBytes;
        public bool completedAfterCancel, completedAfterAbort, completedAfterDispose, completedAfterPeerClose;
        public double elapsedMilliseconds;
    }
    [Serializable] private class Report
    {
        public string unityVersion, timestampUtc, status, failure;
        public string scope = "standalone-ClientWebSocket-stalled-HTTP-and-TLS-handshake";
        public List<Observation> observations = new List<Observation>();
    }

    private static async Task<bool> Completes(Task operation, int milliseconds)
    {
        return await Task.WhenAny(operation, Task.Delay(milliseconds)).ConfigureAwait(false) == operation;
    }

    private static async Task Probe(string scheme, Report report)
    {
        var result = new Observation { scheme = scheme };
        report.observations.Add(result);
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        TcpClient peer = null;
        var socket = new ClientWebSocket();
        var cancellation = new CancellationTokenSource();
        Task connect = null;
        var watch = Stopwatch.StartNew();
        try
        {
            var uri = new Uri(scheme + "://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/probe");
            connect = Task.Run(() => socket.ConnectAsync(uri, cancellation.Token));
            Task<TcpClient> accept = listener.AcceptTcpClientAsync();
            if (!await Completes(accept, 5000).ConfigureAwait(false)) throw new TimeoutException("TCP accept");
            peer = await accept.ConfigureAwait(false);
            var bytes = new byte[4096];
            Task<int> read = peer.GetStream().ReadAsync(bytes, 0, bytes.Length);
            if (!await Completes(read, 5000).ConfigureAwait(false)) throw new TimeoutException("No HTTP request or TLS ClientHello");
            result.requestBytes = await read.ConfigureAwait(false);
            if (result.requestBytes == 0 || connect.IsCompleted) throw new InvalidOperationException("Handshake was not pending");

            // The peer accepts/reads but deliberately sends no handshake response.
            cancellation.Cancel();
            result.completedAfterCancel = await Completes(connect, 1000).ConfigureAwait(false);
            socket.Abort();
            result.completedAfterAbort = await Completes(connect, 1000).ConfigureAwait(false);
            socket.Dispose();
            result.completedAfterDispose = await Completes(connect, 1000).ConfigureAwait(false);
            peer.Close();
            result.completedAfterPeerClose = await Completes(connect, 5000).ConfigureAwait(false);
            if (!result.completedAfterPeerClose) throw new TimeoutException("Connect still pending after peer closure");
            try { await connect.ConfigureAwait(false); result.outcome = "unexpected-success"; }
            catch (Exception error) { result.outcome = "failed-as-expected"; result.exceptionType = error.GetType().Name; }
        }
        finally
        {
            result.elapsedMilliseconds = watch.Elapsed.TotalMilliseconds;
            peer?.Close(); listener.Stop(); socket.Dispose(); cancellation.Dispose();
        }
    }

    public static async void Run()
    {
        var report = new Report { unityVersion = Application.unityVersion, timestampUtc = DateTime.UtcNow.ToString("o"), status = "running" };
        string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../clientwebsocket-result.json"));
        try
        {
            await Probe("ws", report);
            await Probe("wss", report);
            report.status = "passed";
        }
        catch (Exception error) { report.status = "failed"; report.failure = error.ToString(); }
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        EditorApplication.Exit(report.status == "passed" ? 0 : 1);
    }
}
