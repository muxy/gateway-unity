using MuxyGateway.Imports;
using MuxyGateway.Imports.Schema;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Reflection;

#if UNITY_EDITOR || UNITY_STANDALONE
using AOT;
using UnityEngine;
using UnityEngine.Events;
#endif

namespace MuxyGateway
{
    public class AuthenticationResponse
    {
        public string JWT { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string TwitchUsername { get; set; } = string.Empty;

        public bool HasError { get; set; } = false;
    }

    public class Payload
    {
        public byte[] Bytes { get; set; } = new byte[0];
    }

    [Serializable]
    public class GameMetadata
    {
        public string Name;
        public string Logo;
        public string Theme;
    }

    [Serializable]
    public class GameText
    {
        public string Label;
        public string Value;
        public string Icon;
    }

    [Serializable]
    public enum GameActionCategory
    {
        Neutral = 0,
        Hinder = 1,
        Help = 2
    }

    [Serializable]
    public enum GameActionState
    {
        Unavailable = 0,
        Available = 1,
        Hidden = 2
    }

    [Serializable]
    public class MuxyGatewayGameActionUsedEvent : UnityEvent<GameActionUsed> { }

    [Serializable]
    public class GameAction
    {
        public static int InfiniteCount = 0xFFFF;

        public string ID;

        public GameActionCategory Category = GameActionCategory.Neutral;
        public GameActionState State = GameActionState.Unavailable;
        [Range(1, 5)] public int Impact;
        public string Name;
        public string Description;
        public string Icon;

        [SerializeField, Range(0, 65535)] public int Count = 65535;

        public MuxyGatewayGameActionUsedEvent OnGameActionUsed;
    }

    [Serializable]
    public class PollUpdate
    {
        public int Winner;
        public int WinningVoteCount;

        public List<int> Results = new List<int>();

        public int Count;
        public double Mean;
        public bool IsFinal = false;
    }

    [Serializable]
    public enum PollLocation
    {
        Default = 0
    }

    [Serializable]
    public enum PollMode
    {
        Chaos = 0,
        Order = 1
    }

    [Serializable]
    public class PollConfiguration
    {
        public static int InfiniteDuration = 0;
        public delegate void OnUpdateDelegate(PollUpdate Update);

        public string Prompt;
        public PollLocation Location = PollLocation.Default;
        public PollMode Mode = PollMode.Order;

        public List<string> Options = new List<string>();

        public Int32 DurationInSeconds = InfiniteDuration;

        public OnUpdateDelegate OnPollUpdate = (Update) => { };
    }

    public class BitsUsed
    {
        public string TransactionID;
        public string SKU;
        public int Bits;
        public string UserID;
        public string Username;
    }

    public class GameActionUsed
    {
        public string TransactionID;
        public string ActionID;
        public int Cost;
        public string UserID;
        public string Username;
    }

    public class SDK
    {
        private GatewaySDK Instance;
        private string GameID;
        private Encoding UTF8WithoutBOM = new UTF8Encoding(false);

        private GCHandle DebugMessage;

        WebsocketTransport Transport;

        public void Update()
        {
            Transport.Update(this);
        }

        public void StopWebsocketTransport()
        {
            Transport.StopAsync();
        }

        public async Task RunInCustomAsync(String uri)
        {
            await Transport.Open(uri);
            Transport.Run(this);
        }

        public void RunInSandbox()
        {
            Transport.OpenAndRunInSandbox(this);
        }

        public void RunInProduction()
        {
            Transport.OpenAndRunInProduction(this);
        }

        private class InvokeOnDebugMessageParameters
        {
            public SDK SDK;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        [MonoPInvokeCallback(typeof(GatewayDebugMessageDelegate))]
#endif
        private static void InvokeOnDebugMessage(IntPtr Data, string Message)
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            GCHandle handle = GCHandle.FromIntPtr(Data);
            if (handle.Target == null)
            {
                return;
            }

            InvokeOnDebugMessageParameters args = handle.Target as InvokeOnDebugMessageParameters;

            if (args == null)
            {
                return;
            }

            if (args.SDK != null)
            {
                args.SDK.LogMessage(Message);
            }
        }

        public SDK(string GameID)
        {
            Instance = Imported.MGW_MakeSDK(GameID);
            this.GameID = GameID;
            this.Transport = new WebsocketTransport();

            InvokeOnDebugMessageParameters args = new InvokeOnDebugMessageParameters();
            args.SDK = this;

            DebugMessage = GCHandle.Alloc(args);
            Imported.MGW_SDK_OnDebugMessage(Instance, InvokeOnDebugMessage, GCHandle.ToIntPtr(DebugMessage));
        }

        ~SDK()
        {
            try
            {
                Transport.StopAsync().Wait();
            }
            finally
            {
                Imported.MGW_KillSDK(Instance);
            }
        }

        private void LogMessage(string Message)
        {
#if UNITY_EDITOR
            Debug.Log(Message);
#elif UNITY_STANDALONE
            Debug.Log(Message);
#else
            Console.Error.WriteLine(Message);
#endif
        }

        #region Network
        public bool ReceiveMessage(String Message)
        {
            if (Message == null)
            {
                return false;
            }

            byte[] Bytes = UTF8WithoutBOM.GetBytes(Message);
            bool result = Imported.MGW_SDK_ReceiveMessage(Instance, Bytes, (uint)Bytes.Length) != 0;
            return result;
        }

        public bool HasPayloads()
        {
            return Imported.MGW_SDK_HasPayloads(Instance) != 0;
        }

        public delegate void ForeachPayloadDelegate(Payload Payload);

        private class InvokeForeachPayloadParameters
        {
            public ForeachPayloadDelegate Callback;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        [MonoPInvokeCallback(typeof(GatewayForeachPayloadDelegate))]
#endif
        private static void InvokeForeachPayload(IntPtr Data, IntPtr Message)
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            GCHandle handle = GCHandle.FromIntPtr(Data);
            if (handle.Target == null)
            {
                return;
            }

            InvokeForeachPayloadParameters args = handle.Target as InvokeForeachPayloadParameters;

            if (args == null)
            {
                return;
            }

            if (args.Callback != null)
            {
                Payload p = new Payload();
                MGW_Payload first = Marshal.PtrToStructure<MGW_Payload>(Message);

                p.Bytes = new byte[first.Length];
                Marshal.Copy(first.Bytes, p.Bytes, 0, (int)first.Length);

                args.Callback(p);
            }
        }

        public void ForeachPayload(ForeachPayloadDelegate Delegate)
        {
            InvokeForeachPayloadParameters args = new InvokeForeachPayloadParameters();
            args.Callback = Delegate;

            GCHandle Handle = GCHandle.Alloc(args);
            Imported.MGW_SDK_ForeachPayload(Instance, InvokeForeachPayload, GCHandle.ToIntPtr(Handle));
            Handle.Free();
        }

        private struct Version
        {
            public int Major;
            public int Minor;
            public int Patch;
        }

        private Version ParseVersion(string str)
        {
            Version result = new Version();

            if (str == null)
            {
                return result;
            }

            string[] parts = str.Split('.');
            if (parts.Length != 3)
            {
                return result;
            }

            if (parts[0].StartsWith("v"))
            {
                parts[0] = parts[0].TrimStart('v');
            }

            try
            {
                result.Major = int.Parse(parts[0]);
                result.Minor = int.Parse(parts[1]);
                result.Patch = int.Parse(parts[2]);
            }
            catch
            {
                result.Major = 0;
                result.Minor = 0;
                result.Patch = 0;
            }

            return result;
        }

        public String GetSandboxURL()
        {
            Version v = ParseVersion(Constants.Version);
            string s = NativeString.StringFromUTF8AndDeallocate(Imported.MGW_SDK_GetProjectionSandboxURL(Instance, "gateway-unity", v.Major, v.Minor, v.Patch));
            return s;
        }

        public String GetProductionURL()
        {
            Version v = ParseVersion(Constants.Version);
            string s = NativeString.StringFromUTF8AndDeallocate(Imported.MGW_SDK_GetProjectionProductionURL(Instance, "gateway-unity", v.Major, v.Minor, v.Patch));
            return s;
        }

        public void HandleReconnect()
        {
            Imported.MGW_SDK_HandleReconnect(Instance);
        }
        #endregion

        #region Authentication
        public delegate void OnAuthenticateDelegate(AuthenticationResponse Response);
        private class InvokeOnAuthenticateParameters
        {
            public OnAuthenticateDelegate Callback;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        [MonoPInvokeCallback(typeof(GatewayAuthenticateResponseDelegate))]
#endif
        private static void InvokeOnAuthenticate(IntPtr Data, IntPtr Msg)
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            GCHandle handle = GCHandle.FromIntPtr(Data);
            if (handle.Target == null)
            {
                return;
            }

            InvokeOnAuthenticateParameters args = handle.Target as InvokeOnAuthenticateParameters;
            if (args == null)
            {
                return;
            }

            if (args.Callback != null)
            {
                AuthenticationResponse Response = new AuthenticationResponse();
                MGW_AuthenticateResponse resp = Marshal.PtrToStructure<MGW_AuthenticateResponse>(Msg);

                Response.JWT = NativeString.StringFromUTF8(resp.JWT);
                Response.RefreshToken = NativeString.StringFromUTF8(resp.RefreshToken);
                Response.TwitchUsername = NativeString.StringFromUTF8(resp.TwitchName);
                Response.HasError = resp.HasError != 0;

                args.Callback(Response);
            }

            handle.Free();
        }

        public void AuthenticateWithPIN(String PIN, OnAuthenticateDelegate Delegate)
        {
            InvokeOnAuthenticateParameters args = new InvokeOnAuthenticateParameters();
            args.Callback = Delegate;

            GCHandle Handle = GCHandle.Alloc(args);
            Imported.MGW_SDK_AuthenticateWithPIN(Instance, PIN, InvokeOnAuthenticate, GCHandle.ToIntPtr(Handle));
        }

        public void AuthenticateWithRefreshToken(String Refresh, OnAuthenticateDelegate Delegate)
        {
            InvokeOnAuthenticateParameters args = new InvokeOnAuthenticateParameters();
            args.Callback = Delegate;

            GCHandle Handle = GCHandle.Alloc(args);
            Imported.MGW_SDK_AuthenticateWithRefreshToken(Instance, Refresh, InvokeOnAuthenticate, GCHandle.ToIntPtr(Handle));
        }

        public void Deauthenticate()
        {
            if (Transport != null)
            {
                Transport.Disconnect();
            }
            Imported.MGW_SDK_Deauthenticate(Instance);
        }

        public bool IsAuthenticated
        {
            get
            {
                return Imported.MGW_SDK_IsAuthenticated(Instance) != 0;
            }
        }
        #endregion

        #region Game Metadata
        public void SetGameMetadata(GameMetadata InMeta)
        {
            MGW_GameMetadata Meta = new MGW_GameMetadata();
            Meta.GameLogo = InMeta.Logo;
            Meta.GameName = InMeta.Name;
            Meta.Theme = InMeta.Theme;

            Imported.MGW_SDK_SetGameMetadata(Instance, Meta);
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        public void SetGameLogo(Texture2D texture)
        {
            MGW_GameMetadata meta = new MGW_GameMetadata();
            meta.GameLogo = ConvertTextureToBase64(texture);

            Imported.MGW_SDK_SetGameMetadata(Instance, meta);
        }

        public static bool IsCompatibleFormat(TextureFormat format)
        {
            switch (format)
            {
                case TextureFormat.ARGB32:
                case TextureFormat.RGB24:
                case TextureFormat.RGBA32:
                    return true;
                default:
                    return false;
            }
        }

        public static string ConvertTextureToBase64(Texture2D texture)
        {
            if (texture == null)
            {
                return "";
            }

            if (texture.isReadable)
            {
                if (IsCompatibleFormat(texture.format))
                {
                    byte[] fastPathBytes = texture.EncodeToPNG();
                    return String.Format("data:image/png;base64,{0}", Convert.ToBase64String(fastPathBytes));
                }
            }

            throw new ArgumentException("Texture must be readable and in RGB24 or RGBA32 format");
        }
#endif
        #endregion

        #region Game Texts
        public void SetGameTexts(GameText[] Texts)
        {
            List<MGW_GameText> NativeTexts = new List<MGW_GameText>();

            foreach (GameText Text in Texts)
            {
                MGW_GameText Value = new MGW_GameText();
                Value.Label = Text.Label;
                Value.Value = Text.Value;
                Value.Icon = Text.Icon;

                NativeTexts.Add(Value);
            }

            Imported.MGW_SDK_SetGameTexts(Instance, NativeTexts.ToArray(), (UInt64)NativeTexts.Count);

        }
        #endregion

        #region Polls
        private GCHandle PollDelegateHandle;

        private class InvokePollUpdateParameters
        {
            public PollConfiguration.OnUpdateDelegate Callback;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        [MonoPInvokeCallback(typeof(GatewayPollUpdateDelegate))]
#endif
        private static void InvokePollUpdate(IntPtr Data, IntPtr Msg)
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            GCHandle handle = GCHandle.FromIntPtr(Data);
            if (handle.Target == null)
            {
                return;
            }

            InvokePollUpdateParameters args = handle.Target as InvokePollUpdateParameters;
            if (args == null)
            {
                return;
            }

            if (args.Callback != null)
            {
                MGW_PollUpdate NativeUpdate = Marshal.PtrToStructure<MGW_PollUpdate>(Msg);

                PollUpdate Update = new PollUpdate();
                Update.Winner = NativeUpdate.Winner;
                Update.WinningVoteCount = NativeUpdate.WinningVoteCount;

                int[] ManagedResults = new int[NativeUpdate.ResultCount];
                Marshal.Copy(NativeUpdate.Results, ManagedResults, 0, (int)NativeUpdate.ResultCount);

                List<int> ResultList = new List<int>(ManagedResults);

                Update.Results = ResultList;
                Update.Count = NativeUpdate.Count;
                Update.Mean = NativeUpdate.Mean;
                Update.IsFinal = NativeUpdate.IsFinal != 0;

                args.Callback(Update);
            }
        }

        public void StartPoll(PollConfiguration Configuration)
        {
            MGW_PollConfiguration NativeConfig = new MGW_PollConfiguration();

            NativeConfig.Prompt = Configuration.Prompt;
            NativeConfig.Location = (int)Configuration.Location;
            NativeConfig.Mode = (int)Configuration.Mode;

            List<IntPtr> Strings = new List<IntPtr>();
            foreach (string Opt in Configuration.Options)
            {
                int Len = UTF8WithoutBOM.GetByteCount(Opt);
                byte[] buffer = new byte[Len + 1];
                UTF8WithoutBOM.GetBytes(Opt, 0, Opt.Length, buffer, 0);
                buffer[Len] = 0;

                IntPtr NativeStr = Marshal.AllocHGlobal(buffer.Length);
                Marshal.Copy(buffer, 0, NativeStr, buffer.Length);

                Strings.Add(NativeStr);
            }

            IntPtr[] StringsArray = Strings.ToArray();
            GCHandle StringsArrayHandle = GCHandle.Alloc(StringsArray, GCHandleType.Pinned);

            NativeConfig.Options = StringsArrayHandle.AddrOfPinnedObject();
            NativeConfig.OptionsCount = (UInt64)Configuration.Options.Count;
            NativeConfig.Duration = Configuration.DurationInSeconds;

            InvokePollUpdateParameters args = new InvokePollUpdateParameters();
            args.Callback = Configuration.OnPollUpdate;

            GCHandle NextPollDelegateHandle = GCHandle.Alloc(args);

            NativeConfig.OnUpdate = InvokePollUpdate;
            NativeConfig.User = GCHandle.ToIntPtr(NextPollDelegateHandle);

            Imported.MGW_SDK_StartPoll(Instance, NativeConfig);

            StringsArrayHandle.Free();
            foreach (IntPtr Allocated in Strings)
            {
                Marshal.FreeHGlobal(Allocated);
            }

            if (PollDelegateHandle.IsAllocated)
            {
                PollDelegateHandle.Free();
            }

            PollDelegateHandle = NextPollDelegateHandle;
        }

        public void StopPoll()
        {
            Imported.MGW_SDK_StopPoll(Instance);
        }

        #endregion

        #region Actions
        private static Dictionary<string, MuxyGatewayGameActionUsedEvent> GameActionCallbacks;
        public void SetGameActions(GameAction[] Actions)
        {
            GameActionCallbacks = new Dictionary<string, MuxyGatewayGameActionUsedEvent>();

            List<MGW_Action> NativeActions = new List<MGW_Action>();

            foreach (GameAction Action in Actions)
            {
                MGW_Action Value = new MGW_Action();
                Value.ID = Action.ID;
                Value.Category = ((int)Action.Category);
                Value.State = ((int)Action.State);
                Value.Impact = Action.Impact;
                Value.Name = Action.Name;
                Value.Description = Action.Description;
                Value.Icon = Action.Icon;
                Value.Count = Action.Count;

                NativeActions.Add(Value);
                GameActionCallbacks.Add(Action.ID, Action.OnGameActionUsed);
            }

            Imported.MGW_SDK_SetActions(Instance, NativeActions.ToArray(), (UInt64)NativeActions.Count);
        }

        public void EnableGameAction(string ID)
        {
            Imported.MGW_SDK_EnableAction(Instance, ID);
        }

        public void DisableGameAction(string ID)
        {
            Imported.MGW_SDK_DisableAction(Instance, ID);
        }

        public void SetMaximumGameActionCount(string ID, int Count)
        {
            Imported.MGW_SDK_SetActionMaximumCount(Instance, ID, Count);
        }

        public void SetGameActionCount(string ID, int Count)
        {
            Imported.MGW_SDK_SetActionCount(Instance, ID, Count);
        }

        public void IncrementGameActionCount(string ID, int Delta)
        {
            Imported.MGW_SDK_IncrementActionCount(Instance, ID, Delta);
        }

        public void DecrementGameActionCount(string ID, int Delta)
        {
            Imported.MGW_SDK_DecrementActionCount(Instance, ID, Delta);
        }

        public delegate void OnGameActionUsedDelegate(GameActionUsed Used);
        private class InvokeOnActionUsedParameters
        {
            public OnGameActionUsedDelegate Callback;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        [MonoPInvokeCallback(typeof(OnGameActionUsedDelegate))]
#endif
        private static void InvokeOnActionUsed(IntPtr Data, IntPtr Msg)
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            GCHandle handle = GCHandle.FromIntPtr(Data);
            if (handle.Target == null)
            {
                return;
            }

            InvokeOnActionUsedParameters args = handle.Target as InvokeOnActionUsedParameters;
            if (args == null)
            {
                return;
            }

            if (args.Callback != null)
            {
                MGW_ActionUsed Value = Marshal.PtrToStructure<MGW_ActionUsed>(Msg);

                GameActionUsed Used = new GameActionUsed();
                Used.TransactionID = Value.TransactionID;
                Used.ActionID = Value.ActionID;
                Used.Cost = Value.Cost;
                Used.UserID = Value.UserID;
                Used.Username = Value.Username;

                MuxyGatewayGameActionUsedEvent Callback;
                if (GameActionCallbacks.TryGetValue(Used.ActionID, out Callback))
                {
                    Callback.Invoke(Used);
                }
                args.Callback(Used);
            }
        }

        private GCHandle GameActionUsedDelegateHandle;
        public void OnGameActionUsed(OnGameActionUsedDelegate Delegate)
        {
            InvokeOnActionUsedParameters args = new InvokeOnActionUsedParameters();
            args.Callback = Delegate;

            // This is kinda sketch: OnActionUsed doesn't detach the previous
            // callback after a call, so this leaks the GC Handle.
            GameActionUsedDelegateHandle = GCHandle.Alloc(args);
            Imported.MGW_SDK_OnActionUsed(Instance, InvokeOnActionUsed, GCHandle.ToIntPtr(GameActionUsedDelegateHandle));
        }

        public void AcceptGameAction(GameActionUsed Used, string Description)
        {
            MGW_ActionUsed Native = new MGW_ActionUsed();
            Native.ActionID = Used.ActionID;
            Native.TransactionID = Used.TransactionID;
            Native.Cost = Used.Cost;
            Native.UserID = Used.UserID;
            Native.Username = Used.Username;

            Imported.MGW_SDK_AcceptAction(Instance, Native, Description);
        }

        public void RefundGameAction(GameActionUsed Used, string Description)
        {
            MGW_ActionUsed Native = new MGW_ActionUsed();
            Native.ActionID = Used.ActionID;
            Native.TransactionID = Used.TransactionID;
            Native.Cost = Used.Cost;
            Native.UserID = Used.UserID;
            Native.Username = Used.Username;

            Imported.MGW_SDK_RefundAction(Instance, Native, Description);
        }
        #endregion

        #region Bits 
        public delegate void OnBitsUsedDelegate(BitsUsed Used);
        private class InvokeOnBitsUsedParameters
        {
            public OnBitsUsedDelegate Callback;
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        [MonoPInvokeCallback(typeof(GatewayOnBitsUsedDelegate))]
#endif
        private static void InvokeOnBitsUsed(IntPtr Data, IntPtr Msg)
        {
            if (Data == IntPtr.Zero)
            {
                return;
            }

            GCHandle handle = GCHandle.FromIntPtr(Data);
            if (handle.Target == null)
            {
                return;
            }

            InvokeOnBitsUsedParameters args = handle.Target as InvokeOnBitsUsedParameters;
            if (args == null)
            {
                return;
            }

            if (args.Callback != null)
            {
                MGW_BitsUsed Value = Marshal.PtrToStructure<MGW_BitsUsed>(Msg);

                BitsUsed Used = new BitsUsed();
                Used.TransactionID = Value.TransactionID;
                Used.SKU = Value.SKU;
                Used.Bits = Value.Bits;
                Used.UserID = Value.UserID;
                Used.Username = Value.Username;

                args.Callback(Used);
            }
        }

        private GCHandle BitsUsedDelegateHandle;
        public void OnBitsUsed(OnBitsUsedDelegate Delegate)
        {
            InvokeOnBitsUsedParameters args = new InvokeOnBitsUsedParameters();
            args.Callback = Delegate;

            // This is kinda sketch: OnActionUsed doesn't detach the previous
            // callback after a call, so this leaks the GC Handle.
            BitsUsedDelegateHandle = GCHandle.Alloc(args);
            Imported.MGW_SDK_OnBitsUsed(Instance, InvokeOnBitsUsed, GCHandle.ToIntPtr(BitsUsedDelegateHandle));
        }
        #endregion
    }
}
