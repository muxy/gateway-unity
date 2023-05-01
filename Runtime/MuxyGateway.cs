using MuxyGameLink.Imports;
using MuxyGameLink.Imports.Schema;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System;
using System.Threading.Tasks;

#if UNITY_EDITOR || UNITY_STANDALONE
using UnityEngine;
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

    public class GameMetadata
    {
        public string Name { get; set; } = string.Empty;
        public string Logo { get; set; } = string.Empty;
        public string Theme { get; set; } = string.Empty;
    }

    public class GameText
    {
        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
    }

    public enum ActionCategory
    {
        Neutral = 0,
        Hinder = 1,
        Help = 2
    }

    public enum ActionState
    {
        Unavailable = 0,
        Available = 1,
        Hidden = 2
    }

    public class Action
    {
        public static int InfiniteCount = 0xFFFF;

        public string ID { get; set; } = string.Empty;

        public ActionCategory Category { get; set; } = ActionCategory.Neutral;
        public ActionState State { get; set; } = ActionState.Unavailable;
        public int Impact { get; set; } = 0;

        public string Name { set; get; } = string.Empty;
        public string Description { set; get; } = string.Empty;
        public string Icon { set; get; } = string.Empty;

        public int Count { get; set; } = InfiniteCount;
    }

    public class PollUpdate
    {
        public int Winner { get; set; } = 0;
        public int WinningVoteCount { get; set; } = 0;

        public List<int> Results { get; set; } = new List<int>();

        public int Count { set; get; } = 0;
        public double Mean { get; set; } = 0;
        public bool IsFinal { get; set; } = false;
    }

    public enum PollLocation
    {
        Default = 0
    }

    public enum PollMode
    {
        Chaos = 0,
        Order = 1
    }

    public class PollConfiguration
    {
        public static int InfiniteDuration = 0;
        public delegate void OnUpdateDelegate(PollUpdate Update);

        public string Prompt { set; get; } = string.Empty;
        public PollLocation Location { set; get; } = PollLocation.Default;
        public PollMode Mode { set; get; } = PollMode.Order;

        public List<string> Options { set; get; } = new List<string>();

        public Int32 DurationInSeconds { set; get; } = InfiniteDuration;

        public OnUpdateDelegate OnPollUpdate { set; get; } = (Update) => { };
    }

    public class BitsUsed
    {
        public string TransactionID { set; get; } = string.Empty;
        public string SKU { set; get; } = string.Empty;
        public int Bits { set; get; } = 0;
        public string UserID { set; get; } = string.Empty;
        public string Username { set; get; } = string.Empty;
    }

    public class ActionUsed
    {
        public string TransactionID { set; get; } = string.Empty;
        public string ActionID { set; get; } = string.Empty;
        public int Cost { set; get; } = 0;
        public string UserID { set; get; } = string.Empty;
        public string Username { set; get; } = string.Empty;
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

        public SDK(string GameID)
        {
            Instance = Imported.MGW_MakeSDK(GameID);
            this.GameID = GameID;
            this.Transport = new();

            GatewayDebugMessageDelegate Callback = (UserData, Message) =>
            {
                this.LogMessage(Message);
            };

            DebugMessage = GCHandle.Alloc(Callback, GCHandleType.Normal);
            Imported.MGW_SDK_OnDebugMessage(Instance, Callback, IntPtr.Zero);
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
            bool result = Imported.MGW_SDK_ReceiveMessage(Instance, Bytes, (uint)Bytes.Length);
            return result;
        }

        public bool HasPayloads()
        {
            return Imported.MGW_SDK_HasPayloads(Instance);
        }

        public delegate void ForeachPayloadDelegate(Payload Payload);
        public void ForeachPayload(ForeachPayloadDelegate Delegate)
        {
            GatewayForeachPayloadDelegate WrapperCallback = (UserData, Msg) =>
            {
                Payload p = new Payload();
                MGW_Payload first = Marshal.PtrToStructure<MGW_Payload>(Msg);

                p.Bytes = new byte[first.Length];
                Marshal.Copy(first.Bytes, p.Bytes, 0, (int)first.Length);

                Delegate(p);
            };

            GCHandle Handle = GCHandle.Alloc(WrapperCallback, GCHandleType.Normal);
            Imported.MGW_SDK_ForeachPayload(Instance, WrapperCallback, IntPtr.Zero);
            Handle.Free();
        }

        public String GetSandboxURL()
        {
            string s = NativeString.StringFromUTF8AndDeallocate(Imported.MGW_SDK_GetSandboxURL(Instance));
            return s;
        }

        public String GetProductionURL()
        {
            string s = NativeString.StringFromUTF8AndDeallocate(Imported.MGW_SDK_GetProductionURL(Instance));
            return s;
        }

        public void HandleReconnect()
        {
            Imported.MGW_SDK_HandleReconnect(Instance);
        }
        #endregion

        #region Authentication
        public delegate void OnAuthenticateDelegate(AuthenticationResponse Response);
        public void AuthenticateWithPIN(String PIN, OnAuthenticateDelegate Delegate)
        {
            GatewayAuthenticateResponseDelegate WrapperCallback = (UserData, Msg) =>
            {
                AuthenticationResponse Response = new AuthenticationResponse();
                MGW_AuthenticateResponse resp = Marshal.PtrToStructure<MGW_AuthenticateResponse>(Msg);

                Response.JWT = NativeString.StringFromUTF8(resp.JWT);
                Response.RefreshToken = NativeString.StringFromUTF8(resp.RefreshToken);
                Response.TwitchUsername = NativeString.StringFromUTF8(resp.TwitchName);
                Response.HasError = resp.HasError != 0;

                Delegate(Response);

                GCHandle Self = GCHandle.FromIntPtr(UserData);
                Self.Free();
            };

            GCHandle Handle = GCHandle.Alloc(WrapperCallback, GCHandleType.Normal);
            Imported.MGW_SDK_AuthenticateWithPIN(Instance, PIN, WrapperCallback, (IntPtr)Handle);
        }

        public void AuthenticateWithRefreshToken(String Refresh, OnAuthenticateDelegate Delegate)
        {
            GatewayAuthenticateResponseDelegate WrapperCallback = (UserData, Msg) =>
            {
                AuthenticationResponse Response = new AuthenticationResponse();
                MGW_AuthenticateResponse resp = Marshal.PtrToStructure<MGW_AuthenticateResponse>(Msg);

                Response.JWT = NativeString.StringFromUTF8(resp.JWT);
                Response.RefreshToken = NativeString.StringFromUTF8(resp.RefreshToken);
                Response.TwitchUsername = NativeString.StringFromUTF8(resp.TwitchName);
                Response.HasError = resp.HasError != 0;

                Delegate(Response);

                GCHandle Self = GCHandle.FromIntPtr(UserData);
                Self.Free();
            };

            GCHandle Handle = GCHandle.Alloc(WrapperCallback, GCHandleType.Normal);
            Imported.MGW_SDK_AuthenticateWithRefreshToken(Instance, Refresh, WrapperCallback, (IntPtr)Handle);
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
                return Imported.MGW_SDK_IsAuthenticated(Instance);
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
            meta.GameLogo = ConvertTextureToImage(texture);

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

        public static string ConvertTextureToImage(Texture2D texture)
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

            GatewayPollUpdateDelegate WrapperCallback = (IntPtr User, IntPtr UpdatePtr) =>
            {
                MGW_PollUpdate NativeUpdate = Marshal.PtrToStructure<MGW_PollUpdate>(UpdatePtr);

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

                Configuration.OnPollUpdate(Update);
            };

            NativeConfig.OnUpdate = WrapperCallback;
            NativeConfig.User = IntPtr.Zero;

            GCHandle NextPollDelegateHandle = GCHandle.Alloc(WrapperCallback, GCHandleType.Normal);
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
        public void SetActions(Action[] Actions)
        {
            List<MGW_Action> NativeActions = new List<MGW_Action>();

            foreach (Action Action in Actions)
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
            }

            Imported.MGW_SDK_SetActions(Instance, NativeActions.ToArray(), (UInt64)NativeActions.Count);
        }

        public void EnableAction(string ID)
        {
            Imported.MGW_SDK_EnableAction(Instance, ID);
        }

        public void DisableAction(string ID)
        {
            Imported.MGW_SDK_DisableAction(Instance, ID);
        }

        public void SetMaximumActionCount(string ID, int Count)
        {
            Imported.MGW_SDK_SetActionMaximumCount(Instance, ID, Count);
        }

        public void SetActionCount(string ID, int Count)
        {
            Imported.MGW_SDK_SetActionCount(Instance, ID, Count);
        }

        public void IncrementActionCount(string ID, int Delta)
        {
            Imported.MGW_SDK_IncrementActionCount(Instance, ID, Delta);
        }

        public void DecrementActionCount(string ID, int Delta)
        {
            Imported.MGW_SDK_DecrementActionCount(Instance, ID, Delta);
        }

        public delegate void OnActionUsedDelegate(ActionUsed Used);
        private GCHandle ActionUsedDelegateHandle;
        public void OnActionUsed(OnActionUsedDelegate Delegate)
        {
            GatewayOnActionUsedDelegate WrapperDelegate = (UserData, Pointer) =>
            {
                MGW_ActionUsed Value = Marshal.PtrToStructure<MGW_ActionUsed>(Pointer);

                ActionUsed Used = new ActionUsed();
                Used.TransactionID = Value.TransactionID;
                Used.ActionID = Value.ActionID;
                Used.Cost = Value.Cost;
                Used.UserID = Value.UserID;
                Used.Username = Value.Username;

                Delegate(Used);
            };

            // This is kinda sketch: OnActionUsed doesn't detach the previous
            // callback after a call, so this leaks the GC Handle.
            ActionUsedDelegateHandle = GCHandle.Alloc(WrapperDelegate, GCHandleType.Normal);
            Imported.MGW_SDK_OnActionUsed(Instance, WrapperDelegate, IntPtr.Zero);
        }

        public void AcceptAction(ActionUsed Used, string Description)
        {
            MGW_ActionUsed Native = new MGW_ActionUsed();
            Native.ActionID = Used.ActionID;
            Native.TransactionID = Used.TransactionID;
            Native.Cost = Used.Cost;
            Native.UserID = Used.UserID;
            Native.Username = Used.Username;

            Imported.MGW_SDK_AcceptAction(Instance, Native, Description);
        }

        public void RefundAction(ActionUsed Used, string Description)
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
        private GCHandle BitsUsedDelegateHandle;
        public void OnBitsUsed(OnBitsUsedDelegate Delegate)
        {
            GatewayOnBitsUsedDelegate WrapperDelegate = (UserData, Pointer) =>
            {
                MGW_BitsUsed Value = Marshal.PtrToStructure<MGW_BitsUsed>(Pointer);

                BitsUsed Used = new BitsUsed();
                Used.TransactionID = Value.TransactionID;
                Used.SKU = Value.SKU;
                Used.Bits = Value.Bits;
                Used.UserID = Value.UserID;
                Used.Username = Value.Username;

                Delegate(Used);
            };

            // This is kinda sketch: OnActionUsed doesn't detach the previous
            // callback after a call, so this leaks the GC Handle.
            BitsUsedDelegateHandle = GCHandle.Alloc(WrapperDelegate, GCHandleType.Normal);
            Imported.MGW_SDK_OnBitsUsed(Instance, WrapperDelegate, IntPtr.Zero);
        }
        #endregion
    }
}
