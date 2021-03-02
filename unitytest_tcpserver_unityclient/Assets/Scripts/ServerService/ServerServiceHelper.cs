using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using UnityEngine;

using unitytest_tcpserver_client;

namespace Assets.Scripts.ServerService
{
    public class ServerServiceHelper : MonoBehaviour
    {
        public List<Task<Action>> taskList = new List<Task<Action>>();
        public static ServerServiceHelper instance;

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

        //public static void InitService<T>(T alreadyInitializedService = null) where T : class, IServerService, new() // note : for depency injection. But think I will go other direction for now.
        public static void InitService(string ipAdress, int port, string name)
        {
            if (instance == null)
            {
                instance = new GameObject("ServerServiceHelper").AddComponent<ServerServiceHelper>();
                DontDestroyOnLoad(instance);
            }

            instance.client = new TcpGameClient(IPAddress.Parse(ipAdress), port, name);
            instance.client.InitTcpGameClient();
        }

        public static void SendChatMessage(string message, Action onComplete, Action onFail)
        {
            Task<Action> task = Task.Run(() =>
            {
                try
                {
                    var success = instance.client.SendMessage(message); // error :: for some reason data doesn't seem to be flushed to server until after task is completed!?
                    if (success)
                        return onComplete;
                    else
                        return onFail;
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

        private TcpGameClient client;

        private void HandleIncomingMessages()
        {
            while (client.serverMessageQueue.Count != 0)
            {
                client.serverMessageQueue.TryDequeue(out var serverMessage);

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

        private void GameService(TcpGameMessage serverMessage)
        {

        }

        // todo :: fancy interface something to autohandle/semi-autohandle operations and their parameters.
        private void ChatService(TcpGameMessage serverMessage)
        {
            if (serverMessage.operationName == "message")
            {
                Task<Action> task = Task.Run(() =>
                {
                    try
                    {
                        ChatMessage message = JsonConvertUTF8Bytes.DeserializeObject<ChatMessage>(serverMessage.datamembers[0]);
                        return (Action)(() => { messageCallback(message); });
                    }
                    finally
                    {
                        try
                        {
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Error happened while processing json:\n\n" + e);
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
                        ChatMessage message = JsonConvertUTF8Bytes.DeserializeObject<ChatMessage>(serverMessage.datamembers[0]);
                        return (Action)(() => { userjoinCallback(message); });
                    }
                    finally
                    {
                        try
                        {
                        }
                        catch (Exception e)
                        {
                            Debug.LogError("Error happened while processing json:\n\n" + e);
                        }
                    }
                });
                instance.taskList.Add(task);
            }
        }
    }
}
