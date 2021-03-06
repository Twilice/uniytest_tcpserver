using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using UnityEngine;

using unitytest_tcpserver_client;

namespace Assets.Scripts.ServerService
{
    public class ServerServiceHelper : MonoBehaviour
    {
        public List<Task<Action>> taskList = new List<Task<Action>>();
        public static ServerServiceHelper instance;
        private INetworkGameClient client;

        void Update()
        {
            HandleIncomingMessages();

            if (taskList.Count == 0) return;

            List<Task<Action>> completedTasks = new List<Task<Action>>();

            // note :: we don't want to lock the Unity main thread, so we let main thread handle the resulting callback.
            foreach (var task in taskList)
            {
                if (task.IsCompleted)
                {
                    completedTasks.Add(task);
                    task.Result?.Invoke();
                }
            }

            foreach (var completedTask in completedTasks)
            {
                taskList.Remove(completedTask);
            }
        }

        // global helper functions

        public static void InitService<T>(string ipAdress, int port, string name, T alreadyInitializedService = null) where T : class, INetworkGameClient, new()
        //public static void InitService(string ipAdress, int port, string name)
        {
            if (instance == null)
            {
                instance = new GameObject("ServerServiceHelper").AddComponent<ServerServiceHelper>();
                DontDestroyOnLoad(instance);
            }
            if (alreadyInitializedService != null)
            {
                instance.client = alreadyInitializedService;
            }
            else
            {
                instance.client = new T();
                instance.client.InitGameClient(IPAddress.Parse(ipAdress), port, name);
            }
        }

        // todo :: we can't force onFail into Task result if exception? We don't want to just run the onFail outside mainthread. Designwise maybe all messages must be "fire and forget" style. Then server sends the request and it's processed there.
        public static void SendChatMessage(string message, Action onComplete, Action onFail)
        {
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

        public static Action<Exception> errorCallBack = (e) => { Debug.LogError("Error happened while requesting to server :\n\n" + e); };

        public static void ListenOnGameService()
        {

        }

        /* note :: this is a very special way of doing it because you can't do anything with gameobjects unless it's in unity mainthread. 
                But since we have 2 services 1 for tcp and another for websocket and they are very different it's not to bad since can work for both ways.
                Would want/need to rewrite the way we "subscribe with the callbacks" though.
        */
        Action<ChatMessage> messageCallback = (m) => { Debug.LogWarning("Unhandled message operation"); };
        Action<ChatMessage> userjoinCallback = (m) => { Debug.LogWarning("Unhandled join operation"); };
        public static void ListenOnChatService(Action<ChatMessage> messageCallback, Action<ChatMessage> userjoinCallback)
        {
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
            while (client.ServerMessageQueue.Count != 0)
            {
                client.ServerMessageQueue.TryDequeue(out var serverMessage);

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
