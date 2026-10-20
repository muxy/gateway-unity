// Local-only transport regression checks. Copy into a fixture's Assets/Editor
// and invoke GatewayTransportLifecycleValidation.Run with -executeMethod.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using MuxyGateway;
using UnityEditor;
using UnityEngine;

public static class GatewayTransportLifecycleValidation
{
    [Serializable] private class Report
    {
        public string unityVersion, timestampUtc, status, failureType, failureStep, failureDetail;
        public string scope = "local-loopback-transport-lifecycle-and-prefab-disable";
        public List<string> passed = new List<string>();
        public double maximumStopMilliseconds;
    }
    private static Report report;
    private static string step;
    private static object Field(object owner, string name)
    {
        return owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance).GetValue(owner);
    }
    private static void Check(bool condition, string label)
    {
        if (!condition) throw new InvalidOperationException(label);
        report.passed.Add(label);
    }
    private static async Task Until(Func<bool> condition)
    {
        var watch = Stopwatch.StartNew();
        while (!condition()) { if (watch.ElapsedMilliseconds > 8000) throw new TimeoutException(step); await Task.Delay(10); }
    }
    private static async Task Stop(WebsocketTransport transport)
    {
        var watch = Stopwatch.StartNew();
        Task stop = transport.StopAsync();
        if (await Task.WhenAny(stop, Task.Delay(2000)) != stop) throw new TimeoutException("StopAsync did not join promptly.");
        await stop;
        report.maximumStopMilliseconds = Math.Max(report.maximumStopMilliseconds, watch.Elapsed.TotalMilliseconds);
        Check(!transport.IsConnected && Field(transport, "Current") == null && ((Task)Field(transport, "Stopping")).IsCompleted, step + ": socket tasks joined");
    }
    private sealed class Server : IDisposable
    {
        internal readonly TcpListener Listener = new TcpListener(IPAddress.Loopback, 0);
        internal string Uri { get { return "ws://127.0.0.1:" + ((IPEndPoint)Listener.LocalEndpoint).Port + "/gateway-test"; } }
        internal Server() { Listener.Start(); }
        internal async Task<TcpClient> Accept(bool handshake = true)
        {
            TcpClient client = await Listener.AcceptTcpClientAsync().ConfigureAwait(false);
            if (!handshake) return client;
            NetworkStream stream = client.GetStream();
            var header = new StringBuilder();
            var one = new byte[1];
            while (!header.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                if (await stream.ReadAsync(one, 0, 1).ConfigureAwait(false) == 0 || header.Length > 16384) throw new IOException();
                header.Append((char)one[0]);
            }
            string key = null;
            foreach (string line in header.ToString().Split('\n'))
                if (line.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase)) key = line.Substring(line.IndexOf(':') + 1).Trim();
            using (SHA1 sha = SHA1.Create())
            {
                string accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                byte[] bytes = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
                await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            }
            return client;
        }
        internal static async Task Frame(TcpClient client, byte opcode, bool final, string text)
        {
            byte[] textBytes = Encoding.UTF8.GetBytes(text);
            if (textBytes.Length >= 126) throw new InvalidOperationException();
            byte[] frame = new byte[textBytes.Length + 2];
            frame[0] = (byte)((final ? 128 : 0) | opcode); frame[1] = (byte)textBytes.Length;
            Buffer.BlockCopy(textBytes, 0, frame, 2, textBytes.Length);
            await client.GetStream().WriteAsync(frame, 0, frame.Length);
        }
        public void Dispose() { Listener.Stop(); }
    }

    public static async void Run()
    {
        report = new Report { unityVersion = Application.unityVersion, timestampUtc = DateTime.UtcNow.ToString("o"), status = "running" };
        SDK sdk = null;
        WebsocketTransport transport = null;
        GameObject managerObject = null;
        string originalCompany = PlayerSettings.companyName, originalProduct = PlayerSettings.productName;
        try
        {
            step = "never-opened repeated cleanup";
            transport = new WebsocketTransport();
            await Stop(transport); await Stop(transport); transport.Dispose(); transport.Dispose();
            Check(true, step);

            sdk = new SDK("gamelink-testing");
            transport = new WebsocketTransport();
            using (var server = new Server())
            {
                step = "open and duplicate start";
                Task<TcpClient> accept = server.Accept();
                await transport.Open(server.Uri);
                using (TcpClient peer = await accept)
                {
                    transport.Run(sdk); transport.Run(sdk); await transport.Open(server.Uri);
                    Check(transport.IsConnected, step);
                    object session = Field(transport, "Current");
                    object first = Field(session, "Connection");
                    var incoming = (System.Collections.Concurrent.ConcurrentQueue<string>)Field(first, "Incoming");
                    step = "fragment reassembly";
                    await Server.Frame(peer, 1, false, "caf\u00e9");
                    await Server.Frame(peer, 0, true, " \u65e5\u672c\u8a9e");
                    await Until(() => !incoming.IsEmpty);
                    incoming.TryDequeue(out string message);
                    Check(message == "caf\u00e9 \u65e5\u672c\u8a9e", step);

                    step = "drop partial frame and reconnect";
                    await Server.Frame(peer, 1, false, "discard-this-partial");
                    Task<TcpClient> reconnect = server.Accept();
                    peer.Close();
                    using (TcpClient nextPeer = await reconnect)
                    {
                        await Until(() => transport.IsConnected && Field(session, "Connection") != first);
                        object second = Field(session, "Connection");
                        Check((int)Field(second, "NotifyReconnect") == 1, "reconnect scheduled on owning thread");
                        var nextIncoming = (System.Collections.Concurrent.ConcurrentQueue<string>)Field(second, "Incoming");
                        await Server.Frame(nextPeer, 1, true, "fresh-message");
                        await Until(() => !nextIncoming.IsEmpty);
                        nextIncoming.TryDequeue(out string fresh);
                        Check(fresh == "fresh-message" && incoming.IsEmpty, step);
                        // Prevent the synthetic message from entering the real native protocol.
                        transport.Update(sdk);
                        Check((int)Field(second, "NotifyReconnect") == 0, "native reconnect dispatched by Update");
                        step = "stop while receiving";
                        await Stop(transport); await Stop(transport);
                    }
                }

                step = "explicit reopen after stop";
                accept = server.Accept();
                await transport.Open(server.Uri);
                using (TcpClient peer = await accept)
                {
                    transport.Run(sdk);
                    Check(transport.IsConnected, step);
                    step = "cancel reconnect backoff";
                    peer.Close();
                    server.Listener.Stop();
                    await Until(() => !transport.IsConnected);
                    await Task.Delay(150);
                    await Stop(transport);
                }
            }
            transport.Dispose();

            step = "cancel pending connect";
            transport = new WebsocketTransport();
            using (var server = new Server())
            {
                Task<TcpClient> accept = server.Accept(false);
                Task open = transport.Open(server.Uri);
                using (TcpClient stalled = await accept)
                {
                    await Stop(transport);
                    bool canceled = false;
                    try { await open; } catch (OperationCanceledException) { canceled = true; }
                    Check(canceled, step);
                }
            }
            transport.Dispose();

            step = "cancel pending TLS handshake";
            transport = new WebsocketTransport();
            using (var server = new Server())
            {
                Task<TcpClient> accept = server.Accept(false);
                Task open = transport.Open(server.Uri.Replace("ws://", "wss://"));
                using (TcpClient stalled = await accept)
                {
                    await Task.Delay(50);
                    await Stop(transport);
                    bool canceled = false;
                    try { await open; } catch (OperationCanceledException) { canceled = true; }
                    Check(canceled, step);
                }
            }
            transport.Dispose();

            step = "reject invalid HTTP upgrade";
            transport = new WebsocketTransport();
            using (var server = new Server())
            {
                Task<TcpClient> accept = server.Accept(false);
                Task open = transport.Open(server.Uri);
                using (TcpClient peer = await accept)
                {
                    byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: invalid\r\n\r\n");
                    await peer.GetStream().WriteAsync(response, 0, response.Length);
                    bool rejected = false;
                    try { await open; } catch (WebSocketException) { rejected = true; }
                    await Stop(transport);
                    Check(rejected, step);
                }
            }
            transport.Dispose();

            step = "initial failure and retry";
            transport = new WebsocketTransport();
            string unavailable;
            using (var server = new Server()) unavailable = server.Uri;
            bool failure = false;
            try { await transport.Open(unavailable); }
            catch (Exception error) when (error is WebSocketException || error is SocketException || error is IOException) { failure = true; }
            await Stop(transport);
            Check(failure && transport.LastConnectionError != null, "initial connection failure surfaced");
            using (var server = new Server())
            {
                Task<TcpClient> accept = server.Accept();
                await transport.Open(server.Uri);
                using (TcpClient peer = await accept)
                {
                    transport.Run(sdk); Check(transport.IsConnected, step);
                    transport.Dispose(); transport.Dispose();
                    bool rejected = false;
                    try { await transport.Open(server.Uri); } catch (ObjectDisposedException) { rejected = true; }
                    Check(rejected, "disposed transport rejects reuse");
                }
            }

            step = "prefab disable and re-enable";
            PlayerSettings.companyName = "GatewayCompatibilityValidation";
            PlayerSettings.productName = "Temporary-" + Guid.NewGuid().ToString("N");
            managerObject = new GameObject("Gateway temporary lifecycle validation");
            managerObject.SetActive(false);
            var manager = managerObject.AddComponent<MuxyGatewayManager>();
            manager.GameID = "gamelink-testing";
            // Use an isolated, unique PlayerPrefs namespace; never read a user's tokens.
            typeof(MuxyGatewayManager).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            var managed = (SDK)Field(manager, "SDK");
            typeof(MuxyGatewayManager).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            Check(managed.IsDisposed && !managed.IsConnected, "prefab OnDisable disposes SDK");
            typeof(MuxyGatewayManager).GetMethod("OnEnable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            var recreated = (SDK)Field(manager, "SDK");
            Check(recreated != managed && !recreated.IsDisposed, "prefab re-enable creates fresh SDK");
            typeof(MuxyGatewayManager).GetMethod("OnDisable", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(manager, null);
            Check(recreated.IsDisposed, "prefab re-enable cleanup");

            step = "native callbacks and pending authentication cleanup";
            sdk.OnGameActionUsed(used => { }); sdk.OnGameActionUsed(used => { });
            sdk.OnBitsUsed(used => { }); sdk.OnBitsUsed(used => { });
            sdk.AuthenticateWithPIN("000000", response => { }); // Queued locally; never sent to a server.
            Check(((IDictionary)Field(sdk, "AuthenticationHandles")).Count == 1, "pending auth handle tracked");
            sdk.Dispose(); sdk.Dispose();
            Check(((MuxyGateway.Imports.Schema.GatewaySDK)Field(sdk, "Instance")).SDK == IntPtr.Zero, "native instance released");
            bool handlesReleased = ((IDictionary)Field(sdk, "AuthenticationHandles")).Count == 0;
            foreach (string name in new[] { "DebugMessage", "PollDelegateHandle", "GameActionUsedDelegateHandle", "BitsUsedDelegateHandle" })
                handlesReleased &= !((System.Runtime.InteropServices.GCHandle)Field(sdk, name)).IsAllocated;
            Check(handlesReleased, "native callback handles released including pending auth");
            bool useRejected = false;
            try { sdk.AuthenticateWithPIN("000000", response => { }); } catch (ObjectDisposedException) { useRejected = true; }
            Check(useRejected, "disposed SDK rejects native calls");
            report.status = "passed";
        }
        catch (Exception error) { report.status = "failed"; report.failureType = error.GetType().Name; report.failureStep = step; report.failureDetail = error.ToString(); }
        finally
        {
            transport?.Dispose(); sdk?.Dispose();
            if (managerObject != null) UnityEngine.Object.DestroyImmediate(managerObject);
            PlayerSettings.companyName = originalCompany; PlayerSettings.productName = originalProduct;
            string path = Path.Combine(Application.dataPath, "../transport-lifecycle-result.json");
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            EditorApplication.Exit(report.status == "passed" ? 0 : 1);
        }
    }
}
