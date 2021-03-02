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

namespace unitytest_tcpserver_client
{
    class Program
    {
        const string ipAdress = "127.0.0.1";
        const int port = 8000;
        const int clientGameMessageCheckFrequency = 5;


        static void Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentUICulture = Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            Console.WriteLine("Hello Client! Press <Enter> to connect to server.");
            Console.ReadLine();

            var client = new TcpGameClient(IPAddress.Parse(ipAdress), port);

            client.InitTcpGameClient();

            //ThreadPool.QueueUserWorkItem(CheckGameMessagesForClient, client, false); // error :: I maybe missjudged the inner workings to much. Figure out error, then figure out if how much can be kept as is.

            bool programRunning = true;
            string userInput = null;
            while (programRunning)
            {
                Console.WriteLine();
                Console.WriteLine("Send data to server.");
                Console.WriteLine();
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
            static void CheckGameMessagesForClient(TcpGameClient client) 
            {
                client.CheckStreamDataAvaliable();

                Thread.Sleep(clientGameMessageCheckFrequency); // todo :: only sleep if we have already processed all requests fast enough. Else continue working!
                ThreadPool.QueueUserWorkItem(CheckGameMessagesForClient, client, false);
            }

            // todo :: read console, send to server
        }

        public class TcpGameClient
        {
            // todo :: we should have some depency injection for what services can be used / data can be sent (emulate wcf structure?)
            // todo :: or maybe just send data forward to some service, and that service has the depency injection.
            public TcpClient tcpClient = null;
            public const int readBufferSize = 8192;
            public IPAddress ipAdress;
            public int port;


            private TcpGameClient() {;}
            public TcpGameClient(IPAddress ipAdress, int port)
            {
                this.ipAdress = ipAdress;
                this.port = port;
            }

            public void InitTcpGameClient()
            {
                tcpClient = new TcpClient(new IPEndPoint(ipAdress, 0));
                tcpClient.Connect(new IPEndPoint(ipAdress, port));

                // todo :: handle failures to connect



                IPEndPoint serverEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
                Console.WriteLine($"Connected to server {serverEndPoint.Address}:{serverEndPoint.Port}");
                Console.WriteLine();
            }

            public void CheckStreamDataAvaliable()
            {
                if (tcpClient.Connected == false) return;

                using (var stream = tcpClient.GetStream())
                {
                    ReadIncomingStream(stream);
                }
            }


            public void Disconnect()
            {

            }

            // todo :: relay request to "processIncomingStream" or similar.
            public void ReadIncomingStream(NetworkStream stream)  // fake type safety
            {
                // recieve
                Span<byte> jsonSpan = new byte[readBufferSize];
                stream.Read(jsonSpan);
                var jsonReader = new Utf8JsonReader(jsonSpan);
                var gameMessage = JsonSerializer.Deserialize<TcpGameMessage>(ref jsonReader);

                Console.WriteLine($"Client recieved: {gameMessage.serviceName} + {gameMessage.operationName} + {gameMessage.ChatMessageAsJsonString}");

                // send back same message
                //var bytes = JsonSerializer.SerializeToUtf8Bytes<TcpRequest>(request);
                //stream.Write(bytes);
            }

            public void ProcessIncomingStream()
            {
                // should actual message processeing be single threaded? Or should it relay again to correct "service" chat/clan/iap/gameLogic and then be "processed" for real?
            }

            public void SendMessage(string message)
            {
                if (tcpClient.Connected == false)
                {
                    Console.WriteLine("ERROR: not connected to server!");
                    Console.WriteLine();
                    return;
                }

                //Task.Run(() =>
                //{
                    try
                    {
                        var chatMessage = new ChatMessage()
                        {
                            timestamp = DateTime.Now,
                            user = "Tobias",
                            message = message
                        };

                        var gameMessage = new TcpGameMessage()
                        {
                            serviceName = "default",
                            operationName = "message",
                            datamembers = new List<byte[]> { JsonSerializer.SerializeToUtf8Bytes<ChatMessage>(chatMessage) }
                        };
                        var bytes = JsonSerializer.SerializeToUtf8Bytes<TcpGameMessage>(gameMessage);

                        Console.WriteLine($"Client sending: {gameMessage.serviceName} + {gameMessage.operationName} + {gameMessage.ChatMessageAsJsonString}");

                        Console.WriteLine(JsonSerializer.Deserialize<TcpGameMessage>(bytes).ChatMessageAsJsonString);

                        var stream = tcpClient.GetStream();
                        stream.Write(bytes);
                        // temp :: for testing only
                        ReadIncomingStream(stream);
                }
                finally
                    {
                        try
                        {
                            ;
                        }
                        catch (Exception e)
                        {
                            ;
                        }
                    }
                //});
            }
        }

        public class ChatMessage
        {
            public DateTime timestamp { get; set; }
            public string user { get; set; }
            public string message { get; set; }

            [JsonIgnore]
            public string AsJsonString => JsonSerializer.Serialize(this);
        }

            // if use this contract, make sure client/server are synced.
        public class TcpGameMessage
        {
            // only properties are serialized
            [JsonPropertyName("service")]
            public string serviceName { get; set; }

            [JsonPropertyName("func")]
            public string operationName { get; set; }

            [JsonPropertyName("data")]
            public List<byte[]> datamembers { get; set; }

            [JsonIgnore]
            public string ChatMessageAsJsonString => JsonSerializer.Deserialize<ChatMessage>(datamembers[0]).AsJsonString;
        }
    }
}
