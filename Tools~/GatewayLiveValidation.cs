// Temporary Editor-side validation for already-open, licensed Unity projects.
// Copy into a dedicated Assets/.../Editor directory with settings in
// Temp/GatewayLiveValidation/settings.json. This is not runtime SDK code.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MuxyGateway;
using MuxyGateway.Imports;
using MuxyGateway.Imports.Schema;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

[InitializeOnLoad]
public static class GatewayLiveValidation
{
    [Serializable] public class Settings { public string outputDirectory; public string gameId; }
    [Serializable] public class PinRequest { public string pin; public string generatedAtUtc; }
    [Serializable] public class Report
    {
        public string unityVersion, timestampUtc, scope, status;
        public string installation = "existing-project-local-package";
        public bool cleanProject;
        public bool publicTypesPreserved, utf8StructMarshalling;
        public string tmpProvider, uiProvider;
        public string gameId, stage = "production", scheme = "wss", host;
        public bool nativeSdkConstructed, debugForwardingDetached, secureConnectionOpened;
        public string pinGeneratedAtUtc, pinSubmittedAtUtc, pinCallbackAtUtc;
        public double pinAgeAtSubmissionSeconds;
        public bool pinCallbackReceived, pinHasError, pinIsAuthenticated, pinJwtPresent, pinRefreshTokenPresent;
        public string refreshSubmittedAtUtc, refreshCallbackAtUtc;
        public bool refreshCallbackReceived, refreshHasError, refreshIsAuthenticated, refreshJwtPresent, refreshTokenPresent;
        public int protocolErrorCode;
        public string failureCategory;
        public string exceptionType, operation;
        public bool nativeSdkReleased, validationSocketDisposed;
    }
    [Serializable] private class Envelope { public ProtocolError[] errors; }
    [Serializable] private class ProtocolError { public int status, number; }

    private static readonly string controlDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/GatewayLiveValidation"));
    private static Settings settings;
    private static Report compatibility;
    private static Report authentication;
    private static SDK client;
    private static GatewaySDK native;
    private static ClientWebSocket socket;
    private static CancellationTokenSource cancellation;
    private static Task connectTask, sendTask;
    private static Task<string> receiveTask;
    private static readonly Queue<byte[]> sends = new Queue<byte[]>();
    private static byte[] sending;
    private static string refreshToken;
    private static string pendingPin;
    private static Uri endpoint;
    private static string phase;
    private static double deadline;

    static GatewayLiveValidation()
    {
        EditorApplication.delayCall += Initialize;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
    }

    private static void Initialize()
    {
        string path = Path.Combine(controlDirectory, "settings.json");
        if (!File.Exists(path)) return;
        settings = JsonUtility.FromJson<Settings>(File.ReadAllText(path));
        Directory.CreateDirectory(settings.outputDirectory);
        if (File.Exists(Path.Combine(settings.outputDirectory, "authentication-result.json"))) return;
        try
        {
            compatibility = new Report { unityVersion = Application.unityVersion, timestampUtc = Now(), scope = "live-editor-package-compilation-and-utf8", status = "passed" };
            Type[] types = { typeof(SDK), typeof(GameMetadata), typeof(MuxyGatewayManager), typeof(MuxyGatewayAuthentication), typeof(MuxyGatewayPolling), typeof(MuxyGatewayNotificationHistory) };
            foreach (Type type in types)
                if (type.Assembly.GetName().Name != "Muxy.Gateway") throw new InvalidOperationException();
            compatibility.publicTypesPreserved = true;
            ValidateUtf8();
            compatibility.utf8StructMarshalling = true;
            PackageInfo tmp = PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
            PackageInfo ui = PackageInfo.FindForAssembly(typeof(UnityEngine.UI.Graphic).Assembly);
            compatibility.tmpProvider = tmp.name + "@" + tmp.version;
            compatibility.uiProvider = ui.name + "@" + ui.version;
            Write("compatibility-result.json", compatibility);

            authentication = new Report { unityVersion = Application.unityVersion, timestampUtc = Now(), scope = "live-editor-sdk-pin-and-in-memory-refresh-with-validation-wss-transport", status = "connecting", gameId = settings.gameId };
            client = new SDK(settings.gameId);
            authentication.nativeSdkConstructed = true;
            native = (GatewaySDK)typeof(SDK).GetField("Instance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(client);
            Imported.MGW_SDK_DetachOnDebugMessage(native);
            authentication.debugForwardingDetached = true;
            endpoint = new Uri("wss://" + client.GetProductionURL());
            if (endpoint.Scheme != "wss" || endpoint.Host != "gamelink.muxy.io") throw new InvalidOperationException();
            authentication.host = endpoint.Host;
            cancellation = new CancellationTokenSource();
            socket = new ClientWebSocket();
            phase = "waiting-for-pin";
            EditorApplication.update += Tick;
            WriteState();
        }
        catch (Exception) { Fail("initialization-or-compatibility"); }
    }

    private static void Tick()
    {
        try
        {
            if (phase == "finishing") { Finish(); return; }
            if (phase == "connecting")
            {
                if (!connectTask.IsCompleted) { CheckDeadline(); return; }
                authentication.operation = "complete-connect";
                connectTask.GetAwaiter().GetResult();
                authentication.secureConnectionOpened = socket.State == WebSocketState.Open;
                if (!authentication.secureConnectionOpened) { Fail("connection-not-open"); return; }
                authentication.pinSubmittedAtUtc = Now();
                authentication.pinAgeAtSubmissionSeconds = (DateTime.UtcNow - DateTime.Parse(authentication.pinGeneratedAtUtc).ToUniversalTime()).TotalSeconds;
                phase = "authenticating-pin";
                deadline = EditorApplication.timeSinceStartup + 30;
                authentication.operation = "submit-sdk-pin";
                client.AuthenticateWithPIN(pendingPin, OnPin);
                pendingPin = null;
                WriteState();
            }
            if (phase == "waiting-for-pin")
            {
                string requestPath = Path.Combine(controlDirectory, "pin-request.json");
                if (!File.Exists(requestPath)) return;
                PinRequest request = JsonUtility.FromJson<PinRequest>(File.ReadAllText(requestPath));
                File.Delete(requestPath);
                string pin = (request.pin ?? "").Trim().ToUpperInvariant();
                request.pin = null;
                if (!System.Text.RegularExpressions.Regex.IsMatch(pin, "^[A-Z0-9]{6}$")) { Fail("invalid-test-input"); return; }
                authentication.pinGeneratedAtUtc = request.generatedAtUtc;
                pendingPin = pin;
                pin = null;
                phase = "connecting";
                deadline = EditorApplication.timeSinceStartup + 30;
                authentication.operation = "start-connect";
                connectTask = socket.ConnectAsync(endpoint, cancellation.Token);
                WriteState();
                return;
            }
            if (phase == "refresh-pending")
            {
                phase = "authenticating-refresh";
                deadline = EditorApplication.timeSinceStartup + 30;
                authentication.refreshSubmittedAtUtc = Now();
                client.AuthenticateWithRefreshToken(refreshToken, OnRefresh);
                refreshToken = null;
                WriteState();
            }
            if (phase == "authenticating-pin" || phase == "authenticating-refresh")
            {
                CheckDeadline();
                if (phase == "finishing") return;
                client.Update();
                if (sendTask != null && sendTask.IsCompleted)
                {
                    authentication.operation = "complete-send";
                    sendTask.GetAwaiter().GetResult();
                    Array.Clear(sending, 0, sending.Length);
                    sending = null;
                    sendTask = null;
                }
                authentication.operation = "enumerate-sdk-payloads";
                client.ForeachPayload(payload => sends.Enqueue(payload.Bytes));
                if (sendTask == null && sends.Count > 0)
                {
                    sending = sends.Dequeue();
                    authentication.operation = "start-send";
                    sendTask = socket.SendAsync(new ArraySegment<byte>(sending), WebSocketMessageType.Text, true, cancellation.Token);
                }
                if (receiveTask != null && receiveTask.IsCompleted)
                {
                    authentication.operation = "complete-receive";
                    string message = receiveTask.GetAwaiter().GetResult();
                    receiveTask = null;
                    try
                    {
                        Envelope envelope = JsonUtility.FromJson<Envelope>(message);
                        if (envelope != null && envelope.errors != null)
                            foreach (ProtocolError error in envelope.errors)
                                authentication.protocolErrorCode = error.status != 0 ? error.status : error.number;
                    }
                    catch (Exception) { /* Diagnostics must not prevent native parsing. */ }
                    authentication.operation = "dispatch-native-response";
                    client.ReceiveMessage(message);
                    message = null;
                }
                if (receiveTask == null && phase != "finishing") receiveTask = ReadMessage(socket, cancellation.Token);
            }
        }
        catch (Exception exception) { authentication.exceptionType = exception.GetType().Name; Fail("transport-or-native-operation"); }
    }

    private static void OnPin(AuthenticationResponse response)
    {
        authentication.pinCallbackReceived = true;
        authentication.pinCallbackAtUtc = Now();
        authentication.pinHasError = response.HasError;
        authentication.pinIsAuthenticated = client.IsAuthenticated;
        authentication.pinJwtPresent = !String.IsNullOrEmpty(response.JWT);
        authentication.pinRefreshTokenPresent = !String.IsNullOrEmpty(response.RefreshToken);
        bool passed = !response.HasError && authentication.pinIsAuthenticated && authentication.pinJwtPresent && authentication.pinRefreshTokenPresent;
        if (passed) { refreshToken = response.RefreshToken; phase = "refresh-pending"; }
        else Fail("pin-authentication-rejected-or-incomplete");
        response.JWT = response.RefreshToken = response.TwitchUsername = null;
        WriteState();
    }

    private static void OnRefresh(AuthenticationResponse response)
    {
        authentication.refreshCallbackReceived = true;
        authentication.refreshCallbackAtUtc = Now();
        authentication.refreshHasError = response.HasError;
        authentication.refreshIsAuthenticated = client.IsAuthenticated;
        authentication.refreshJwtPresent = !String.IsNullOrEmpty(response.JWT);
        authentication.refreshTokenPresent = !String.IsNullOrEmpty(response.RefreshToken);
        authentication.status = !response.HasError && authentication.refreshIsAuthenticated && authentication.refreshJwtPresent && authentication.refreshTokenPresent ? "passed" : "failed";
        if (authentication.status != "passed") authentication.failureCategory = "refresh-authentication-rejected-or-incomplete";
        response.JWT = response.RefreshToken = response.TwitchUsername = null;
        phase = "finishing";
    }

    private static async Task<string> ReadMessage(ClientWebSocket connection, CancellationToken token)
    {
        using (var memory = new MemoryStream())
        {
            byte[] buffer = new byte[4096];
            WebSocketReceiveResult result;
            do
            {
                result = await connection.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close) throw new EndOfStreamException();
                if (result.MessageType != WebSocketMessageType.Text || memory.Length + result.Count > 1024 * 1024) throw new InvalidDataException();
                memory.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);
            return Encoding.UTF8.GetString(memory.ToArray());
        }
    }

    private static void CheckDeadline() { if (EditorApplication.timeSinceStartup > deadline) Fail("operation-timeout"); }
    private static void Fail(string category)
    {
        if (authentication == null) authentication = new Report { unityVersion = Application.unityVersion, timestampUtc = Now(), scope = "live-editor-sdk-validation" };
        authentication.status = "failed";
        authentication.failureCategory = category;
        phase = "finishing";
        if (client == null) { Write("authentication-result.json", authentication); return; }
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        refreshToken = null;
        pendingPin = null;
        try
        {
            if (cancellation != null) cancellation.Cancel();
            if (socket != null) { socket.Abort(); socket.Dispose(); authentication.validationSocketDisposed = true; }
            if (sending != null && (sendTask == null || sendTask.IsCompleted)) Array.Clear(sending, 0, sending.Length);
            while (sends.Count > 0) { byte[] bytes = sends.Dequeue(); Array.Clear(bytes, 0, bytes.Length); }
            // The SDK's own transport was never started. Release this test's idle
            // legacy transport/hooks and native instance without claiming that
            // the SDK currently exposes a supported disposal API.
            if (client != null)
            {
                var transport = (WebsocketTransport)typeof(SDK).GetField("Transport", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(client);
                RemoveIdleTransportHooks(transport);
                var idleSocket = (ClientWebSocket)typeof(WebsocketTransport).GetField("Websocket", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transport);
                idleSocket.Dispose();
                var idleCancellation = (CancellationTokenSource)typeof(WebsocketTransport).GetField("UnboundedCancellationSource", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(transport);
                idleCancellation.Cancel(); idleCancellation.Dispose();
                GC.SuppressFinalize(transport);
                GC.SuppressFinalize(client);
                Imported.MGW_SDK_DetachOnDebugMessage(native);
                Imported.MGW_KillSDK(native);
                var handle = (GCHandle)typeof(SDK).GetField("DebugMessage", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(client);
                if (handle.IsAllocated) handle.Free();
                typeof(SDK).GetField("DebugMessage", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(client, default(GCHandle));
                typeof(SDK).GetField("Instance", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(client, default(GatewaySDK));
                authentication.nativeSdkReleased = true;
                client = null;
            }
        }
        catch (Exception) { authentication.status = "failed"; authentication.failureCategory = "validation-cleanup"; }
        finally
        {
            if (cancellation != null) cancellation.Dispose();
            receiveTask = null; sendTask = null; sending = null;
            Write("authentication-result.json", authentication);
            WriteState();
        }
    }

    private static void RemoveIdleTransportHooks(WebsocketTransport transport)
    {
        // Rebuild delegates belonging only to this idle transport, then remove
        // them through public events. No other Editor subscriptions are edited.
        foreach (MethodInfo method in typeof(WebsocketTransport).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
        {
            if (!method.Name.StartsWith("<.ctor>")) continue;
            ParameterInfo[] parameters = method.GetParameters();
            if (parameters.Length == 1 && parameters[0].ParameterType == typeof(PlayModeStateChange))
                EditorApplication.playModeStateChanged -= (Action<PlayModeStateChange>)Delegate.CreateDelegate(typeof(Action<PlayModeStateChange>), transport, method);
            else if (parameters.Length == 0)
            {
                Action callback = (Action)Delegate.CreateDelegate(typeof(Action), transport, method);
                EditorApplication.quitting -= callback;
                Application.quitting -= callback;
            }
        }
    }

    private static void BeforeReload()
    {
        if (client == null) return;
        authentication.status = "failed";
        authentication.failureCategory = "editor-reload-during-validation";
        Finish();
    }

    private static void ValidateUtf8()
    {
        const string text = "Gateway caf\u00e9 \u65e5\u672c\u8a9e \ud83d\ude80";
        var value = new MGW_GameMetadata { GameName = text, GameLogo = "", Theme = "" };
        IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MGW_GameMetadata)));
        bool initialized = false;
        try
        {
            Marshal.StructureToPtr(value, memory, false); initialized = true;
            IntPtr name = Marshal.ReadIntPtr(memory, Marshal.OffsetOf(typeof(MGW_GameMetadata), "GameName").ToInt32());
            byte[] bytes = new byte[Encoding.UTF8.GetByteCount(text)]; Marshal.Copy(name, bytes, 0, bytes.Length);
            if (Encoding.UTF8.GetString(bytes) != text || Marshal.ReadByte(name, bytes.Length) != 0) throw new InvalidOperationException();
            if (((MGW_GameMetadata)Marshal.PtrToStructure(memory, typeof(MGW_GameMetadata))).GameName != text) throw new InvalidOperationException();
        }
        finally { if (initialized) Marshal.DestroyStructure(memory, typeof(MGW_GameMetadata)); Marshal.FreeHGlobal(memory); }
    }

    private static string Now() { return DateTime.UtcNow.ToString("o"); }
    private static void WriteState() { if (authentication != null) { if (authentication.status != "passed" && authentication.status != "failed") authentication.status = phase; Write("state.json", authentication); } }
    private static void Write(string name, Report report) { if (settings != null) File.WriteAllText(Path.Combine(settings.outputDirectory, name), JsonUtility.ToJson(report, true)); }
}
