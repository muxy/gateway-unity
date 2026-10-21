// Test-player entry point. Copy to Assets/GatewayPlayerValidation/Runtime.
// Uses synthetic protocol fixtures offline, then optionally the normal live SDK flow.
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;
using MuxyGateway;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GatewayPlayerValidation : MonoBehaviour
{
    [Serializable] public class Settings
    {
        public string outputDirectory, gameId = "gamelink-testing", mode = "offline";
        public int cycles = 100;
    }
    [Serializable] private class PinRequest { public string pin, generatedAtUtc; }
    [Serializable] private class RequestEnvelope { public string action; public RequestParameters @params; }
    [Serializable] private class RequestParameters { public int request_id; }
    [Serializable] private class WireEnvelope { public WireMeta meta; public WireError[] errors; }
    [Serializable] private class WireMeta { public string action; }
    [Serializable] private class WireError { public int number; }
    [Serializable] private class Report
    {
        public string unityVersion, backend, platform, timestampUtc, mode, status, failureStep, exceptionType, failureDetail;
        public int pointerSize, cycles, actionCallbacks, bitsCallbacks, pollCallbacks;
        public bool nativeLoaded, metadataUtf8, syntheticPin, syntheticRefresh, nativePoll, nativeAction, nativeBits, actionUnityEvent;
        public bool callbacksOnMainThread = true, disposalCycles, nativeDisposed, sceneCleanup;
        public long managedBytesBefore, managedBytesAfter, privateBytesBefore, privateBytesAfter;
        public int handlesBefore, handlesAfter;
        public bool processMetricsAvailable;
        public int componentCycles;
        public bool componentLifecycle;
        public bool livePin, liveRefresh, liveReopenRefresh, stopped, reopened, disposedInCallback;
        public bool authenticatedNetworkDrop, automaticReconnect, automaticReauthentication;
        public string pinRequestedAtUtc, pinSubmittedAtUtc, pinCallbackAtUtc, refreshCallbackAtUtc, reopenRefreshCallbackAtUtc;
        public double pinAgeSeconds, stopMilliseconds;
    }

    private Settings settings;
    private Report report;
    private SDK client;
    private Task operation;
    private string phase, pin, refresh;
    private double deadline;
    private int owningThread;
    private System.Diagnostics.Stopwatch stopWatch;
    private object droppedConnection, reconnectedConnection;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-gatewayPlayerSettings");
        if (index < 0 || index + 1 >= args.Length) return;
        var owner = new GameObject("Gateway player validation");
        DontDestroyOnLoad(owner);
        var probe = owner.AddComponent<GatewayPlayerValidation>();
        probe.Initialize(args[index + 1]);
    }

    private void Initialize(string settingsPath)
    {
        settings = JsonUtility.FromJson<Settings>(File.ReadAllText(settingsPath));
        Directory.CreateDirectory(settings.outputDirectory);
        owningThread = Thread.CurrentThread.ManagedThreadId;
        Application.runInBackground = true;
        report = new Report { unityVersion = Application.unityVersion, platform = Application.platform.ToString(), timestampUtc = Now(), mode = settings.mode, status = "running", pointerSize = IntPtr.Size };
#if ENABLE_IL2CPP
        report.backend = "IL2CPP";
#else
        report.backend = "Mono";
#endif
        StartCoroutine(Guard(Validate()));
    }

    private IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            bool more;
            try { more = routine.MoveNext(); }
            catch (Exception error) { Fail(error); yield break; }
            if (!more) yield break;
            yield return routine.Current;
        }
    }

    private IEnumerator Validate()
    {
        phase = "native-callbacks"; State();
        try { ValidateCallbacks(); }
        catch (Exception error) { Fail(error); yield break; }
        // Warm managed/native allocation paths before recording repeated-cycle deltas.
        for (int i = 0; i < 10; i++) { using (var warm = new SDK(settings.gameId)) { warm.OnBitsUsed(used => { }); } }
        Require(!PlayerPrefs.HasKey("MuxyGatewayRefreshToken"), "fixture-profile-must-have-no-stored-token");
        GameObject componentObject = MakeManagerObject();
        var manager = componentObject.GetComponent<MuxyGatewayManager>();
        FieldInfo sdkField = typeof(MuxyGatewayManager).GetField("SDK", BindingFlags.Instance | BindingFlags.NonPublic);
        SDK componentSdk = (SDK)sdkField.GetValue(manager);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        yield return null;
        CaptureMemory(false);
        if (settings.mode == "soak")
        {
            phase = "soak-baseline"; State();
            yield return new WaitUntil(() => File.Exists(Path.Combine(settings.outputDirectory, "continue-soak")));
        }
        phase = "repeated-disposal"; State();
        for (int i = 0; i < settings.cycles; i++)
        {
            try
            {
                using (var repeated = new SDK(settings.gameId))
                {
                    repeated.OnGameActionUsed(used => { }); repeated.OnGameActionUsed(used => { });
                    repeated.OnBitsUsed(used => { }); repeated.OnBitsUsed(used => { });
                    repeated.AuthenticateWithPIN("000000", response => { }); // Queued only; no socket exists.
                    repeated.Dispose(); repeated.Dispose();
                    Require(repeated.IsDisposed && !repeated.IsConnected, "repeated-disposal");
                }
                report.cycles++;
                componentObject.SetActive(false);
                Require(componentSdk.IsDisposed, "component-disable-disposal");
                componentObject.SetActive(true);
                SDK next = (SDK)sdkField.GetValue(manager);
                Require(next != componentSdk && !next.IsDisposed, "component-enable-recreates");
                componentSdk = next; report.componentCycles++;
            }
            catch (Exception error) { Fail(error); yield break; }
            if (i % 10 == 0) yield return null;
            if (settings.mode == "soak" && report.cycles == settings.cycles / 2)
            {
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                phase = "soak-midpoint"; State();
                yield return new WaitUntil(() => File.Exists(Path.Combine(settings.outputDirectory, "continue-soak-second-half")));
            }
        }
        Destroy(componentObject);
        yield return null;
        report.componentLifecycle = componentSdk.IsDisposed && report.componentCycles == settings.cycles;
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        yield return null;
        CaptureMemory(true);
        report.disposalCycles = report.cycles == settings.cycles;
        if (settings.mode == "soak")
        {
            phase = "soak-completed"; State();
            yield return new WaitUntil(() => File.Exists(Path.Combine(settings.outputDirectory, "finish-soak")));
        }

        phase = "scene-cleanup"; State();
        Scene temporary = SceneManager.CreateScene("Gateway temporary scene");
        var sceneObject = MakeManagerObject();
        SceneManager.MoveGameObjectToScene(sceneObject, temporary);
        SDK sceneSdk = (SDK)sdkField.GetValue(sceneObject.GetComponent<MuxyGatewayManager>());
        yield return SceneManager.UnloadSceneAsync(temporary);
        report.sceneCleanup = sceneSdk.IsDisposed && !sceneSdk.IsConnected;
        if (settings.mode == "live")
        {
            client = new SDK(settings.gameId);
            SetPhase("waiting-for-pin", 600);
        }
        else Finish();
    }

    private GameObject MakeManagerObject()
    {
        var owner = new GameObject("Gateway prefab lifecycle validation");
        owner.SetActive(false);
        var manager = owner.AddComponent<MuxyGatewayManager>();
        manager.GameID = settings.gameId;
        owner.SetActive(true);
        return owner;
    }

    private void ValidateCallbacks()
    {
        using (var sdk = new SDK(settings.gameId))
        {
            report.nativeLoaded = !String.IsNullOrEmpty(sdk.GetProductionURL());
            sdk.AuthenticateWithPIN("000000", response =>
            {
                OnCallback(); report.syntheticPin = !response.HasError && sdk.IsAuthenticated && response.JWT == "local-test-jwt";
            });
            ReplyToAuth(sdk);
            sdk.AuthenticateWithRefreshToken("local-test-refresh", response =>
            {
                OnCallback(); report.syntheticRefresh = !response.HasError && sdk.IsAuthenticated && response.RefreshToken == "local-test-refresh";
            });
            ReplyToAuth(sdk);
            const string unicode = "Gateway caf\u00e9 \u65e5\u672c\u8a9e \ud83d\ude80";
            sdk.SetGameMetadata(new GameMetadata { Name = unicode, Logo = "", Theme = "" });
            Drain(sdk, text => { if (text.Contains(unicode)) report.metadataUtf8 = true; });
            var action = new GameAction { ID = "validation-action", Name = unicode, Description = "Local callback fixture", Icon = "", Count = 5, State = GameActionState.Available, OnGameActionUsed = new MuxyGatewayGameActionUsedEvent() };
            action.OnGameActionUsed.AddListener(used => { OnCallback(); report.actionUnityEvent = used.ActionID == action.ID; });
            sdk.SetGameActions(new[] { action });
            sdk.OnGameActionUsed(used =>
            {
                OnCallback(); report.actionCallbacks++;
                report.nativeAction = used.ActionID == "validation-action" && used.Cost == 7 && used.Username == "caf\u00e9";
            });
            sdk.OnBitsUsed(used =>
            {
                OnCallback(); report.bitsCallbacks++;
                report.nativeBits = used.SKU == "validation-action" && used.Bits == 11 && used.Username == "caf\u00e9";
            });
            Drain(sdk, text => { });
            sdk.ReceiveMessage(Transaction("coins", 7));
            sdk.ReceiveMessage(Transaction("bits", 11));
            sdk.StartPoll(new PollConfiguration
            {
                Prompt = unicode, Options = new List<string> { "Yes", "No" }, DurationInSeconds = 3,
                OnPollUpdate = update =>
                {
                    OnCallback(); report.pollCallbacks++;
                    report.nativePoll = update.IsFinal && update.Results.Count == 2 && update.Results[0] == 1 && update.Results[1] == 3 && update.Winner == 1 && update.Count == 4;
                }
            });
            Drain(sdk, text => { });
            sdk.ReceiveMessage("{\"meta\":{\"action\":\"update\",\"target\":\"poll\"},\"data\":{\"count\":4,\"mean\":0.0,\"sum\":0.0,\"results\":[1,3],\"poll\":{\"poll_id\":\"default\",\"prompt\":\"Validation\",\"options\":[\"Yes\",\"No\"],\"status\":\"expired\"}}}");
            sdk.StopPoll();
            Drain(sdk, text => { });
            Require(report.nativeLoaded && report.metadataUtf8 && report.syntheticPin && report.syntheticRefresh && report.nativeAction && report.nativeBits && report.nativePoll && report.actionUnityEvent && report.callbacksOnMainThread, "native-callbacks");
            sdk.Dispose(); report.nativeDisposed = sdk.IsDisposed;
        }
    }

    private static string Transaction(string currency, int cost)
    {
        return "{\"meta\":{\"action\":\"update\",\"target\":\"transaction\"},\"data\":{\"id\":\"local-id\",\"muxy_id\":\"local-muxy-id\",\"sku\":\"validation-action\",\"displayName\":\"Validation\",\"userId\":\"local-user\",\"username\":\"caf\u00e9\",\"cost\":" + cost + ",\"currency\":\"" + currency + "\",\"timestamp\":1,\"additional\":{}}}";
    }

    private static void ReplyToAuth(SDK sdk)
    {
        int request = -1;
        sdk.ForeachPayload(payload =>
        {
            var envelope = JsonUtility.FromJson<RequestEnvelope>(Encoding.UTF8.GetString(payload.Bytes));
            if (envelope.action == "authenticate") request = envelope.@params.request_id;
            Array.Clear(payload.Bytes, 0, payload.Bytes.Length);
        });
        Require(request >= 0, "synthetic-auth-request");
        sdk.ReceiveMessage("{\"meta\":{\"request_id\":" + request + ",\"action\":\"authenticate\"},\"data\":{\"jwt\":\"local-test-jwt\",\"refresh\":\"local-test-refresh\",\"twitch_name\":\"local-test\",\"twitch_id\":\"local-user\"}}");
    }

    private static void Drain(SDK sdk, Action<string> observe)
    {
        for (int i = 0; i < 20; i++)
        {
            var requests = new List<int>();
            sdk.ForeachPayload(payload =>
            {
                string text = Encoding.UTF8.GetString(payload.Bytes); observe(text);
                var envelope = JsonUtility.FromJson<RequestEnvelope>(text);
                if (envelope.@params != null) requests.Add(envelope.@params.request_id);
                Array.Clear(payload.Bytes, 0, payload.Bytes.Length);
            });
            if (requests.Count == 0) return;
            foreach (int request in requests) sdk.ReceiveMessage("{\"meta\":{\"request_id\":" + request + ",\"action\":\"nothing\"}}");
        }
        throw new InvalidOperationException("native-queue-did-not-drain");
    }

    private void Update()
    {
        if (report == null || report.status != "running" || client == null) return;
        try
        {
            if (phase == "waiting-for-pin")
            {
                string path = Path.Combine(settings.outputDirectory, "pin-request.json");
                if (File.Exists(path))
                {
                    PinRequest request;
                    try { request = JsonUtility.FromJson<PinRequest>(File.ReadAllText(path)); File.Delete(path); }
                    catch (IOException) { return; }
                    pin = request.pin; report.pinRequestedAtUtc = request.generatedAtUtc;
                    operation = client.RunInProductionAsync(); SetPhase("connecting");
                }
            }
            if (phase == "connecting" && operation.IsCompleted)
            {
                operation.GetAwaiter().GetResult(); Require(client.IsConnected, "connect-state");
                report.pinSubmittedAtUtc = Now();
                report.pinAgeSeconds = (DateTime.UtcNow - DateTime.Parse(report.pinRequestedAtUtc).ToUniversalTime()).TotalSeconds;
                SetPhase("pin"); client.AuthenticateWithPIN(pin, OnPin); pin = null;
            }
            if (phase == "refresh-pending")
            {
                string value = refresh; refresh = null; SetPhase("refresh"); client.AuthenticateWithRefreshToken(value, OnRefresh);
            }
            if (phase == "stop-pending")
            {
                stopWatch = System.Diagnostics.Stopwatch.StartNew(); operation = client.StopWebsocketTransportAsync(); SetPhase("stop");
            }
            if (phase == "network-drop-pending")
            {
                droppedConnection = CurrentConnection();
                Require(droppedConnection != null && client.IsAuthenticated, "network-drop-precondition");
                droppedConnection.GetType().GetMethod("Abort", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(droppedConnection, null);
                report.authenticatedNetworkDrop = true;
                SetPhase("network-reconnect");
            }
            if (phase == "network-reconnect" && client.IsConnected)
            {
                object current = CurrentConnection();
                if (current != null && !ReferenceEquals(current, droppedConnection))
                {
                    reconnectedConnection = current; report.automaticReconnect = true;
                    client.Update(); // Dispatch the SDK's native HandleReconnect and queue its own refresh.
                    SetPhase("network-reauth-response");
                }
            }
            if (phase == "network-reauth-response")
            {
                // Observe only action/error metadata; leave the original response
                // queued for SDK.Update and never store raw credential payloads.
                var incoming = (System.Collections.Concurrent.ConcurrentQueue<string>)Field(reconnectedConnection, "Incoming");
                if (incoming.TryPeek(out string message))
                {
                    var envelope = JsonUtility.FromJson<WireEnvelope>(message);
                    Require(envelope.meta?.action == "authenticate" && (envelope.errors == null || envelope.errors.Length == 0), "automatic-reauth-response");
                    client.Update();
                    report.automaticReauthentication = client.IsAuthenticated;
                    SetPhase("stop-pending");
                }
            }
            if (phase == "stop" && operation.IsCompleted)
            {
                operation.GetAwaiter().GetResult(); stopWatch.Stop(); report.stopMilliseconds = stopWatch.Elapsed.TotalMilliseconds;
                report.stopped = !client.IsConnected; operation = client.RunInProductionAsync(); SetPhase("reopen");
            }
            if (phase == "reopen" && operation.IsCompleted)
            {
                operation.GetAwaiter().GetResult(); report.reopened = client.IsConnected;
                string value = refresh; refresh = null; SetPhase("reopen-refresh"); client.AuthenticateWithRefreshToken(value, OnReopenRefresh);
            }
            if (phase != "network-reauth-response") client.Update();
            if (phase == "finish") { Finish(); return; }
            if (Time.realtimeSinceStartup > deadline) throw new TimeoutException();
        }
        catch (Exception error) { Fail(error); }
    }

    private bool AuthPassed(AuthenticationResponse response)
    {
        OnCallback(); return !response.HasError && client.IsAuthenticated && !String.IsNullOrEmpty(response.JWT) && !String.IsNullOrEmpty(response.RefreshToken);
    }
    private void OnPin(AuthenticationResponse response)
    {
        report.livePin = AuthPassed(response); report.pinCallbackAtUtc = Now(); refresh = response.RefreshToken;
        response.JWT = response.RefreshToken = response.TwitchUsername = null;
        if (!report.livePin) { Fail(new InvalidOperationException("pin-incomplete")); return; } SetPhase("refresh-pending");
    }
    private void OnRefresh(AuthenticationResponse response)
    {
        report.liveRefresh = AuthPassed(response); report.refreshCallbackAtUtc = Now(); refresh = response.RefreshToken;
        response.JWT = response.RefreshToken = response.TwitchUsername = null;
        if (!report.liveRefresh) { Fail(new InvalidOperationException("refresh-incomplete")); return; } SetPhase("network-drop-pending");
    }
    private void OnReopenRefresh(AuthenticationResponse response)
    {
        report.liveReopenRefresh = AuthPassed(response); report.reopenRefreshCallbackAtUtc = Now();
        response.JWT = response.RefreshToken = response.TwitchUsername = null;
        client.Dispose(); report.disposedInCallback = client.IsDisposed && !client.IsConnected; phase = "finish";
    }
    private void OnCallback() { report.callbacksOnMainThread &= Thread.CurrentThread.ManagedThreadId == owningThread; }
    private static object Field(object owner, string name)
    {
        return owner?.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(owner);
    }
    private object CurrentConnection() { return Field(Field(Field(client, "Transport"), "Current"), "Connection"); }
    private void CaptureMemory(bool after)
    {
        long managed = GC.GetTotalMemory(true);
        if (after) report.managedBytesAfter = managed; else report.managedBytesBefore = managed;
        try
        {
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                process.Refresh();
                long bytes = process.PrivateMemorySize64; int handles = process.HandleCount;
                report.processMetricsAvailable = bytes > 0 && handles > 0;
                if (after) { report.privateBytesAfter = bytes; report.handlesAfter = handles; }
                else { report.privateBytesBefore = bytes; report.handlesBefore = handles; }
            }
        }
        catch (NotSupportedException) { report.processMetricsAvailable = false; }
    }
    private void Finish()
    {
        client?.Dispose(); pin = refresh = null;
        bool live = settings.mode != "live" || (report.livePin && report.liveRefresh && report.authenticatedNetworkDrop && report.automaticReconnect && report.automaticReauthentication && report.liveReopenRefresh && report.stopped && report.reopened && report.disposedInCallback);
        report.status = String.IsNullOrEmpty(report.failureStep) && report.disposalCycles && report.componentLifecycle && report.sceneCleanup && live ? "passed" : "failed";
        File.WriteAllText(Path.Combine(settings.outputDirectory, "result.json"), JsonUtility.ToJson(report, true));
        Application.Quit(report.status == "passed" ? 0 : 1);
    }
    private void Fail(Exception error)
    {
        report.failureStep = phase; report.exceptionType = error.GetType().Name;
        // Connection establishment carries no application credentials.
        report.failureDetail = phase == "connecting" ? error.ToString() : error.Message;
        Finish();
    }
    private void SetPhase(string value, int seconds = 45) { phase = value; deadline = Time.realtimeSinceStartup + seconds; State(); }
    private void State() { File.WriteAllText(Path.Combine(settings.outputDirectory, "state.json"), "{\"phase\":\"" + phase + "\",\"unityVersion\":\"" + Application.unityVersion + "\",\"backend\":\"" + report.backend + "\"}"); }
    private static void Require(bool value, string label) { if (!value) throw new InvalidOperationException(label); }
    private static string Now() { return DateTime.UtcNow.ToString("o"); }
    private void OnDestroy() { client?.Dispose(); }
}
