using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace unitytest_tcpserver_host
{
    class Program
    {
        // todo :: write todo list.

        const string ipAdress = "127.0.0.1";
        const int port = 8080;
        const int serverRequestCheckFrequency = 10;

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");

    
            var server = new TcpServer(IPAddress.Parse(ipAdress), port);

            server.InitTcpListener();


            server.CheckStreamDataAvaliable();

            ThreadPool.QueueUserWorkItem(new WaitCallback(CheckServerRequest<TcpServer>), server);
        }

        static void CheckServerRequest<T>(object _server) where T : TcpServer
        {
            T server = (T)_server;
            server.CheckStreamDataAvaliable();

            Thread.Sleep(serverRequestCheckFrequency); // todo :: only sleep if we have already processed all requests fast enough. Else continue working!
            ThreadPool.QueueUserWorkItem(new WaitCallback(CheckServerRequest<TcpServer>), server);
        }
    }

    public class TcpServer
    {
        // todo :: we should have some depency injection for what services can be used / data can be sent (emulate wcf structure?)
        // todo :: or maybe just send data forward to some service, and that service has the depency injection.
        public Thread tcpListenThread;
        public TcpListener tcpListener;
        public ConcurrentBag<TcpClient> tcpClients = null;
        public const int readBufferSize = 8192;
        public IPAddress ipAdress;
        public int port;


        // maybes / not implemented yet state variables
        public bool listeningToClients = true;

        private TcpServer() {;}
        public TcpServer(IPAddress ipAdress, int port)
        {
            this.ipAdress = ipAdress;
            this.port = port;
        }

        public void InitTcpListener()
        {
            tcpClients = new ConcurrentBag<TcpClient>();
            // todo :: adress and port should be in config file.
            tcpListener = new TcpListener(ipAdress, 8080);
            tcpListener.Start();
            Console.WriteLine("Server is listening");

            tcpListenThread = new Thread(new ThreadStart(ListenIncomingClients));
            tcpListenThread.IsBackground = true;
            tcpListenThread.Start();
        }

        public void CheckStreamDataAvaliable()
        {
            Parallel.ForEach(tcpClients, client =>
            {
                using (var stream = client.GetStream())
                {
                    if (stream.DataAvailable)
                    {
                        ThreadPool.QueueUserWorkItem(new WaitCallback(ReadIncomingStream<NetworkStream>), client);
                    }
                }
            });
        }
        public void ListenIncomingClients()
        {
            listeningToClients = true;

            while (listeningToClients)
            {
                TcpClient client = tcpListener.AcceptTcpClient();
                client.ReceiveBufferSize = readBufferSize;
                tcpClients.Add(client);

                ThreadPool.QueueUserWorkItem(new WaitCallback(ReadIncomingStream<NetworkStream>), client);
            }
        }

      

        public void DisconnectTcpClient(TcpClient clientToDisconenct)
        {

        }


        public void ReadIncomingStream<T>(object _stream) where T : NetworkStream // fake type safety
        {

            T stream = (T)_stream;
            if(stream.DataAvailable)
            {
                // recieve
                Span<byte> jsonSpan = new Span<byte>();
                stream.Read(jsonSpan);
                var jsonReader = new Utf8JsonReader(jsonSpan);
                var request = JsonSerializer.Deserialize<TcpRequest>(ref jsonReader);

                Console.WriteLine($"Server recieved: {request.serviceName} + {request.operationName} + {request.datamembers}");

                // send back same message
                var bytes = JsonSerializer.SerializeToUtf8Bytes<TcpRequest>(request);
                stream.Write(bytes);


                ///** START example code from microsoft **/
                //String data = null;
                //Byte[] bytes = new Byte[readBufferSize];
                //int i;
                //while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                //{
                //    // do json stuff here instead
                //    data = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                //    Console.WriteLine("Received: {0}", data);

                //    // Process the data sent by the client.
                //    data = data.ToUpper();

                //    byte[] msg = System.Text.Encoding.ASCII.GetBytes(data);

                //    // Send back a response.
                //    stream.Write(msg, 0, msg.Length);
                //    Console.WriteLine("Sent: {0}", data);
                //}
                ///** END examplecode from microsoft **/
            }
        }

        public void ProcessIncomingStream()
        {
            // if request is large, use new thread to process it=?
        }

        public void BroadcastMessage()
        {

        }

        public void SendMessage()
        {

        }

        // if use this contract, make sure client also has the same.
        public class TcpRequest
        {
            // only properties are serialized
            [JsonPropertyName("service")]
            public string serviceName { get; set; }

            [JsonPropertyName("func")]
            public string operationName { get; set; }

            [JsonPropertyName("data")]
            public List<byte[]> datamembers { get; set; }
        }
    }
}
