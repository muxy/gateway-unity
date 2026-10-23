// Copy into a disposable test project's Assets/.../Editor directory.
// Uses the shipping SDK transport exclusively. Reflection below only observes cleanup.
using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using MuxyGateway;
using MuxyGateway.Imports.Schema;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class GatewayShippingTransportValidation
{
    [Serializable] public class Settings { public string outputDirectory; public string gameId; }
    [Serializable] private class Request { public string pin; public string generatedAtUtc; }
    [Serializable] private class Report
    {
        public string unityVersion, timestampUtc, status, scope = "shipping-sdk-production-connection-pin-refresh-stop-reopen-dispose";
        public string gameId, scheme, host, pinGeneratedAtUtc, pinSubmittedAtUtc, pinCallbackAtUtc;
        public double pinAgeAtSubmissionSeconds, shutdownMilliseconds;
        public bool connectionAwaited, debugLoggingDisabled, pinPassed, refreshPassed, reopenRefreshPassed;
        public bool consumerCompilation, publicTypesPreserved;
        public string tmpProvider, uiProvider;
        public bool pinJwtPresent, pinRefreshTokenPresent, callbacksOnMainThread = true;
        public bool stopCompleted, repeatedStopCompleted, reopened, disposedInCallback;
        public bool nativeReleased, handlesReleased, socketTasksCompleted, repeatedDisposeCompleted, disposedUseRejected;
        public string refreshCallbackAtUtc, reopenRefreshCallbackAtUtc, failureCategory, exceptionType;
    }

    private static readonly string Controls = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/GatewayShippingTransportValidation"));
    private static SDK client;
    private static Settings settings;
    private static Report report;
    private static string phase, pin, refresh;
    private static Task operation;
    private static double deadline;
    private static int mainThread;
    private static bool batch;
    private static System.Diagnostics.Stopwatch shutdown;

    static GatewayShippingTransportValidation()
    {
        EditorApplication.delayCall += Initialize;
        AssemblyReloadEvents.beforeAssemblyReload += BeforeReload;
    }

    public static void RunBatch() { batch = true; Initialize(); }

    private static void Initialize()
    {
        if (report != null) return;
        string path = Path.Combine(Controls, "settings.json");
        string[] arguments = Environment.GetCommandLineArgs();
        int settingsIndex = Array.IndexOf(arguments, "-gatewayTransportSettings");
        if (settingsIndex >= 0 && settingsIndex + 1 < arguments.Length) path = arguments[settingsIndex + 1];
        if (!File.Exists(path)) return;
        settings = JsonUtility.FromJson<Settings>(File.ReadAllText(path));
        Directory.CreateDirectory(settings.outputDirectory);
        Directory.CreateDirectory(Controls);
        if (File.Exists(Path.Combine(settings.outputDirectory, "result.json"))) return;
        mainThread = Thread.CurrentThread.ManagedThreadId;
        report = new Report { unityVersion = Application.unityVersion, timestampUtc = Now(), status = "running", gameId = settings.gameId };
        try
        {
            report.consumerCompilation = typeof(GatewayPackageConsumer).Assembly.GetName().Name == "Gateway.PackageConsumer";
            report.publicTypesPreserved = GatewayPackageConsumer.PublicTypes.Length == 6;
            foreach (Type type in GatewayPackageConsumer.PublicTypes) report.publicTypesPreserved &= type.Assembly.GetName().Name == "Muxy.Gateway";
            var tmp = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
            var ui = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UnityEngine.UI.Graphic).Assembly);
            report.tmpProvider = tmp.name + "@" + tmp.version; report.uiProvider = ui.name + "@" + ui.version;
            client = new SDK(settings.gameId);
            report.debugLoggingDisabled = !client.DebugLoggingEnabled;
            phase = "waiting-for-pin";
            EditorApplication.update += Tick;
            State();
        }
        catch (Exception error) { Fail("initialize", error); }
    }

    private static void Tick()
    {
        try
        {
            if (phase == "finish") { Finish(); return; }
            if (phase == "waiting-for-pin")
            {
                string path = Path.Combine(Controls, "pin-request.json");
                if (!File.Exists(path)) return;
                Request request;
                try
                {
                    request = JsonUtility.FromJson<Request>(File.ReadAllText(path));
                    File.Delete(path);
                }
                catch (IOException) { return; } // Retry a request still held by its publisher.
                pin = (request.pin ?? "").Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(pin, "^[A-Z0-9]{6}$")) throw new InvalidDataException();
                report.pinGeneratedAtUtc = request.generatedAtUtc;
                operation = client.RunInProductionAsync();
                SetPhase("connect");
            }
            if (phase == "connect" && operation.IsCompleted)
            {
                operation.GetAwaiter().GetResult();
                report.connectionAwaited = client.IsConnected;
                ObserveRoute();
                report.pinSubmittedAtUtc = Now();
                report.pinAgeAtSubmissionSeconds = (DateTime.UtcNow - DateTime.Parse(report.pinGeneratedAtUtc).ToUniversalTime()).TotalSeconds;
                SetPhase("pin");
                client.AuthenticateWithPIN(pin, OnPin);
                pin = null;
            }
            if (phase == "refresh-pending")
            {
                string token = refresh; refresh = null;
                SetPhase("refresh");
                client.AuthenticateWithRefreshToken(token, OnRefresh);
            }
            if (phase == "stop-pending")
            {
                shutdown = System.Diagnostics.Stopwatch.StartNew();
                operation = client.StopWebsocketTransportAsync();
                SetPhase("stop");
            }
            if (phase == "stop" && operation.IsCompleted)
            {
                operation.GetAwaiter().GetResult();
                shutdown.Stop(); report.shutdownMilliseconds = shutdown.Elapsed.TotalMilliseconds;
                report.stopCompleted = !client.IsConnected;
                report.repeatedStopCompleted = client.StopWebsocketTransportAsync().IsCompleted;
                operation = client.RunInProductionAsync();
                SetPhase("reopen");
            }
            if (phase == "reopen" && operation.IsCompleted)
            {
                operation.GetAwaiter().GetResult();
                report.reopened = client.IsConnected;
                string token = refresh; refresh = null;
                SetPhase("reopen-refresh");
                client.AuthenticateWithRefreshToken(token, OnReopenRefresh);
            }
            if (phase != "waiting-for-pin" && EditorApplication.timeSinceStartup > deadline) throw new TimeoutException();
            client.Update();
            State();
        }
        catch (Exception error) { Fail(phase, error); }
    }

    private static bool AuthPassed(AuthenticationResponse response)
    {
        report.callbacksOnMainThread &= Thread.CurrentThread.ManagedThreadId == mainThread;
        return !response.HasError && client.IsAuthenticated && !String.IsNullOrEmpty(response.JWT) && !String.IsNullOrEmpty(response.RefreshToken);
    }

    private static void OnPin(AuthenticationResponse response)
    {
        report.pinCallbackAtUtc = Now();
        report.pinPassed = AuthPassed(response);
        report.pinJwtPresent = !String.IsNullOrEmpty(response.JWT);
        report.pinRefreshTokenPresent = !String.IsNullOrEmpty(response.RefreshToken);
        refresh = response.RefreshToken;
        response.JWT = response.RefreshToken = response.TwitchUsername = null;
        if (!report.pinPassed) { Fail("pin-rejected-or-incomplete", null); return; }
        SetPhase("refresh-pending");
    }

    private static void OnRefresh(AuthenticationResponse response)
    {
        report.refreshCallbackAtUtc = Now();
        report.refreshPassed = AuthPassed(response);
        refresh = response.RefreshToken;
        response.JWT = response.RefreshToken = response.TwitchUsername = null;
        if (!report.refreshPassed) { Fail("refresh-rejected-or-incomplete", null); return; }
        SetPhase("stop-pending");
    }

    private static void OnReopenRefresh(AuthenticationResponse response)
    {
        report.reopenRefreshCallbackAtUtc = Now();
        report.reopenRefreshPassed = AuthPassed(response);
        response.JWT = response.RefreshToken = response.TwitchUsername = null;
        client.Dispose(); // Exercise reentrant disposal from the real native callback.
        report.disposedInCallback = client.IsDisposed && !client.IsConnected;
        phase = "finish";
    }

    private static object Field(object owner, string name)
    {
        return owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(owner);
    }

    private static void ObserveRoute()
    {
        object transport = Field(client, "Transport");
        var target = (Uri)Field(Field(transport, "Current"), "Target");
        report.scheme = target.Scheme; report.host = target.Host;
        if (target.Scheme != "wss" || target.Host != "gamelink.muxy.io") throw new InvalidOperationException();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        pin = refresh = null;
        try
        {
            client?.Dispose();
            if (client != null)
            {
                report.repeatedDisposeCompleted = client.IsDisposed;
                report.nativeReleased = ((GatewaySDK)Field(client, "Instance")).SDK == IntPtr.Zero;
                report.handlesReleased = ((IDictionary)Field(client, "AuthenticationHandles")).Count == 0;
                foreach (string name in new[] { "DebugMessage", "PollDelegateHandle", "GameActionUsedDelegateHandle", "BitsUsedDelegateHandle" })
                    report.handlesReleased &= !((GCHandle)Field(client, name)).IsAllocated;
                object transport = Field(client, "Transport");
                report.socketTasksCompleted = Field(transport, "Current") == null && ((Task)Field(transport, "Stopping")).IsCompleted;
                try { client.RunInProductionAsync(); }
                catch (ObjectDisposedException) { report.disposedUseRejected = true; }
            }
            report.status = String.IsNullOrEmpty(report.failureCategory) && report.consumerCompilation && report.publicTypesPreserved && report.pinPassed && report.refreshPassed && report.reopenRefreshPassed && report.callbacksOnMainThread && report.stopCompleted && report.repeatedStopCompleted && report.reopened && report.disposedInCallback && report.nativeReleased && report.handlesReleased && report.socketTasksCompleted && report.disposedUseRejected ? "passed" : "failed";
        }
        catch (Exception error) { report.status = "failed"; report.failureCategory = "cleanup"; report.exceptionType = error.GetType().Name; }
        File.WriteAllText(Path.Combine(settings.outputDirectory, "result.json"), JsonUtility.ToJson(report, true));
        State();
        if (batch) EditorApplication.Exit(report.status == "passed" ? 0 : 1);
    }

    private static void Fail(string category, Exception error)
    {
        report.failureCategory = category;
        report.exceptionType = error?.GetType().Name;
        phase = "finish";
        EditorApplication.update -= Tick; EditorApplication.update += Tick;
    }
    private static void SetPhase(string value) { phase = value; deadline = EditorApplication.timeSinceStartup + 45; State(); }
    private static void State() { File.WriteAllText(Path.Combine(settings.outputDirectory, "state.json"), JsonUtility.ToJson(new StateValue { phase = phase, unityVersion = Application.unityVersion }, true)); }
    [Serializable] private class StateValue { public string phase, unityVersion; }
    private static string Now() { return DateTime.UtcNow.ToString("o"); }
    private static void BeforeReload() { if (report != null && phase != "finish") { Fail("assembly-reload", null); Finish(); } }
}
