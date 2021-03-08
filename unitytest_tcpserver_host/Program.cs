using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Net.WebSockets;
using static unitytest_tcpserver_host.TcpGameServer;
using static unitytest_tcpserver_host.PaintGame;
using System.Runtime.CompilerServices;

namespace unitytest_tcpserver_host
{
    class Program
    {
        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentUICulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            Console.WriteLine("Hello Server! Awaiting clients.");

            var server = new TcpGameServer(IPAddress.Any);
            server.ConnectService(new ChatService(server));
            server.ConnectService(new PaintGame(server));
            server.InitTcpGameServer();

            Console.ReadLine();
            // todo :: i really should dispose of everyhing better here...
        }
    }

    public class TcpGameServer
    {
        // todo :: we should have some depency injection for what services can be used / data can be sent (emulate wcf structure?)
        // todo :: or maybe just send data forward to some service, and that service has the depency injection.
        public Thread tcpListenThread;
        public Thread websocketListenThread;
        public TcpListener tcpListener;
        public HttpListener httpListener;

        private List<INetworkService> connectedServices = new List<INetworkService>();

        public ConcurrentDictionary<IPEndPoint, TcpClient> tcpClients = null;
        public ConcurrentDictionary<IPEndPoint, WebSocket> webClients = null;
        public const int readBufferSize = 8192;
        public IPAddress ipAdress;
        const int socketPort = 8000;
        const int wssPort = 443;
        const int wsPort = 80;
        public string serverName = "server";

        // maybes / not implemented yet state variables
        public bool listeningToClients = true;
        private static JsonSerializerOptions _relaxedJsonEscaping;
        public static JsonSerializerOptions RelaxedJsonEscaping {
            get 
            {
                if (_relaxedJsonEscaping == null)
                {
                    _relaxedJsonEscaping = new JsonSerializerOptions();
                    _relaxedJsonEscaping.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
                }
                return _relaxedJsonEscaping;
            }
        }

        private TcpGameServer() {;}
        public TcpGameServer(IPAddress ipAdress)
        {
            this.ipAdress = ipAdress;
        }

        public void ConnectService(INetworkService service)
        {
            connectedServices.Add(service);
        }

        public void InitTcpGameServer()
        {
            tcpClients = new ConcurrentDictionary<IPEndPoint, TcpClient>();
            // todo :: adress and port should be in config file.

            tcpListenThread = new Thread(new ThreadStart(ListenIncomingClients));
            tcpListenThread.IsBackground = true;
            tcpListenThread.Start();

            webClients = new ConcurrentDictionary<IPEndPoint, WebSocket>();

            websocketListenThread = new Thread(new ThreadStart(ListenIncomingWebsocketClient));
            websocketListenThread.IsBackground = true;
            websocketListenThread.Start();
        }

        public void ListenIncomingClients()
        {
            tcpListener = new TcpListener(ipAdress, socketPort);
            tcpListener.Start();
            Console.WriteLine("Server is listening");

            while (listeningToClients)
            {
                IPEndPoint clientEndPoint = null;
                try
                {
                    /* note :: do we need to accept these clients on different threads? 
                            :: Example 1 client is really buggy and disconnects. Will it still try to block and connect.
                            ::  Or is it at .Read() that it will block? Need to figure out which thread is blocked and code accordingly. */

                    TcpClient client = tcpListener.AcceptTcpClient();

                    // todo :: handle failures to connect
                    client.ReceiveBufferSize = readBufferSize;


                    clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    Console.WriteLine($"Client connected from {clientEndPoint.Address}:{clientEndPoint.Port}");
                
                    tcpClients[clientEndPoint] = client;              

                    var stream = client.GetStream();

                    Task.Run(() =>
                    {
                        ReadIncomingStream(stream);
                    });
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e.Message + e.InnerException?.Message);
                    if (clientEndPoint != null)
                        tcpClients.TryRemove(clientEndPoint, out _);
                }
            }
        }

        public void ListenIncomingWebsocketClient()
        {
            httpListener = new HttpListener();

            if (ipAdress == IPAddress.Any)
            {
                httpListener.Prefixes.Add($"https://+:{wssPort}/");
                httpListener.Prefixes.Add($"http://+:{wsPort}/");
            }
            else
            {
                httpListener.Prefixes.Add($"https://{ipAdress}:{wssPort}/");
                httpListener.Prefixes.Add($"http://{ipAdress}:{wsPort}/");
            }
            httpListener.Start();

            while (listeningToClients)
            {
                IPEndPoint clientEndPoint = null;
                try
                {
                    var listenContext = httpListener.GetContext();
                    clientEndPoint = listenContext.Request.RemoteEndPoint;
                    var task = listenContext.AcceptWebSocketAsync(null, TimeSpan.FromSeconds(60));
                    var webSocketClient = task.Result.WebSocket; // result is blocking
                    Console.WriteLine($"Client connected from {clientEndPoint.Address}:{clientEndPoint.Port}");
                    webClients[clientEndPoint] = webSocketClient;

                    Task.Run(() =>
                    {
                        ReadIncomingWebSocketMessage(clientEndPoint, webSocketClient);
                    });
                }
                catch (WebSocketException e)
                {
                    Console.WriteLine(e.Message + e.InnerException?.Message);
                    if (clientEndPoint != null)
                        webClients.TryRemove(clientEndPoint, out _);
                }
            }
        }

        public void ReadIncomingWebSocketMessage(IPEndPoint endpoint, WebSocket socket)
        {
            try
            {
                Memory<byte> buffer = new Memory<byte>(new byte[readBufferSize]); // todo :: heap allocated every frame, maybe reserve memory per client instead?
                var recieve = socket.ReceiveAsync(buffer, CancellationToken.None);
                var res = recieve.Result;
                var eof = res.EndOfMessage;

                if (eof == false)
                {
                    Console.WriteLine("Important! ----- buffer to small. Increase buffer size or read message in chunks."); // todo :: dynamic buffer size? both for socket and tcp.
                }

                if (res.MessageType == WebSocketMessageType.Close)
                {
                    // note :: is this correct? Documentation says initaite or complete the close handshake. So assume client wants to know everything went ok? Or is that done under da hood?
                    socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "client initiated disconnect", CancellationToken.None).Wait();
                    webClients.TryRemove(endpoint, out _);
                }

                var jsonReader = new Utf8JsonReader(buffer.Span); // todo :: not sure if buffer here is the json payload from websocket
                var gameMessage = JsonSerializer.Deserialize<NetworkMessage>(ref jsonReader);

                ProcessIncomingNetworkMessage(gameMessage);

                Task.Run(() =>
                {
                    ReadIncomingWebSocketMessage(endpoint, socket);
                });
                //// todo :: handle stream / tcp package failures and json serialize failures etc.
            }
            catch (JsonException e)
            {
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }



        public void DisconnectTcpClient(TcpClient clientToDisconenct)
        {

        }


        // todo :: relay request to "processIncomingStream" or similar.
        public void ReadIncomingStream(NetworkStream stream)
        {
            try
            {
                // recieve
                Span<byte> jsonBuffer = new byte[readBufferSize];
                stream.Read(jsonBuffer);
                var jsonReader = new Utf8JsonReader(jsonBuffer);
                var gameMessage = JsonSerializer.Deserialize<NetworkMessage>(ref jsonReader);
                //// todo :: handle stream / tcp package failures and json serialize failures etc.

                ProcessIncomingNetworkMessage(gameMessage);
                Task.Run(() =>
                {
                    ReadIncomingStream(stream);
                });
            }
            catch (JsonException e)
            {
                // todo :: dispose of client? What to do with incorrect json... Many environments will likely cause issue.
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
            catch (IOException e)
            {
                // todo :: dispose of client? Or is error because client already is disposed?
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }

        // message processeing should probably be in it's own thread instead of socketthread? Or should it relay again to correct "service" chat/clan/iap/gameLogic and then be "processed" for real?
        public void ProcessIncomingNetworkMessage(NetworkMessage networkMessage)
        {
            var service = connectedServices.FirstOrDefault((x) => x.serviceName == networkMessage.serviceName);

            if (service == null)
            {
                Console.WriteLine("Error: Service does not exist - " + networkMessage.serviceName);
                // todo :: incorrect service, disconnect user.
            }
            else
            {
                Console.WriteLine($"Server recieved: {networkMessage.serviceName} + {networkMessage.operationName}");

                service.ProcessNetworkMessage(networkMessage);
            }
        }

        public void BroadcastNetworkMessage(NetworkMessage message)
        {
            // todo :: we should send to webclients in parallel, since if 1 tcp client is blocking we block all webclients
            Parallel.ForEach(tcpClients, client =>
            {
                SendNetworkMessageTcpClient(message, client);
            });

            Parallel.ForEach(webClients, client =>
            {
                SendNetworkMessageWebsocket(message, client);
            });
        }

        public async void SendNetworkMessageTcpClient(NetworkMessage message, KeyValuePair<IPEndPoint, TcpClient> client)
        {
            TcpClient tcpClient = client.Value;
            if (tcpClient.Connected == false)
            {
                // can we remove while iterating? .values should be a copy of the values and we remove keys so should be fine.
                var clientEndpoint = client.Key;

                tcpClients.TryRemove(clientEndpoint, out _); // - this should point to tcpClient
                                                             //disposeClient.Dispose(); // bug :: howto dispose everything correctly? I get errors when I try to "gracefully" dispose...

                return;
            }

            try
            {
                var stream = tcpClient.GetStream();
                await stream.WriteAsync(message.AsJsonBytes);
            }
            catch (IOException e)
            {
                // todo :: dispose of client? Or is error because client already is disposed?
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }


        public async void SendNetworkMessageWebsocket(NetworkMessage message, KeyValuePair<IPEndPoint, WebSocket> client)
        {
            /* error :: error can occur crashing server, example sometimes when browser reconnect or timeout?
           exception ---> System.Net.HttpListenerException (1229): An operation was attempted on a nonexistent network connection.
                       at System.Net.WebSockets.WebSocketHttpListenerDuplexStream.WriteAsyncFast(HttpListenerAsyncEventArgs eventArgs) */
            WebSocket webClient = client.Value;
            if (webClient.State != WebSocketState.Open)
            {
                // can we remove while iterating? .values should be a copy of the values and we remove keys so should be fine.
                var clientUri = client.Key;

                webClients.TryRemove(clientUri, out _); // - this should point to tcpClient
                                                        //disposeClient.Dispose(); // bug :: howto dispose everything correctly? I get errors when I try to "gracefully" dispose...

                return;
            }
            try
            {
                await webClient.SendAsync(message.AsJsonBytes, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
            catch (JsonException e)
            {
                // todo :: dispose of client? What to do with incorrect json... Many environments will likely cause issue.
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
            catch (IOException e)
            {
                // todo :: dispose of client? Or is error because client already is disposed?
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }   

    

        // if use this contract, make sure client/server are synced.
        public class NetworkMessage
        {
            // replace names with enums with underlying int/byte?

            // only properties are serialized - Unity does the opposite...
            //[JsonPropertyName("service")]
            public string serviceName { get; set; }

            public string operationName { get; set; }
            public List<string> datamembers { get; set; } // note :: I wanted this in bytes[], but webgl/browser javascript had to much issue encoding + decoding it. No problem between newtonsoft.json + .net core JsonSerializer though. Maybe possible to revisit in future?

            //public List<byte[]> datamembers { get; set; } // to much issue to get javascript to encode/decode like this. Also my brain hurts trying to read the bytes.

            [JsonIgnore]
            public string AsJsonString => JsonSerializer.Serialize(this, RelaxedJsonEscaping);
            [JsonIgnore]
            public byte[] AsJsonBytes => JsonSerializer.SerializeToUtf8Bytes(this, RelaxedJsonEscaping);
        }

        
    }
}
