using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Net.Security;
using System.Net.Sockets;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MuxyGateway
{
    public enum Stage { Production = 0, Sandbox = 1 }

    /// <summary>
    /// Owns one connection session. Call Update on the SDK's owning/main thread:
    /// native payload extraction, reconnect handling and callbacks run there.
    /// Socket tasks never call into the native SDK or Unity APIs.
    /// </summary>
    public class WebsocketTransport : IDisposable
    {
        private sealed class Connection : IDisposable
        {
            // Own the TCP/TLS stream during the upgrade as well as after it.
            // Some Unity Mono ClientWebSocket versions cannot abort a stalled
            // HTTP/TLS handshake because they do not retain that stream yet.
            private readonly TcpClient Tcp = new TcpClient();
            private readonly object SocketGate = new object();
            private Stream Stream;
            internal WebSocket Socket;
            internal readonly ConcurrentQueue<string> Incoming = new ConcurrentQueue<string>();
            internal readonly ConcurrentQueue<byte[]> Outgoing = new ConcurrentQueue<byte[]>();
            internal int NotifyReconnect;
            internal bool Active;
            internal async Task ConnectAsync(Uri target, CancellationToken token)
            {
                using (token.Register(Abort))
                {
                    token.ThrowIfCancellationRequested();
                    await Tcp.ConnectAsync(target.DnsSafeHost, target.Port).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    Stream stream = Tcp.GetStream();
                    Stream = stream;
                    if (target.Scheme == "wss")
                    {
                        // No certificate-validation override: validate the
                        // certificate chain and hostname using the runtime.
                        var tls = new SslStream(stream, false);
                        Stream = stream = tls;
                        // Unity 2020.3 UnityTLS rejects SslProtocols.None. TLS 1.2
                        // is supported across the declared Editor range.
                        await tls.AuthenticateAsClientAsync(target.DnsSafeHost, null, SslProtocols.Tls12, false).ConfigureAwait(false);
                    }
                    token.ThrowIfCancellationRequested();
                    string key;
                    using (RandomNumberGenerator random = RandomNumberGenerator.Create())
                    {
                        var nonce = new byte[16]; random.GetBytes(nonce);
                        key = Convert.ToBase64String(nonce);
                    }
                    byte[] request = Encoding.ASCII.GetBytes("GET " + target.PathAndQuery + " HTTP/1.1\r\nHost: " + target.Authority +
                        "\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Version: 13\r\nSec-WebSocket-Key: " + key + "\r\n\r\n");
                    await stream.WriteAsync(request, 0, request.Length, token).ConfigureAwait(false);
                    var header = new StringBuilder();
                    var one = new byte[1];
                    int end = 0;
                    const string terminator = "\r\n\r\n";
                    while (end != terminator.Length)
                    {
                        if (await stream.ReadAsync(one, 0, 1, token).ConfigureAwait(false) == 0 || header.Length >= 16384)
                            throw new WebSocketException("Invalid WebSocket upgrade response.");
                        char value = (char)one[0]; header.Append(value);
                        end = value == terminator[end] ? end + 1 : (value == '\r' ? 1 : 0);
                    }
                    ValidateUpgrade(header.ToString(), key);
                    lock (SocketGate)
                    {
                        token.ThrowIfCancellationRequested();
                        // This factory is part of .NET Standard 2.0; framing,
                        // masking, ping/pong and close parsing remain in .NET.
#pragma warning disable 618
                        Socket = WebSocket.CreateClientWebSocket(stream, null, 4096, 4096, TimeSpan.FromSeconds(20), false, new ArraySegment<byte>(new byte[16384]));
#pragma warning restore 618
                    }
                }
            }

            private static void ValidateUpgrade(string header, string key)
            {
                string[] lines = header.Split(new[] { "\r\n" }, StringSplitOptions.None);
                string[] status = lines[0].Split(' ');
                if (status.Length < 2 || status[0] != "HTTP/1.1" || status[1] != "101")
                    throw new WebSocketException("Gateway did not accept the WebSocket upgrade.");
                var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length && lines[i].Length != 0; i++)
                {
                    int colon = lines[i].IndexOf(':');
                    if (colon <= 0) throw new WebSocketException("Invalid WebSocket response header.");
                    string name = lines[i].Substring(0, colon).Trim();
                    if (fields.ContainsKey(name))
                    {
                        if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) || name.Equals("Connection", StringComparison.OrdinalIgnoreCase) || name.StartsWith("Sec-WebSocket-", StringComparison.OrdinalIgnoreCase))
                            throw new WebSocketException("Duplicate WebSocket negotiation header.");
                        continue; // Unrelated repeatable headers (for example Set-Cookie).
                    }
                    fields.Add(name, lines[i].Substring(colon + 1).Trim());
                }
                string expected;
                using (SHA1 hash = SHA1.Create())
                    expected = Convert.ToBase64String(hash.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                if (!fields.TryGetValue("Upgrade", out string upgrade) || !String.Equals(upgrade, "websocket", StringComparison.OrdinalIgnoreCase) ||
                    !fields.TryGetValue("Connection", out string connection) || !Array.Exists(connection.Split(','), value => String.Equals(value.Trim(), "Upgrade", StringComparison.OrdinalIgnoreCase)) ||
                    !fields.TryGetValue("Sec-WebSocket-Accept", out string accept) || accept != expected ||
                    (fields.TryGetValue("Sec-WebSocket-Extensions", out string extensions) && extensions.Length != 0) ||
                    (fields.TryGetValue("Sec-WebSocket-Protocol", out string protocol) && protocol.Length != 0))
                    throw new WebSocketException("Invalid WebSocket upgrade negotiation.");
            }

            internal void Abort()
            {
                lock (SocketGate)
                {
                    Socket?.Abort();
                    Tcp.Close(); // Interrupt connect, TLS and upgrade reads too.
                }
            }

            public void Dispose()
            {
                Abort();
                Socket?.Dispose();
                Stream?.Dispose();
                Tcp.Dispose();
                while (Incoming.TryDequeue(out _)) { }
                while (Outgoing.TryDequeue(out byte[] bytes)) Array.Clear(bytes, 0, bytes.Length);
            }
        }

        private sealed class Session
        {
            internal readonly Uri Target;
            internal readonly CancellationTokenSource Lifetime = new CancellationTokenSource();
            internal readonly TaskCompletionSource<bool> Ready = NewCompletion();
            internal readonly TaskCompletionSource<bool> RunRequested = NewCompletion();
            internal SDK Instance;
            internal Connection Connection;
            internal Task Worker;
            internal Session(Uri target, SDK instance) { Target = target; Instance = instance; }
        }

        private readonly object Gate = new object();
        private Session Current;
        private Task Stopping = Task.CompletedTask;
        private bool Disposed;
        private static TaskCompletionSource<bool> NewCompletion()
        {
            return new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public bool IsConnected
        {
            get { lock (Gate) return Current?.Connection?.Active == true; }
        }

        /// <summary>Last connection/reconnect failure; application payloads are never logged.</summary>
        public Exception LastConnectionError { get; private set; }

        public WebsocketTransport()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.quitting += Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
#elif UNITY_STANDALONE
            UnityEngine.Application.quitting += Dispose;
#endif
        }

#if UNITY_EDITOR
        private void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Disconnect();
        }
#endif

        /// <summary>Connects to an explicit ws:// or wss:// endpoint. Stage helpers always use WSS.</summary>
        public Task Open(string uri) { return Start(uri, null); }

        private Task Start(string uri, SDK instance)
        {
            var target = new Uri(uri, UriKind.Absolute);
            if (target.Scheme != "wss" && target.Scheme != "ws")
                throw new ArgumentException("A ws:// or wss:// endpoint is required.", nameof(uri));
            if (target.UserInfo.Length != 0 || target.Fragment.Length != 0)
                throw new ArgumentException("WebSocket endpoints cannot include user info or fragments.", nameof(uri));
            lock (Gate)
            {
                if (Disposed) throw new ObjectDisposedException(nameof(WebsocketTransport));
                if (!Stopping.IsCompleted) throw new InvalidOperationException("Await StopAsync before opening another connection.");
                if (Current != null)
                {
                    if (Current.Target != target || (Current.Instance != null && instance != null && Current.Instance != instance))
                        throw new InvalidOperationException("Stop the current connection before changing its endpoint or SDK.");
                    if (instance != null) RequestRun(Current, instance);
                    return Current.Ready.Task;
                }
                var session = new Session(target, instance);
                Current = session;
                LastConnectionError = null;
                if (instance != null) session.RunRequested.TrySetResult(true);
                session.Worker = Task.Run(() => RunSession(session));
                return session.Ready.Task;
            }
        }

        private static string StageUri(string address)
        {
            // Native versions may return a bare address or an already-prefixed URI.
            var uri = new Uri(address.Contains("://") ? address : "wss://" + address);
            if (uri.Scheme != "wss") throw new ArgumentException("Gateway stages require WSS.");
            return uri.AbsoluteUri;
        }

        public Task OpenAndRunInSandbox(SDK instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return Start(StageUri(instance.GetSandboxURL()), instance);
        }

        public Task OpenAndRunInProduction(SDK instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            return Start(StageUri(instance.GetProductionURL()), instance);
        }

        public void Run(SDK instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            lock (Gate)
            {
                if (Disposed) throw new ObjectDisposedException(nameof(WebsocketTransport));
                if (Current == null || !Current.Ready.Task.IsCompleted || Current.Ready.Task.IsFaulted || Current.Ready.Task.IsCanceled)
                    throw new InvalidOperationException("Await Open before starting the transport.");
                RequestRun(Current, instance);
            }
        }

        private static void RequestRun(Session session, SDK instance)
        {
            if (session.Instance != null && session.Instance != instance)
                throw new InvalidOperationException("The connection already belongs to another SDK.");
            session.Instance = instance;
            session.RunRequested.TrySetResult(true);
        }

        private async Task RunSession(Session session)
        {
            bool connectedBefore = false;
            int failures = 0;
            try
            {
                while (!session.Lifetime.IsCancellationRequested)
                {
                    var connection = new Connection();
                    Task sender = Task.CompletedTask, receiver = Task.CompletedTask;
                    using (var cycle = CancellationTokenSource.CreateLinkedTokenSource(session.Lifetime.Token))
                    {
                        try
                        {
                            lock (Gate)
                            {
                                session.Lifetime.Token.ThrowIfCancellationRequested();
                                session.Connection = connection;
                            }
                            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cycle.Token))
                            {
                                timeout.CancelAfter(5000);
                                await connection.ConnectAsync(session.Target, timeout.Token).ConfigureAwait(false);
                            }
                            lock (Gate)
                            {
                                cycle.Token.ThrowIfCancellationRequested();
                                connection.NotifyReconnect = connectedBefore ? 1 : 0;
                                connection.Active = true;
                            }
                            connectedBefore = true;
                            failures = 0;
                            session.Ready.TrySetResult(true);
                            await session.RunRequested.Task.ConfigureAwait(false);
                            cycle.Token.ThrowIfCancellationRequested();
                            sender = SendLoop(connection, cycle.Token);
                            receiver = ReceiveLoop(connection, cycle.Token);
                            await (await Task.WhenAny(sender, receiver).ConfigureAwait(false)).ConfigureAwait(false);
                        }
                        catch (Exception error) when (error is WebSocketException || error is SocketException || error is AuthenticationException || error is IOException || error is OperationCanceledException || error is ObjectDisposedException)
                        {
                            if (!session.Lifetime.IsCancellationRequested)
                            {
                                LastConnectionError = error;
                                if (!connectedBefore) { session.Ready.TrySetException(error); return; }
                            }
                        }
                        finally
                        {
                            lock (Gate) connection.Active = false;
                            cycle.Cancel();
                            connection.Abort();
                            try { await Task.WhenAll(sender, receiver).ConfigureAwait(false); }
                            catch (Exception) { /* Both tasks are observed; the first error is recorded above. */ }
                            lock (Gate)
                            {
                                if (session.Connection == connection) session.Connection = null;
                                connection.Dispose();
                            }
                        }
                    }
                    await Task.Delay(Math.Min(30000, 500 * ++failures), session.Lifetime.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (session.Lifetime.IsCancellationRequested) { }
            catch (Exception error) { LastConnectionError = error; session.Ready.TrySetException(error); }
            finally
            {
                lock (Gate)
                {
                    session.Ready.TrySetCanceled();
                    if (Current == session) Current = null;
                    session.Lifetime.Dispose();
                }
            }
        }

        private static async Task SendLoop(Connection connection, CancellationToken token)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                while (connection.Outgoing.TryDequeue(out byte[] bytes))
                {
                    try
                    {
                        using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            timeout.CancelAfter(5000);
                            await connection.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token).ConfigureAwait(false);
                        }
                    }
                    finally { Array.Clear(bytes, 0, bytes.Length); }
                }
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }

        private static async Task ReceiveLoop(Connection connection, CancellationToken token)
        {
            while (true)
            {
                using (var memory = new MemoryStream())
                {
                    var buffer = new byte[4096];
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await connection.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                        if (result.MessageType == WebSocketMessageType.Close) throw new EndOfStreamException("Gateway connection closed.");
                        if (result.MessageType != WebSocketMessageType.Text || memory.Length + result.Count > 16 * 1024 * 1024)
                            throw new InvalidDataException("Invalid Gateway WebSocket message.");
                        memory.Write(buffer, 0, result.Count);
                    } while (!result.EndOfMessage);
                    token.ThrowIfCancellationRequested();
                    connection.Incoming.Enqueue(Encoding.UTF8.GetString(memory.ToArray()));
                }
            }
        }

        private bool IsCurrent(Session session, Connection connection)
        {
            lock (Gate) return Current == session && session.Connection == connection && connection.Active;
        }

        /// <summary>Dispatch callbacks and queue native payloads on the owning/main thread.</summary>
        public void Update(SDK instance)
        {
            Session session;
            Connection connection;
            lock (Gate)
            {
                session = Current;
                connection = session?.Connection;
                if (connection == null || !connection.Active || session.Instance != instance) return;
            }
            // Never hold Gate across a native/user callback: it may synchronously stop/dispose.
            if (Interlocked.Exchange(ref connection.NotifyReconnect, 0) != 0 && IsCurrent(session, connection)) instance.HandleReconnect();
            while (IsCurrent(session, connection) && connection.Incoming.TryDequeue(out string message))
                instance.ReceiveMessage(message);
            if (IsCurrent(session, connection))
                instance.ForeachPayload(payload =>
                {
                    lock (Gate)
                    {
                        if (Current == session && session.Connection == connection) connection.Outgoing.Enqueue(payload.Bytes);
                        else Array.Clear(payload.Bytes, 0, payload.Bytes.Length);
                    }
                });
        }

        /// <summary>Queues native payloads on the owning/main thread; Update normally does this.</summary>
        public Task SendMessages(SDK instance) { Update(instance); return Task.CompletedTask; }

        /// <summary>Compatibility entry point. The tracked receiver already owns socket reads.</summary>
        public Task ReceiveMessage(SDK instance) { Update(instance); return Task.CompletedTask; }

        public Task StopAsync()
        {
            lock (Gate)
            {
                Session session = Current;
                if (session == null) return Stopping;
                Current = null;
                session.Lifetime.Cancel();
                session.RunRequested.TrySetCanceled();
                session.Ready.TrySetCanceled();
                session.Connection?.Abort();
                Stopping = session.Worker;
                return Stopping;
            }
        }

        // Workers never wait for Unity, so synchronous lifecycle wrappers can join safely.
        public void Disconnect() { StopAsync().GetAwaiter().GetResult(); }

        public Task DisposeAsync()
        {
            lock (Gate)
            {
                if (!Disposed)
                {
                    Disposed = true;
#if UNITY_EDITOR
                    EditorApplication.playModeStateChanged -= OnPlayModeChanged;
                    EditorApplication.quitting -= Dispose;
                    AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
#elif UNITY_STANDALONE
                    UnityEngine.Application.quitting -= Dispose;
#endif
                }
                return StopAsync();
            }
        }

        public void Dispose() { DisposeAsync().GetAwaiter().GetResult(); }
    }
}
