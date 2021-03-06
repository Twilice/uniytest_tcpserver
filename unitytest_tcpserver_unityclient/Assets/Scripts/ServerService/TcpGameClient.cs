using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
//using System.Text.Json;
//using System.Text.Json.Serialization;
using Newtonsoft.Json;
using System.IO;
using UnityEngine;
using Assets.Scripts.ServerService;

namespace unitytest_tcpserver_tcpclient
{
    public class TcpGameClient : INetworkGameClient
    {
        // todo :: we should have some depency injection for what services can be used / data can be sent (emulate wcf structure?)
        // todo :: or maybe just send data forward to some service, and that service has the depency injection.
        public TcpClient tcpClient = null;
        public TcpListener tcpListener = null;
        public const int readBufferSize = 8192;
        public IPAddress ipAdress;
        public int serverPort;
        public int clientPort = 0;
        public string userName = "unityTcpClient";
        public ConcurrentQueue<NetworkGameMessage> ServerMessageQueue { get; private set; }


        public TcpGameClient() {; }

        public void InitGameClient(IPAddress ipAdress, int port, string userName = null)
        {
            this.ipAdress = ipAdress;
            if (userName != null)
                this.userName = userName;
            serverPort = port;

            ServerMessageQueue = new ConcurrentQueue<NetworkGameMessage>();

            tcpClient = new TcpClient(new IPEndPoint(ipAdress, clientPort));
            tcpClient.Connect(new IPEndPoint(ipAdress, serverPort));

            // todo :: handle failures to connect

            IPEndPoint serverEndPoint = tcpClient.Client.RemoteEndPoint as IPEndPoint;
            clientPort = ((IPEndPoint)tcpClient.Client.LocalEndPoint).Port;
            Debug.Log($"Connected to server {serverEndPoint.Address}:{serverEndPoint.Port}");
            
            var stream = tcpClient.GetStream();

            try
            {
                var gameMessage = new NetworkGameMessage()
                {
                    serviceName = "default",
                    operationName = "join",
                    datamembers = new List<string> { JsonConvert.SerializeObject(userName) }
                };

                var bytes = gameMessage.AsJsonBytes;
                stream.Write(bytes, 0, bytes.Length);
            }
            catch (IOException e)
            {
                Debug.Log(e.Message + e.InnerException?.Message);
            }

            ThreadPool.QueueUserWorkItem(new WaitCallback(ReadIncomingStream<NetworkStream>), stream);
        }

        public void Disconnect()
        {

        }

        // todo :: relay request to "processIncomingStream" or similar.
        public void ReadIncomingStream<T>(object _stream) where T : NetworkStream // overload not availiable in this .net version
        {
            try
            {
                // recieve
                T stream = (T)_stream;
                // static size buffer, I think serverside .net core Span<byte> with jsonReader fixes this. But not tested.
                // note :: might be possible to read byte directly similar to how server does it with bson reader - se imported JsonDotNet.pdf for example
                byte[] jsonBuffer = new byte[readBufferSize];
                stream.Read(jsonBuffer, 0, jsonBuffer.Length);
                var gameMessage = JsonConvertUTF8Bytes.DeserializeObject<NetworkGameMessage>(jsonBuffer); 

                ServerMessageQueue.Enqueue(gameMessage);

                //ThreadPool.QueueUserWorkItem(ReadIncomingStream, stream, true);
                ThreadPool.QueueUserWorkItem(new WaitCallback(ReadIncomingStream<NetworkStream>), stream);
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

        public void ProcessIncomingStream()
        {
            // should actual message processeing be single threaded? Or should it relay again to correct "service" chat/clan/iap/gameLogic and then be "processed" for real?
        }

        public void SendChatMessage(string message)
        {
            if (tcpClient.Connected == false)
            {
                tcpClient.Dispose();
                Debug.LogWarning(" ------- not connected to server!");

                InitGameClient(ipAdress, serverPort, userName);                
            }

            try
            {
                var chatMessage = new ChatMessage()
                {
                    timestamp = DateTime.Now,
                    user = userName,
                    message = message
                };

                var gameMessage = new NetworkGameMessage()
                {
                    serviceName = "chat",
                    operationName = "message",
                    datamembers = new List<string> { chatMessage.AsJsonString }
                };
                var bytes = gameMessage.AsJsonBytes;

                var test = JsonConvert.SerializeObject(chatMessage);
                var test2 = JsonConvert.SerializeObject(gameMessage);

                var stream = tcpClient.GetStream();
                stream.Write(bytes, 0, bytes.Length);
            }
            catch (IOException e)
            {
                Debug.Log(e.Message + e.InnerException?.Message);
            }
        }
    }
}
