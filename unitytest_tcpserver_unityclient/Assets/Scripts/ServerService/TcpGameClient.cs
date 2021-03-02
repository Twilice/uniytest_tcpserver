using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
//using System.Text.Json;
//using System.Text.Json.Serialization;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace unitytest_tcpserver_client
{
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
        public ConcurrentQueue<TcpGameMessage> serverMessageQueue = new ConcurrentQueue<TcpGameMessage>();


        private TcpGameClient() {; }
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
            Debug.Log($"Connected to server {serverEndPoint.Address}:{serverEndPoint.Port}");
            
            var stream = tcpClient.GetStream();
            ThreadPool.QueueUserWorkItem(new WaitCallback(ReadIncomingStream<NetworkStream>), stream);

            try
            {
                var gameMessage = new TcpGameMessage()
                {
                    serviceName = "default",
                    operationName = "join",
                    datamembers = new List<byte[]> { JsonUtilityUTF8Bytes.ToJson(userName) }
                };

                var test = JsonUtility.ToJson(userName);
                var test2 = JsonUtility.ToJson(gameMessage);



                var bytes = gameMessage.AsJsonBytes;
                stream.Write(bytes, 0, bytes.Length);
            }
            catch (IOException e)
            {
                Debug.Log(e.Message + e.InnerException?.Message);
            }
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
                byte[] jsonBuffer = new byte[readBufferSize];
                stream.Read(jsonBuffer, 0, jsonBuffer.Length);
                var gameMessage = JsonUtilityUTF8Bytes.FromJson<TcpGameMessage>(jsonBuffer);

                var test0 = System.Text.Encoding.UTF8.GetString(jsonBuffer);
                var test = JsonUtility.FromJson<TcpGameMessage>(test0);
                var test0b = System.Text.Encoding.UTF8.GetString(test.datamembers[0]);
                var test2 = JsonUtility.FromJson<ChatMessage>(test0b);

                serverMessageQueue.Enqueue(gameMessage);

                //ThreadPool.QueueUserWorkItem(ReadIncomingStream, stream, true);
                ThreadPool.QueueUserWorkItem(new WaitCallback(ReadIncomingStream<NetworkStream>), stream);
            }
            catch (IOException e)
            {
                Debug.Log(e.Message + e.InnerException?.Message);
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
                Debug.LogWarning(" ------- not connected to server!");

                InitTcpGameClient();                
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
                    serviceName = "default",
                    operationName = "message",
                    datamembers = new List<byte[]> { chatMessage.AsJsonBytes }
                };
                var bytes = gameMessage.AsJsonBytes;

                var test = JsonUtility.ToJson(chatMessage);
                var test2 = JsonUtility.ToJson(gameMessage);

                var stream = tcpClient.GetStream();
                stream.Write(bytes, 0, bytes.Length);
                return true;
            }
            catch (IOException e)
            {
                Debug.Log(e.Message + e.InnerException?.Message);
                return false;
            }
        }

    }

    [Serializable]
    public class ChatMessage
    {
        [field: SerializeField]
        public DateTime timestamp { get; set; }
        [field: SerializeField]
        public string user { get; set; }
        [field: SerializeField]
        public string message { get; set; }

        public string AsJsonString => JsonUtility.ToJson(this);
        public byte[] AsJsonBytes => JsonUtilityUTF8Bytes.ToJson(this);
    }

    // if use this contract, make sure client/server are synced.
    [Serializable]
    public class TcpGameMessage
    {
        // replace names with enums with underlying int/byte?

        // only properties are serialized
        [field: SerializeField]
        public string serviceName { get; set; }

        [field: SerializeField]
        public string operationName { get; set; }

        [field: SerializeField]
        public List<byte[]> datamembers { get; set; }

        public string ChatMessageAsJsonString => JsonUtilityUTF8Bytes.FromJson<ChatMessage>(datamembers[0]).AsJsonString;
        public byte[] AsJsonBytes => JsonUtilityUTF8Bytes.ToJson(this);
    }

    // note :: have not tested if convert to utf8 is needed. But anyways, it's nice to have data parity in all environments.
    public static class JsonUtilityUTF8Bytes
    {
        public static byte[] ToJson(object obj)
        {
            return System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(obj));
        }

        public static T FromJson<T>(byte[] json)
        {
            return (T)JsonUtility.FromJson(System.Text.Encoding.UTF8.GetString(json), typeof(T));
        }
    }
}
