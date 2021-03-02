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
using System.IO;

namespace unitytest_tcpserver_client
{
    class Program
    {
        const string ipAdress = "127.0.0.1";
        const int port = 8000;

        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentUICulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            Console.WriteLine("Hello Client! Enter <name> to connect to server.");
            var name = Console.ReadLine();
            Console.WriteLine();

            var client = new TcpGameClient(IPAddress.Parse(ipAdress), port, name);

            client.InitTcpGameClient();

            //ThreadPool.QueueUserWorkItem(CheckGameMessagesForClient, client, false); // error :: I maybe missjudged the inner workings to much. Figure out error, then figure out if how much can be kept as is.

            bool programRunning = true;
            string userInput = null;
            while (programRunning)
            {
                userInput = Console.ReadLine();

                if (userInput == "-1" || userInput == "exit" || userInput == "close")
                {
                    programRunning = false;
                }
                else
                {
                    client.SendMessage(userInput); // todo :: limit message size
                }
            }

            // note :: should this be done more similar to a while true .read()? - see comment in ListenIncomingClients
            //static void CheckGameMessagesForClient(TcpGameClient client) 
            //{
            //    client.CheckStreamDataAvaliable();

            //    Thread.Sleep(clientGameMessageCheckFrequency); // todo :: only sleep if we have already processed all requests fast enough. Else continue working!
            //    ThreadPool.QueueUserWorkItem(CheckGameMessagesForClient, client, false);
            //}

            // todo :: read console, send to server
        }

        public class TcpGameClient
        {
            // todo :: we should have some depency injection for what services can be used / data can be sent (emulate wcf structure?)
            // todo :: or maybe just send data forward to some service, and that service has the depency injection.
            public TcpClient tcpClient = null;
            public TcpListener tcpListener = null;
            public const int readBufferSize = 8192;
            public IPAddress ipAdress;
            public int serverPort;
            public int clientPort = 0;
            public string userName = "unknown";


            private TcpGameClient() {;}
            public TcpGameClient(IPAddress ipAdress, int port, string userName = "unknown")
            {
                this.ipAdress = ipAdress;
                this.serverPort = port;
                this.userName = userName;
            }

            public void InitTcpGameClient()
            {
                tcpClient = new TcpClient(new IPEndPoint(ipAdress, clientPort));
                tcpClient.Connect(new IPEndPoint(ipAdress, serverPort));

                // todo :: handle failures to connect

                IPEndPoint serverEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                clientPort = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Port;
                Console.WriteLine($"Connected to server {serverEndPoint.Address}:{serverEndPoint.Port}");
                Console.WriteLine();

                var stream = tcpClient.GetStream();
                ThreadPool.QueueUserWorkItem(ReadIncomingStream, stream, true);

                try
                {
                    var gameMessage = new TcpGameMessage()
                    {
                        serviceName = "default",
                        operationName = "join",
                        datamembers = new List<byte[]> { JsonSerializer.SerializeToUtf8Bytes(userName) }
                    };
                    var bytes = gameMessage.AsJsonBytes;
                    stream.Write(bytes);
                }
                catch (IOException e)
                {
                    Console.WriteLine(e.Message + e.InnerException?.Message);
                }
            }

            public void Disconnect()
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
                    var gameMessage = JsonSerializer.Deserialize<TcpGameMessage>(ref jsonReader);

                    if (gameMessage.operationName == "message")
                    {
                        var chatMessage = JsonSerializer.Deserialize<ChatMessage>(gameMessage.datamembers[0]);

                        Console.WriteLine($"[{chatMessage.timestamp.Hour}:{chatMessage.timestamp.Minute}]<{chatMessage.user}>: {chatMessage.message}");

                    }
                    else
                    {
                        Console.WriteLine("unknown operation from server " + gameMessage.serviceName + " " + gameMessage.operationName);
                    }
                    ThreadPool.QueueUserWorkItem(ReadIncomingStream, stream, true);
                }
                catch (IOException e)
                {
                    Console.WriteLine(e.Message + e.InnerException?.Message);
                }
            }

            public void ProcessIncomingStream()
            {
                // should actual message processeing be single threaded? Or should it relay again to correct "service" chat/clan/iap/gameLogic and then be "processed" for real?
            }

            public bool SendMessage(string message)
            {
                if (tcpClient.Connected == false)
                {
                    tcpClient.Dispose();
                    Console.WriteLine(" ------- not connected to server!");

                    InitTcpGameClient();
                    Console.WriteLine();
                }

                try
                {
                    var chatMessage = new ChatMessage()
                    {
                        timestamp = DateTime.Now,
                        user = userName,
                        message = message
                    };

                    var gameMessage = new TcpGameMessage()
                    {
                        serviceName = "chat",
                        operationName = "message",
                        datamembers = new List<byte[]> { chatMessage.AsJsonBytes }
                    };
                    var bytes = gameMessage.AsJsonBytes;

                    var stream = tcpClient.GetStream();
                    stream.Write(bytes);
                    return true;
                }
                catch (IOException e)
                {
                    Console.WriteLine(e.Message + e.InnerException?.Message);
                    return false;
                }
            }
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
        public class TcpGameMessage
        {
            // replace names with enums with underlying int/byte?

            // only properties are serialized - Unity does the opposite...
            public string serviceName { get; set; }

            public string operationName { get; set; }

            public List<byte[]> datamembers { get; set; }

            [JsonIgnore]
            public string ChatMessageAsJsonString => JsonSerializer.Deserialize<ChatMessage>(datamembers[0]).AsJsonString;
            [JsonIgnore]
            public byte[] AsJsonBytes => JsonSerializer.SerializeToUtf8Bytes(this);
        }
    }
}
