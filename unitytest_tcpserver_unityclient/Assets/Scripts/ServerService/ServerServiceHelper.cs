using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

using Newtonsoft.Json;

using UnityEngine;

namespace Assets.Scripts.ServerService
{
    public class ServerServiceHelper : MonoBehaviour
    {
        public List<Task<Action>> taskList = new List<Task<Action>>();
        public static ServerServiceHelper instance;
        private INetworkGameClient client;
        private bool initialized = false;

        void Update()
        {
            if (!initialized) return;

            //HandleIncomingMessages();

            if (taskList.Count == 0) return;

            List<Task<Action>> completedTasks = new List<Task<Action>>();

            // note :: we don't want to lock the Unity main thread, so we let main thread handle the resulting callback.
            foreach (var task in taskList)
            {
                if (task.IsCompleted)
                {
                    completedTasks.Add(task);
                    task.Result?.Invoke();
                    SendMessageToBrowser("some task was completed");
                }
            }

            foreach (var completedTask in completedTasks)
            {
                taskList.Remove(completedTask);
            }
        }

        // temp :: for webgl test to figure out null only
#if UNITY_WEBGL // && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern void SendMessageToBrowser(string message);
#else
        private static void SendMessageToBrowser(string msg){;}
#endif

        // global helper functions
        /// <summary>
        /// Create ServiceHelper instance and create NetworkClient. Client will be stored in static field in ServiceHelper and be used by the static helper functions.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="alreadyInstancedClient">If client is already created by something else.</param>
        /// <returns></returns>
        public static INetworkGameClient CreateClient<T>(T alreadyInstancedClient = null) where T : class, INetworkGameClient, new()
        //public static void InitService(string ipAdress, int port, string name)
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
                SendMessageToBrowser("create client dynamic with new T()");
                instance.client = new T(); // bug? :: is this causing the webgl issues?
                // I'm stupid... the webgl instance is a monobehvaiour...
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
                Debug.LogError("ServerServiceHelper or NetworkClient is not initialized, but tried to listen on ChatService");
            }
        }

        // todo :: we can't force onFail into Task result if exception? We don't want to just run the onFail outside mainthread. Designwise maybe all messages must be "fire and forget" style. Then server sends the request and it's processed there.
        public static void SendChatMessage(string message, Action onComplete, Action onFail)
        {
            SendMessageToBrowser("helper send chatmessage");

            Task<Action> task = Task.Run(() =>
            {
                try
                {
                    instance.client.SendChatMessage(message);
                    return onComplete;
                }
                finally
                {
                    try
                    {

                    }
                    catch (Exception e)
                    {
                        errorCallBack(e);
                    }
                }
            });
            instance.taskList.Add(task);
        }

        public static Action<Exception> errorCallBack = (e) => { Debug.LogError("Error happened while requesting to server :\n\n" + e);
            SendMessageToBrowser(e.Message + e.InnerException?.Message + e.StackTrace + e.InnerException?.StackTrace);
        };

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

            ConcurrentQueue<NetworkGameMessage> mq = client.ServerMessageQueue;
            while (mq.Count != 0) // error :: I'm finding some sources saying concurrentbag.count doesn't work in webgl. Maybe it's cause of out of memory?
            {
                mq.TryDequeue(out var serverMessage);

                switch (serverMessage.serviceName)
                {
                    case "game":
                        GameService(serverMessage);
                        break;
                    case "chat":
                        ChatService(serverMessage);
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
                Task<Action> task = Task.Run(() =>
                {
                    try
                    {
                        ChatMessage message = JsonConvert.DeserializeObject<ChatMessage>(serverMessage.datamembers[0]);
                        return (Action)(() => { messageCallback(message); });
                    }
                    finally
                    {
                        try
                        {

                        }
                        catch (JsonException e)
                        {
                            Debug.LogError("Error happened while processing json - service chat - operation join:\n\n" + e + e.InnerException?.Message);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Unknown Error happened while calling service chat - operation message:\n\n" + e + e.InnerException?.Message);
                        }
                    }
                });
                instance.taskList.Add(task);
            }
            else if (serverMessage.operationName == "join")
            {
                Task<Action> task = Task.Run(() =>
                {
                    try
                    {
                        ChatMessage message = JsonConvert.DeserializeObject<ChatMessage>(serverMessage.datamembers[0]);
                        return (Action)(() => { userjoinCallback(message); });
                    }
                    finally
                    {
                        try
                        {

                        }
                        catch (JsonException e)
                        {
                            Debug.LogError("Error happened while processing json - service chat - operation join:\n\n" + e + e.InnerException?.Message);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Unknown Error happened while calling service chat - operation join:\n\n" + e + e.InnerException?.Message);
                        }
                    }
                });
                instance.taskList.Add(task);
            }
        }
    }
}
