using System;
using System.Net;

using Newtonsoft.Json;

using UnityEngine;

namespace Assets.Scripts.ServerService
{
    public class ServerServiceHelper : MonoBehaviour
    {
        public static ServerServiceHelper instance;
        private INetworkGameClient client;
        private bool initialized = false;

        void Update()
        {
            if (!initialized) return;

            HandleIncomingMessages();
        }

        public static void ErrorDebugMessage(Exception e, string additionalInfo = "")
        {
            Debug.LogError($"{additionalInfo}\n{e.Message}\n{e.InnerException?.Message}\n{e.StackTrace}\n{e.InnerException?.StackTrace}");
            SendMessageToBrowser($"{additionalInfo}\n{e.Message}\n{e.InnerException?.Message}\n{e.StackTrace}\n{e.InnerException?.StackTrace}");
        }


        // temp :: for webgl test to figure out bugs
#if UNITY_WEBGL  && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void SendMessageToBrowser(string message);
#else
        private static void SendMessageToBrowser(string _){;}
#endif

        // global helper functions
        /// <summary>
        /// Create ServiceHelper instance and create NetworkClient. Client will be stored in static field in ServiceHelper and be used by the static helper functions.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="alreadyInstancedClient">If client is already created by something else.</param>
        /// <returns></returns>
        public static INetworkGameClient CreateClient<T>(T alreadyInstancedClient = null) where T : class, INetworkGameClient, new()
        {
            if (instance == null)
            {
                instance = new GameObject("ServerServiceHelper").AddComponent<ServerServiceHelper>();
                DontDestroyOnLoad(instance);
            }

            if (alreadyInstancedClient != null)
            {
                instance.client = alreadyInstancedClient;
            }
            else
            {
                instance.client = new T();
            }

            return instance.client;
        }

        public static void InitializeClient<T>(string ipAdress, int port, string name) where T : class, INetworkGameClient, new()
        {
            SendMessageToBrowser("init client");

            if (instance != null && instance.client != null)
            {
                instance.initialized = true;
                instance.client.InitGameClient(IPAddress.Parse(ipAdress), port, name);
            }
            else
            {
                Debug.LogError("ServerServiceHelper or NetworkClient is not created, but tried to connect client to server");
            }
        }

        public static void SendChatMessage(string message)
        {
            try
            {
                SendMessageToBrowser("helper send chatmessage");
                instance.client.SendChatMessage(message);
            }
            catch (Exception e)
            {
                ErrorDebugMessage(e);
            }
        }
    
        public static void ListenOnGameService()
        {

        }

        /* note :: this is a very special way of doing it because you can't do anything with gameobjects unless it's in unity mainthread. 
                But since we have 2 services 1 for tcp and another for websocket and they are very different it's not to bad since can work for both ways.
                Would want/need to rewrite the way we "subscribe with the callbacks" though.
        */
        Action<ChatMessage> messageCallback = (m) => { Debug.LogWarning("Unhandled message operation"); };
        Action<ChatMessage> userjoinCallback = (m) => { Debug.LogWarning("Unhandled join operation"); };
        public static void RegisterChatCallBacks(Action<ChatMessage> messageCallback, Action<ChatMessage> userjoinCallback)
        {
            SendMessageToBrowser("register helper callbacks");

            if (instance == null)
            {
                Debug.LogError("ServerServiceHelper is not initialized, but tried to listen on ChatService");
                return;
            }
            instance.messageCallback = messageCallback;
            instance.userjoinCallback = userjoinCallback;
        }


        private void HandleIncomingMessages()
        {
            SendMessageToBrowser("handle incoming messages");
            var messages = client.GetUnproccesdNetworkMessages();
            foreach(var msg in messages)
            {
                switch (msg.serviceName)
                {
                    case "game":
                        GameService(msg);
                        break;
                    case "chat":
                        ChatService(msg);
                        break;
                    default:
                        break;
                }
            }
        }

        private void GameService(NetworkGameMessage serverMessage)
        {

        }

        // todo :: fancy interface something to autohandle/semi-autohandle operations and their parameters.
        private void ChatService(NetworkGameMessage serverMessage)
        {
            if (serverMessage.operationName == "message")
            {
                try 
                { 
                    ChatMessage message = JsonConvert.DeserializeObject<ChatMessage>(serverMessage.datamembers[0]);
                    messageCallback(message);
                }
                catch (JsonException e)
                {
                    ErrorDebugMessage(e, "Error happened while processing json - service chat - operation join:\n");
                }
                catch (Exception e)
                {
                    ErrorDebugMessage(e, "Unknown Error happened while calling service chat - operation message:\n");
                }
            }
            else if (serverMessage.operationName == "join")
            {
                try
                {
                    ChatMessage message = JsonConvert.DeserializeObject<ChatMessage>(serverMessage.datamembers[0]);
                    userjoinCallback(message);
                }
                catch (JsonException e)
                {
                    ErrorDebugMessage(e, "Error happened while processing json - service chat - operation join:\n");
                }
                catch (Exception e)
                {
                    ErrorDebugMessage(e, "Unknown Error happened while calling service chat - operation message:\n");
                }
            }
            else
            {
                // todo : unknown operation, just skip could be spam
            }
        }
    }
}
