using System;
using System.Collections.Generic;
using System.Net;

#if UNITY_WEBGL // && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using Assets.Scripts.ServerService;

using Newtonsoft.Json;

using UnityEngine;

namespace unitytest_tcpserver_webglclient
{
   

    public class WebglGameClient : INetworkGameClient
    {
        internal class WebglGameClientListenServer : MonoBehaviour
        {
            private static WebglGameClientListenServer inst;
            public WebglGameClient networkClientRef;

            public void Awake()
            {
                SendMessageToBrowser("awaken lister on webgl");
                if (inst != null)
                {
                    Debug.LogError("Duplicate WebglGameClients in UnityInstance. Fatal error!!!");
                    Destroy(inst);
                    Destroy(this);
                    return;
                }
                DontDestroyOnLoad(this);
            }

            [UnityEngine.Scripting.Preserve]
            void RecieveNetworkGameMessage(string jsonString) // note :: we don't need to thread this because everything was handled in webgl. This is detached from socket/server code.
            {
                instance.RecieveNetworkGameMessage(jsonString);
            }

            [UnityEngine.Scripting.Preserve]
            private void ServerConnected()
            {
                instance.ServerConnected();
            }
        }

        public static WebglGameClient instance;
        internal static WebglGameClientListenServer listenInstance; 
        public string userName = "unityWebglClient";
        public string ipAdress;
        public int port;
        const int readBufferSize = 8192;

#if UNITY_WEBGL // && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void SendMessageToBrowser(string message);
        [DllImport("__Internal")]
        private static extern void SendNetworkMessageToServer(string utf8byte);
        [DllImport("__Internal")]
        private static extern void ConnectWebglToServer(int https, string ipadress, string username);
        [DllImport("__Internal")]
        private static extern void RegisterCallBackToWebgl(string gameobjectName, string onConnectUnityCallback, string onRecieveNetworkMessageCallback);
#else
        private static void SendMessageToBrowser(string _) {;}
        private static void SendNetworkMessageToServer(string _) {;}
        private static void ConnectWebglToServer(int _, string _2, string _3) {;}
        private static void RegisterCallBackToWebgl(string _, string _2, string _3) {;}
#endif

        public WebglGameClient()
        {
            instance = this;
            listenInstance = new GameObject("webglgameclient").AddComponent<WebglGameClientListenServer>();
        }

        public Queue<NetworkGameMessage> networkMessageQueue;

        /// <summary>
        /// If port is -1 = https
        /// </summary>
        /// <param name="ipAdress">Ipadress to concat to wss://ipAdress or ws://ipAdress</param>
        /// <param name="port">If -1 use wss, else ws.</param>
        /// <param name="userName"></param>
        public void InitGameClient(string ipAdress, int port, string userName = null)
        {
            networkMessageQueue = new Queue<NetworkGameMessage>();

            SendMessageToBrowser("init");
            if (userName != null)
                this.userName = userName;

            this.ipAdress = ipAdress;
            this.port = port;
            ConnectToServer();
        }

        private void ConnectToServer()
        { 
            try
            {
                SendMessageToBrowser("begin register callbacks");

                // bug :: is is nameof that doesn't work for webgl? But it should be compile time constant whut...?
                //RegisterCallBackToWebgl(name, nameof(ServerConnected), nameof(RecieveNetworkGameMessage));
                RegisterCallBackToWebgl(listenInstance.name, "ServerConnected", "RecieveNetworkGameMessage");

                SendMessageToBrowser("begin connect");

                int https = 0;
                if (port == -1)
                {
                    https = 1;
                }
                ConnectWebglToServer(https, ipAdress, userName);
                    //ConnectWebglToServer();
            }
            // temp :: debug
            catch (Exception e)
            {
                SendMessageToBrowser(e.Message + e.InnerException?.Message + e.StackTrace + e.InnerException?.StackTrace);
            }
        }

        internal void ServerConnected()
        {
            SendMessageToBrowser("server connected");
            // todo :: send join message
            try
            {
                NetworkGameMessage networkMessage = new NetworkGameMessage()
                {
                    serviceName = "chat",
                    operationName = "join",
                    datamembers = new List<string> { JsonConvert.SerializeObject(userName) }
                };
                SendNetworkMessageToServer(networkMessage.AsJsonString);
            }
            catch (JsonException e)
            {
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }


        //internal void RecieveNetworkGameMessage(byte[] utf8bytes) // note :: we don't need to thread this because everything was handled in webgl. This is detached from socket/server code.
        internal void RecieveNetworkGameMessage(string jsonString) // note :: we don't need to thread this because everything was handled in webgl. This is detached from socket/server code.
        {
            SendMessageToBrowser("recieve message");
            try
            {
                //byte[] jsonBuffer = new byte[readBufferSize];
                NetworkGameMessage networkMessage = JsonConvert.DeserializeObject<NetworkGameMessage>(jsonString);

                networkMessageQueue.Enqueue(networkMessage);
            }
            catch (JsonException e)
            {
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }

        public void SendChatMessage(string message)
        {
            SendMessageToBrowser("send chatmessage");
            // todo :: "re"ConnectToServer?

            ChatMessage chatMessage = new ChatMessage()
            {
                timestamp = DateTime.Now,
                user = userName,
                message = message
            };

            NetworkGameMessage networkMessage = new NetworkGameMessage()
            {
                serviceName = "chat",
                operationName = "message",
                datamembers = new List<string> { chatMessage.AsJsonString }
            };

            SendNetworkMessageToServer(networkMessage.AsJsonString);
        }

        public List<NetworkGameMessage> GetUnproccesdNetworkMessages(int maxMessagesToProcess = -1)
        {
            List<NetworkGameMessage> messagesToProcess = new List<NetworkGameMessage>();
            if (maxMessagesToProcess == -1)
            {
                while (0 < networkMessageQueue.Count)
                {
                    messagesToProcess.Add(networkMessageQueue.Dequeue());
                }
            }
            else
            {
                while (messagesToProcess.Count < maxMessagesToProcess && 0 < networkMessageQueue.Count)
                {
                    messagesToProcess.Add(networkMessageQueue.Dequeue());
                }
            }
            return messagesToProcess;
        }
    }
}