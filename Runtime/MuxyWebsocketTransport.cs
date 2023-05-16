using System.Text;
using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

namespace MuxyGateway
{
    public enum Stage
    {
        Production = 0,
        Sandbox = 1
    };

    public class WebsocketTransport
    {
        private ClientWebSocket Websocket;
        private CancellationTokenSource UnboundedCancellationSource = new CancellationTokenSource();
        private static readonly Encoding UTF8Encoding = new UTF8Encoding(false);

        private bool HandleMessagesInMainThread = true;
        private ConcurrentQueue<string> Messages = new ConcurrentQueue<string>();

        private Thread WriteThread;
        private Thread ReadThread;
        private bool Done = false;

        private Uri TargetUri;

        private CancellationTokenSource TokenSource()
        {
            CancellationTokenSource src = new CancellationTokenSource();
            src.CancelAfter(5000);

            return src;
        }

        private void LogMessage(string message)
        {
#if UNITY_EDITOR
            UnityEngine.Debug.Log(message);
#else
            Console.WriteLine(message);
#endif
        }

        /// <summary>
        ///  Creates a websocket transport without an associated Gamelink instance or stage.
        /// </summary>
        public WebsocketTransport()
        {
            Websocket = new ClientWebSocket();
            this.HandleMessagesInMainThread = true;

#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += (PlayModeStateChange state) =>
            {
                if (state == PlayModeStateChange.ExitingPlayMode)
                {
                    // If a user forgets to stop the websocket transport, the editor locks up.
                    // This is bad, prevent this by attaching an event to stop the websocket connection
                    // when the editor stops the PIE mode.
                    LogMessage("Stopping websocket transport due to editor state change.");
                    StopAsync().Wait();
                    LogMessage("This may cause errors while playing in editor, but prevents leaking a connection, which is worse.");
                }
            };

            EditorApplication.quitting += () =>
            {
                LogMessage("Stopping due to application quit.");
                StopAsync().Wait();
            };
#endif
        }

        ~WebsocketTransport()
        {
            try
            {
                StopAsync().Wait();
            }
            catch (InvalidOperationException ex)
            {
                LogMessage(ex.ToString());
            }
        }

        /// <summary>
        ///  Opens a websocket connection to the given uri, usually computed by calling SDK.ConnectionAddress
        /// </summary>
        /// <param name="uri">URI to connect to. Must be prefixed with the protocol, usually "ws://"</param>
        /// <returns></returns>
        public async Task Open(string uri)
        {
            TargetUri = new Uri(uri);

            using (CancellationTokenSource src = TokenSource())
            {
                await Websocket.ConnectAsync(TargetUri, src.Token)
                    .ConfigureAwait(false);
            }
        }

        private async Task OpenAndRunInStage(SDK instance, Stage stage)
        {
            switch (stage)
            {
                case Stage.Sandbox:
                    {
                        string url = instance.GetSandboxURL();
                        await Open("ws://" + url)
                            .ConfigureAwait(false);
                        break;
                    }

                case Stage.Production:
                    {
                        string url = instance.GetProductionURL();
                        await Open("ws://" + url)
                            .ConfigureAwait(false);
                        break;
                    }
            }

            Run(instance);
        }

        public Task OpenAndRunInSandbox(MuxyGateway.SDK instance)
        {
            return OpenAndRunInStage(instance, Stage.Sandbox);
        }

        public Task OpenAndRunInProduction(MuxyGateway.SDK instance)
        {
            return OpenAndRunInStage(instance, Stage.Production);
        }

        public void Disconnect()
        {
            StopAsync();
        }

        /// <summary>
        ///  Invokes SendMessages and ReceiveMessage on different threads until a call to Stop()
        ///  Any callbacks invoked from `instance` will be called on a background thread, not the main thread.
        /// </summary>
        /// <param name="instance">The instance to use for sending and receiving messages</param>
        public void Run(SDK instance)
        {
            Done = false;
            WriteThread = new Thread(async () =>
            {
                try
                {
                    while (!Done)
                    {
                        await SendMessages(instance);
                        Thread.Sleep(100);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(ex.ToString());
                }

            });
            WriteThread.Start();

            ReadThread = new Thread(async () =>
            {
                try
                {
                    while (!Done)
                    {
                        await ReceiveMessage(instance);
                    }
                }
                catch (Exception ex)
                {
                    LogMessage(ex.ToString());
                }
            });
            ReadThread.Start();
        }

        /// <summary>
        ///  Updates the Websocket Transport, it's only required to call this if you set HandleMessagesInMainThread to true when initializing the WebsocketTransport
        /// </summary>
        /// <param name="instance">The instance to use for sending and receiving messages</param>
        public void Update(SDK instance)
        {
            string m;
            while (Messages.TryDequeue(out m))
            {
                instance.ReceiveMessage(m);
            }
        }

        /// <summary>
        ///  Stops writing and reading threads.
        ///  When running in unity, this must be called on a MonoBehavior's OnDisable callback.
        ///  for a clean shutdown.
        /// </summary>
        public async Task StopAsync()
        {
            Done = true;
            UnboundedCancellationSource.Cancel();

            try
            {
                if (Websocket.State != WebSocketState.Closed && Websocket.State != WebSocketState.Aborted)
                {
                    using (CancellationTokenSource src = TokenSource())
                    {
                        await Websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "going away", src.Token)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                // Ignore
                LogMessage(ex.ToString());
            }

            if (WriteThread != null)
            {
                WriteThread.Join(TimeSpan.FromSeconds(5));
            }

            if (ReadThread != null)
            {
                ReadThread.Join(TimeSpan.FromSeconds(5));
            }

            UnboundedCancellationSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Sends all queued messages in the instance
        /// </summary>
        /// <param name="instance">instance to send messages from</param>
        /// <returns></returns>
        public async Task SendMessages(SDK instance)
        {
            if (Websocket.State != WebSocketState.Open)
            {
                return;
            }

            if (Reconnecting)
            {
                return;
            }

            List<byte[]> messages = new List<byte[]>();

            instance.ForeachPayload((Payload Payload) =>
            {
                messages.Add(Payload.Bytes);
            });

            try
            {
                foreach (byte[] msg in messages)
                {
                    var bytes = new ArraySegment<byte>(msg);

                    using (CancellationTokenSource src = TokenSource())
                    {
                        await Websocket.SendAsync(bytes, WebSocketMessageType.Text, true, src.Token)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.ToString());

                if (!Done)
                {
                    await AttemptReconnect(instance);
                }
                else
                {
                    return;
                }
            }
        }

        /// <summary>
        ///  Receives a single message to the SDK from the active websocket connection.
        /// </summary>
        /// <param name="instance">The instance to receive a mesage to</param>
        /// <returns></returns>
        public async Task ReceiveMessage(MuxyGateway.SDK instance)
        {
            MemoryStream memory = new MemoryStream();
            while (!Done)
            {
                if (Websocket.State != WebSocketState.Open)
                {
                    Thread.Sleep(100);
                    continue;
                }

                if (Reconnecting)
                {
                    Thread.Sleep(100);
                    continue;
                }

                try
                {
                    // Reading has an infinite timeout
                    ArraySegment<byte> segment = new ArraySegment<byte>(new byte[1024]);
                    var Result = await Websocket.ReceiveAsync(segment, UnboundedCancellationSource.Token)
                        .ConfigureAwait(false);

                    if (Result.MessageType == WebSocketMessageType.Close)
                    {
                        throw new EndOfStreamException("Closed");
                    }

                    if (Result.MessageType != WebSocketMessageType.Text)
                    {
                        throw new InvalidDataException("Message type was not text");
                    }

                    memory.Write(segment.Array, 0, Result.Count);
                    if (Result.EndOfMessage)
                    {
                        break;
                    }
                }
                catch (ThreadAbortException ex)
                {
                    // Don't try.
                    return;
                }
                catch (Exception)
                {
                    if (!Done)
                    {
                        await AttemptReconnect(instance);
                    }
                    else
                    {
                        return;
                    }
                }
            }

            string input = UTF8Encoding.GetString(memory.ToArray());

            if (HandleMessagesInMainThread)
            {
                Messages.Enqueue(input);
            }
            else
            {
                instance.ReceiveMessage(input);
            }
        }

        private bool Reconnecting = false;
        private async Task AttemptReconnect(MuxyGateway.SDK instance)
        {
            if (Reconnecting)
            {
                return;
            }

            Reconnecting = true;

            if (Websocket.State != WebSocketState.Aborted)
            {
                try
                {
                    using (CancellationTokenSource src = TokenSource())
                    {
                        await Websocket.CloseAsync(WebSocketCloseStatus.EndpointUnavailable, "going away", src.Token)
                            .ConfigureAwait(false);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    // Ignore this one
                }
            }

            // Setup the reconnection setup.
            int i = 0;
            while (!Done)
            {
                Websocket = new ClientWebSocket();

                try
                {
                    using (CancellationTokenSource src = TokenSource())
                    {
                        await Websocket.ConnectAsync(TargetUri, src.Token)
                            .ConfigureAwait(false);

                        instance.HandleReconnect();
                        Reconnecting = false;
                        return;
                    }
                }
                catch (OperationCanceledException)
                {
                    // Not connected.
                    int waitMillis = 500 * (i * i + 1);
                    if (waitMillis > 30000)
                    {
                        waitMillis = 30000;
                    }

                    LogMessage(string.Format("Attempting to reconnect. attempt={0} wait={1}ms", i + 1, waitMillis));
                    Thread.Sleep(waitMillis);
                }

                i++;
            }
        }
    }
}