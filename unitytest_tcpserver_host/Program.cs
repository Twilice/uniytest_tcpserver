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
using System.Linq.Expressions;

namespace unitytest_tcpserver_host
{
    class Program
    {
        const string ipAdress = "127.0.0.1";
        const int port = 8000;

        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentUICulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            Console.WriteLine("Hello Server! Awaiting clients.");

            var server = new TcpGameServer(IPAddress.Parse(ipAdress), port);

            server.InitTcpGameServer();


            //server.CheckStreamDataAvaliable();

            //ThreadPool.QueueUserWorkItem(CheckGameMessageForServer, server, false); // error :: I maybe missjudged the inner workings to much. Figure out error, then figure out if how much can be kept as is.

            Console.WriteLine("Press <Enter> to exit the server.");
            Console.ReadLine();
        }

        //static void CheckGameMessageForServer(TcpGameServer server)
        //{
        //    server.CheckStreamDataAvaliable();

        //    Thread.Sleep(serverGameMessageCheckFrequency); // todo :: only sleep if we have already processed all requests fast enough. Else continue working!
        //    ThreadPool.QueueUserWorkItem(CheckGameMessageForServer, server, false);
        //}
    }

    public class TcpGameServer
    {
        // todo :: we should have some depency injection for what services can be used / data can be sent (emulate wcf structure?)
        // todo :: or maybe just send data forward to some service, and that service has the depency injection.
        public Thread tcpListenThread;
        public Thread websocketListenThread;
        public TcpListener tcpListener;
        public HttpListener httpListener;

        public ConcurrentDictionary<IPEndPoint, TcpClient> tcpClients = null;
        public ConcurrentDictionary<IPEndPoint, WebSocket> webClients = null;
        public const int readBufferSize = 8192;
        public IPAddress ipAdress;
        public int port;
        public string serverName = "server";

        // maybes / not implemented yet state variables
        public bool listeningToClients = true;

        private TcpGameServer() {;}
        public TcpGameServer(IPAddress ipAdress, int port)
        {
            this.ipAdress = ipAdress;
            this.port = port;
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

        //public void CheckStreamDataAvaliable()
        //{
        //    // bug :: current implementation will block other callers if client fails to send / be read. But keep as is for now until figure out why connection is dropped.
        //    // bug :: wanted to read just a little per client in case of 50000 clients we don't want to keep socket open and listen on them all...
        //    Parallel.ForEach(tcpClients.Values, client =>
        //    {
        //        if (client.Connected == false)
        //        {
        //            // can we remove while iterating? .values should be a copy of the values and we remove keys so should be fine.
        //            tcpClients.Remove(client.Client.RemoteEndPoint, out _);
        //            return;
        //        }


        //        using (var stream = client.GetStream())
        //        {
        //            ReadIncomingStream(stream);
        //        }
        //    });
        //}

        public void ListenIncomingClients()
        {
            tcpListener = new TcpListener(ipAdress, port);
            tcpListener.Start();
            Console.WriteLine("Server is listening");

            while (listeningToClients)
            {
                /* note :: do we need to accept these clients on different threads? 
                        :: Example 1 client is really buggy and disconnects. Will it still try to block and connect.
                        ::  Or is it at .Read() that it will block? Need to figure out which thread is blocked and code accordingly. */

                TcpClient client = tcpListener.AcceptTcpClient();

                // todo :: handle failures to connect
                client.ReceiveBufferSize = readBufferSize;


                IPEndPoint clientEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                Console.WriteLine($"Client connected from {clientEndPoint.Address}:{clientEndPoint.Port}");
                
                tcpClients[clientEndPoint] = client;              

                var stream = client.GetStream();

                ThreadPool.QueueUserWorkItem(ReadIncomingStream, stream, true);
            }
        }

        public void ListenIncomingWebsocketClient()
        {
            httpListener = new HttpListener();
            httpListener.Prefixes.Add($"http://{ipAdress}/");
            httpListener.Prefixes.Add($"https://{ipAdress}/");
            httpListener.Start();

            while (listeningToClients)
            {
                var listenContext = httpListener.GetContext();
                IPEndPoint clientEndPoint = listenContext.Request.RemoteEndPoint;
                var task = listenContext.AcceptWebSocketAsync(null, TimeSpan.FromSeconds(10));
                var webSocketClient = task.Result.WebSocket; // result is blocking
                Console.WriteLine($"Client connected from {clientEndPoint.Address}:{clientEndPoint.Port}");
                webClients[clientEndPoint] = webSocketClient;

                ThreadPool.QueueUserWorkItem(ReadIncomingWebSocketMessage, (clientEndPoint, webSocketClient), true);
            }
        }

        public void ReadIncomingWebSocketMessage((IPEndPoint endpoint, WebSocket socket) webClient)
        {
            try
            {
                Memory<byte> buffer = new Memory<byte>(new byte[readBufferSize]); // todo :: heap allocated every frame, maybe reserve memory per client instead?
                var recieve = webClient.socket.ReceiveAsync(buffer, CancellationToken.None);
                var res = recieve.Result;
                var eof = res.EndOfMessage;

                if (eof == false)
                {
                    Console.WriteLine("Important! ----- buffer to small. Increase buffer size or read message in chunks."); // todo :: dynamic buffer size? both for socket and tcp.
                }

                if (res.MessageType == WebSocketMessageType.Close)
                {
                    // note :: is this correct? Documentation says initaite or complete the close handshake. So assume client wants to know everything went ok? Or is that done under da hood?
                    webClient.socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "client initiated disconnect", CancellationToken.None).Wait();
                    webClients.TryRemove(webClient.endpoint, out _);
                }

                var jsonReader = new Utf8JsonReader(buffer.Span); // todo :: not sure if buffer here is the json payload from websocket
                var gameMessage = JsonSerializer.Deserialize<NetworkGameMessage>(ref jsonReader);

                ProcessIncomingGameMessage(gameMessage);

                ThreadPool.QueueUserWorkItem(ReadIncomingWebSocketMessage, webClient, true);
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
                var gameMessage = JsonSerializer.Deserialize<NetworkGameMessage>(ref jsonReader);
                //// todo :: handle stream / tcp package failures and json serialize failures etc.

                ProcessIncomingGameMessage(gameMessage);
                ThreadPool.QueueUserWorkItem(ReadIncomingStream, stream, true);
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
        public void ProcessIncomingGameMessage(NetworkGameMessage gameMessage)
        {
            Console.WriteLine($"Server recieved: {gameMessage.serviceName} + {gameMessage.operationName}");

            if (gameMessage.operationName == "message")
            {
                var chatMessage = JsonSerializer.Deserialize<ChatMessage>(gameMessage.datamembers[0]);
                BroadcastChatMessage(chatMessage);
            }
            else if (gameMessage.operationName == "join")
            {
                // note : add to concurrentDictionary?`Or maybe just have it because it looks "cool".
                var userName = JsonSerializer.Deserialize<string>(gameMessage.datamembers[0]);
                BroadcastChatMessage(new ChatMessage()
                {
                    timestamp = DateTime.Now,
                    user = "server",
                    message = $"new client <{userName}> joined the server"
                });
            }
            else if (gameMessage.operationName == "parsemessagetest")
            {
                var chatMessage = JsonSerializer.Deserialize<ChatMessage>(gameMessage.datamembers[0]);
            }

            //var bytes = JsonSerializer.SerializeToUtf8Bytes<TcpGameMessage>(gameMessage);
            //stream.Write(bytes);
        }

        public void BroadcastChatMessage(ChatMessage message)
        {
            Console.WriteLine($"[{message.timestamp.Hour}:{message.timestamp.Minute}]<{message.user}>: {message.message}");


            Parallel.ForEach(tcpClients, client =>
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
                    var networkMessage = new NetworkGameMessage() { serviceName = "chat", operationName = "message", datamembers = new List<string> { message.AsJsonString } };

                    // debug bytes
                    //Console.WriteLine(networkMessage.AsJsonString);
                    //var bytes = networkMessage.AsJsonBytes;
                    //foreach (var b in bytes)
                    //    Console.Write(b + " ");
                    //Console.WriteLine();
                    //Console.WriteLine();

                    stream.Write(networkMessage.AsJsonBytes);
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

            });

            Parallel.ForEach(webClients, client =>
            {
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
                    var networkMessage = new NetworkGameMessage() { serviceName = "chat", operationName = "message", datamembers = new List<string> { message.AsJsonString } };

                    webClient.SendAsync(networkMessage.AsJsonBytes, WebSocketMessageType.Binary, true, CancellationToken.None).Wait();
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
            });
        }

        public void SendMessage()
        {

        }

        public class ChatMessage
        {
            public DateTime timestamp { get; set; }
            public string user { get; set; }
            public string message { get; set; }

            [JsonIgnore]
            public string AsJsonString => JsonSerializer.Serialize(this);
            [JsonIgnore]
            public byte[] AsJsonBytes => JsonSerializer.SerializeToUtf8Bytes(this);
        }

        // if use this contract, make sure client/server are synced.
        public class NetworkGameMessage
        {
            // replace names with enums with underlying int/byte?

            // only properties are serialized - Unity does the opposite...
            //[JsonPropertyName("service")]
            public string serviceName { get; set; }

            public string operationName { get; set; }
            public List<string> datamembers { get; set; }
            
            //public List<byte[]> datamembers { get; set; } // to much issue to get javascript to encode/decode like this. Also my brain hurts trying to read the bytes.

            [JsonIgnore]
            public string ChatMessageAsJsonString => JsonSerializer.Deserialize<ChatMessage>(datamembers[0]).AsJsonString;
            [JsonIgnore]
            public string AsJsonString => JsonSerializer.Serialize(this);
            [JsonIgnore]
            public byte[] AsJsonBytes => JsonSerializer.SerializeToUtf8Bytes(this);
        }
    }
}
