using MuxyGateway.Imports.Schema;
using System.Diagnostics.Contracts;
using System.Runtime.InteropServices;
using System;

namespace MuxyGateway.Imports
{
    // StringPtr should be made into MGL_String
    using StringPtr = System.IntPtr;

    // These pointers should be deallocated.
    using AllocatedStringPtr = System.IntPtr;

    // VoidPtr used for UserData, probably won't be used much in C#
    using VoidPtr = System.IntPtr;

    using RequestId = UInt16;


    namespace Schema
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct GatewaySDK
        {
            public IntPtr SDK;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_AuthenticateResponse
        {
            public IntPtr JWT;
            public IntPtr RefreshToken;
            public IntPtr TwitchName;
            public UInt32 HasError;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_Payload
        {
            public IntPtr Bytes;
            public UInt64 Length;
        };

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_GameMetadata
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String GameName;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String GameLogo;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Theme;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_GameText
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Label;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Value;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Icon;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_Action
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String ID;

            public Int32 Category;
            public Int32 State;
            public Int32 Impact;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Name;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Description;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Icon;

            public Int32 Count;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_PollUpdate
        {
            public Int32 Winner;
            public Int32 WinningVoteCount;

            public IntPtr Results;
            public UInt64 ResultCount;

            public Int32 Count;
            public double Mean;
            public UInt32 IsFinal;
        }

        public delegate void GatewayPollUpdateDelegate(VoidPtr User, IntPtr Update);

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_PollConfiguration
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public string Prompt;

            public Int32 Location;
            public Int32 Mode;

            public IntPtr Options;
            public UInt64 OptionsCount;

            public Int32 Duration;

            public GatewayPollUpdateDelegate OnUpdate;
            public VoidPtr User;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_BitsUsed
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String TransactionID;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String SKU;

            public Int32 Bits;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String UserID;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Username;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MGW_ActionUsed
        {
            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String TransactionID;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String ActionID;

            public Int32 Cost;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String UserID;

            [MarshalAs(UnmanagedType.LPUTF8Str)]
            public String Username;
        }
    }

    public class NativeString
    {
        public static String StringFromUTF8(StringPtr Ptr, int Length)
        {
            if (Ptr.Equals(StringPtr.Zero))
            {
                return String.Empty;
            }

            byte[] Copy = new byte[Length];
            Marshal.Copy(Ptr, Copy, 0, Length);

            return System.Text.Encoding.UTF8.GetString(Copy);
        }

        public static String StringFromUTF8(StringPtr Ptr)
        {
            UInt32 Length = Imported.StrLen(Ptr);
            return StringFromUTF8(Ptr, ((int)Length));
        }

        public static String StringFromUTF8AndDeallocate(AllocatedStringPtr Ptr)
        {
            String Result = StringFromUTF8(Ptr);
            Imported.FreeString(Ptr);
            return Result;
        }
    }

    public class NativeTimestamp
    {
        public static DateTime DateTimeFromMilliseconds(Int64 milliseconds)
        {
            TimeSpan Interval = TimeSpan.FromMilliseconds(milliseconds);
            return new DateTime(1970, 1, 1) + Interval;
        }
    }

    public delegate void GatewayAuthenticateResponseDelegate(VoidPtr UserData, IntPtr Response);
    public delegate void GatewayForeachPayloadDelegate(VoidPtr UserData, IntPtr Payload);
    public delegate void GatewayDebugMessageDelegate(VoidPtr UserData, [MarshalAs(UnmanagedType.LPUTF8Str)] String Message);
    public delegate void GatewayOnBitsUsedDelegate(VoidPtr UserData, IntPtr BitsUsed);
    public delegate void GatewayOnActionUsedDelegate(VoidPtr UserData, IntPtr ActionUsed);

    public class Imported
    {
        // URL Derivation
        [DllImport("cgamelink.dll", EntryPoint = "MuxyGameLink_ProjectionWebsocketConnectionURL")]
        public static extern AllocatedStringPtr ProjectionWebsocketConnectionURL(
            [MarshalAs(UnmanagedType.LPStr)] String clientID,
            Int32 stage,
            [MarshalAs(UnmanagedType.LPStr)] String projection,
            int projectionMajor,
            int projectionMinor,
            int projectionPatch);

        [DllImport("cgamelink.dll", EntryPoint = "MuxyGameLink_Strlen")]
        public static extern UInt32 StrLen(StringPtr Str);

        [DllImport("cgamelink.dll", EntryPoint = "MuxyGameLink_FreeString")]
        public static extern void FreeString(StringPtr Str);

        #region Gateway
        [DllImport("cgamelink.dll", EntryPoint = "MGW_MakeSDK")]
        public static extern Schema.GatewaySDK MGW_MakeSDK([MarshalAs(UnmanagedType.LPUTF8Str)] String GameID);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_KillSDK")]
        public static extern void MGW_KillSDK(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_GetSandboxURL")]
        public static extern StringPtr MGW_SDK_GetSandboxURL(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_GetProductionURL")]
        public static extern StringPtr MGW_SDK_GetProductionURL(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_GetProjectionSandboxURL")]
        public static extern StringPtr MGW_SDK_GetProjectionSandboxURL(Schema.GatewaySDK SDK, [MarshalAs(UnmanagedType.LPUTF8Str)] String Projection, int Major, int Minor, int Patch);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_GetProjectionProductionURL")]
        public static extern StringPtr MGW_SDK_GetProjectionProductionURL(Schema.GatewaySDK SDK, [MarshalAs(UnmanagedType.LPUTF8Str)] String Projection, int Major, int Minor, int Patch);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_AuthenticateWithPIN")]
        public static extern RequestId MGW_SDK_AuthenticateWithPIN(Schema.GatewaySDK SDK, [MarshalAs(UnmanagedType.LPUTF8Str)] String PIN, GatewayAuthenticateResponseDelegate Delegate, VoidPtr User);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_AuthenticateWithRefreshToken")]
        public static extern RequestId MGW_SDK_AuthenticateWithRefreshToken(Schema.GatewaySDK SDK, [MarshalAs(UnmanagedType.LPUTF8Str)] String Refresh, GatewayAuthenticateResponseDelegate Delegate, VoidPtr User);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_Deauthenticate")]
        public static extern RequestId MGW_SDK_Deauthenticate(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_ReceiveMessage")]
        public static extern UInt32 MGW_SDK_ReceiveMessage(Schema.GatewaySDK SDK, byte[] Message, uint BytesLength);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_HasPayloads")]
        public static extern UInt32 MGW_SDK_HasPayloads(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_HandleReconnect")]
        public static extern void MGW_SDK_HandleReconnect(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_OnDebugMessage")]
        public static extern void MGW_SDK_OnDebugMessage(Schema.GatewaySDK SDK, GatewayDebugMessageDelegate Callback, VoidPtr User);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_DetachOnDebugMessage")]
        public static extern void MGW_SDK_DetachOnDebugMessage(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_ForeachPayload")]
        public static extern void MGW_SDK_ForeachPayload(Schema.GatewaySDK SDK, GatewayForeachPayloadDelegate Delegate, VoidPtr User);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_IsAuthenticated")]
        public static extern UInt32 MGW_SDK_IsAuthenticated(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_SetGameMetadata")]
        public static extern RequestId MGW_SDK_SetGameMetadata(Schema.GatewaySDK SDK, MGW_GameMetadata Meta);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_SetGameTexts")]
        public static extern void MGW_SDK_SetGameTexts(Schema.GatewaySDK SDK, MGW_GameText[] Texts, UInt64 Count);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_StartPoll")]
        public static extern void MGW_SDK_StartPoll(Schema.GatewaySDK SDK, MGW_PollConfiguration Config);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_StopPoll")]
        public static extern void MGW_SDK_StopPoll(Schema.GatewaySDK SDK);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_SetActions")]
        public static extern void MGW_SDK_SetActions(Schema.GatewaySDK SDK, MGW_Action[] Actions, UInt64 Count);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_EnableAction")]
        public static extern void MGW_SDK_EnableAction(Schema.GatewaySDK Gateway, [MarshalAs(UnmanagedType.LPUTF8Str)] String ID);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_DisableAction")]
        public static extern void MGW_SDK_DisableAction(Schema.GatewaySDK Gateway, [MarshalAs(UnmanagedType.LPUTF8Str)] String ID);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_SetActionMaximumCount")]
        public static extern void MGW_SDK_SetActionMaximumCount(Schema.GatewaySDK Gateway, [MarshalAs(UnmanagedType.LPUTF8Str)] String ID, Int32 count);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_SetActionCount")]
        public static extern void MGW_SDK_SetActionCount(Schema.GatewaySDK Gateway, [MarshalAs(UnmanagedType.LPUTF8Str)] String ID, Int32 count);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_IncrementActionCount")]
        public static extern void MGW_SDK_IncrementActionCount(Schema.GatewaySDK Gateway, [MarshalAs(UnmanagedType.LPUTF8Str)] String ID, Int32 count);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_DecrementActionCount")]
        public static extern void MGW_SDK_DecrementActionCount(Schema.GatewaySDK Gateway, [MarshalAs(UnmanagedType.LPUTF8Str)] String ID, Int32 count);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_OnBitsUsed")]
        public static extern void MGW_SDK_OnBitsUsed(Schema.GatewaySDK Gateway, GatewayOnBitsUsedDelegate Callback, VoidPtr User);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_OnActionUsed")]
        public static extern void MGW_SDK_OnActionUsed(Schema.GatewaySDK Gateway, GatewayOnActionUsedDelegate Callback, VoidPtr User);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_AcceptAction")]
        public static extern void MGW_SDK_AcceptAction(Schema.GatewaySDK Gateway, MGW_ActionUsed Coins, [MarshalAs(UnmanagedType.LPUTF8Str)] String Reason);

        [DllImport("cgamelink.dll", EntryPoint = "MGW_SDK_RefundAction")]
        public static extern void MGW_SDK_RefundAction(Schema.GatewaySDK Gateway, MGW_ActionUsed Coins, [MarshalAs(UnmanagedType.LPUTF8Str)] String Reason);
        #endregion
    }
}