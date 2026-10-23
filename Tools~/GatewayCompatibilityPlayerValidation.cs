// Compatibility-only player smoke test. No SDK transport/lifecycle API is used.
using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using MuxyGateway;
using MuxyGateway.Imports;
using MuxyGateway.Imports.Schema;
using UnityEngine;

public static class GatewayCompatibilityPlayerValidation
{
    [Serializable] private class Report
    {
        public string unityVersion, backend, timestampUtc, status = "failed", failure;
        public string scope = "compatibility-only-stripped-player-native-utf8-and-TLS-configuration";
        public bool publicTypes, structUtf8, nativeLoaded, nativeUtf8Payload, configurationInitialized;
    }
    private static Report report;
    private const string Text = "Gateway caf\u00e9 \u65e5\u672c\u8a9e \ud83d\ude80";
    private static readonly GatewayForeachPayloadDelegate Collect = OnPayload;

    [AOT.MonoPInvokeCallback(typeof(GatewayForeachPayloadDelegate))]
    private static void OnPayload(IntPtr data, IntPtr pointer)
    {
        var payload = Marshal.PtrToStructure<MGW_Payload>(pointer);
        var bytes = new byte[(int)payload.Length];
        Marshal.Copy(payload.Bytes, bytes, 0, bytes.Length);
        report.nativeUtf8Payload |= Encoding.UTF8.GetString(bytes).Contains(Text);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Run()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "-gatewayCompatibilityResult");
        if (index < 0) return;
        report = new Report { unityVersion = Application.unityVersion, timestampUtc = DateTime.UtcNow.ToString("o") };
#if ENABLE_IL2CPP
        report.backend = "IL2CPP";
#else
        report.backend = "Mono";
#endif
        GatewaySDK native = default(GatewaySDK);
        try
        {
            report.publicTypes = true;
            foreach (Type type in new[] { typeof(SDK), typeof(GameMetadata), typeof(MuxyGatewayManager), typeof(MuxyGatewayAuthentication), typeof(MuxyGatewayPolling), typeof(MuxyGatewayNotificationHistory) })
                report.publicTypes &= type.Assembly.GetName().Name == "Muxy.Gateway";
            var metadata = new MGW_GameMetadata { GameName = Text, GameLogo = "", Theme = "" };
            IntPtr memory = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(MGW_GameMetadata)));
            bool initialized = false;
            try
            {
                Marshal.StructureToPtr(metadata, memory, false); initialized = true;
                IntPtr name = Marshal.ReadIntPtr(memory, Marshal.OffsetOf(typeof(MGW_GameMetadata), "GameName").ToInt32());
                byte[] expected = Encoding.UTF8.GetBytes(Text), actual = new byte[expected.Length];
                Marshal.Copy(name, actual, 0, actual.Length);
                report.structUtf8 = Encoding.UTF8.GetString(actual) == Text && Marshal.ReadByte(name, actual.Length) == 0;
            }
            finally { if (initialized) Marshal.DestroyStructure(memory, typeof(MGW_GameMetadata)); Marshal.FreeHGlobal(memory); }

            // Create/destroy through the public C ABI; do not instantiate the
            // original managed SDK's transport or rely on the lifecycle PR.
            native = Imported.MGW_MakeSDK("gamelink-testing");
            report.nativeLoaded = native.SDK != IntPtr.Zero;
            Imported.MGW_SDK_SetGameMetadata(native, metadata);
            Imported.MGW_SDK_ForeachPayload(native, Collect, IntPtr.Zero);
            // Mono initializes reflection-loaded network configuration here.
            var protocols = ServicePointManager.SecurityProtocol;
            report.configurationInitialized = true;
            if (report.publicTypes && report.structUtf8 && report.nativeLoaded && report.nativeUtf8Payload && report.configurationInitialized) report.status = "passed";
        }
        catch (Exception error) { report.failure = error.ToString(); }
        finally { if (native.SDK != IntPtr.Zero) Imported.MGW_KillSDK(native); }
        File.WriteAllText(args[index + 1], JsonUtility.ToJson(report, true));
        Application.Quit(report.status == "passed" ? 0 : 1);
    }
}
