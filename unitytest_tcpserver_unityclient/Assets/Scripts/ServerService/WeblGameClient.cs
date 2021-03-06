using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

#if UNITY_WEBGL // && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif
using Assets.Scripts.ServerService;

using Newtonsoft.Json;

using UnityEngine;

public class WeblGameClient : MonoBehaviour, INetworkGameClient
{
    public static WeblGameClient instance;
    public string userName = "unityWebglClient";
    public IPAddress ipAdress;
    public int port;
    const int readBufferSize = 8192;

#if UNITY_WEBGL // && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void SendMessageToBrowser(string message);
    [DllImport("__Internal")]
    private static extern void SendNetworkMessageToServer(byte[] utf8byte);
    [DllImport("__Internal")]
    private static extern void ConnectWebglToServer(bool https, string ipadress, string username, string gameobjectName, string onConnectUnityCallback, string onRecieveNetworkMessageCallback);
#endif
    public void Awake()
    {
        if (instance != null)
        {
            Debug.LogError("Duplicate WebglGameClients in UnityInstance. Fatal error!!!");
            Destroy(this);
            Destroy(instance);
            return;
        }
        DontDestroyOnLoad(this);
        transform.name = "webglgameclient";
        instance = this;
    }

    public ConcurrentQueue<NetworkGameMessage> ServerMessageQueue { get; } = new ConcurrentQueue<NetworkGameMessage>();

    /// <summary>
    /// If port is -1 = https
    /// </summary>
    /// <param name="ipAdress">Ipadress to concat to wss://ipAdress or ws://ipAdress</param>
    /// <param name="port">If -1 use wss, else ws.</param>
    /// <param name="userName"></param>
    public void InitGameClient(IPAddress ipAdress, int port, string userName = null)
    {
        if(userName != null)
            this.userName = userName;
        this.ipAdress = ipAdress;
        this.port = port;
        ConnectToServer();
    }

    private void ConnectToServer()
    {
        ConnectWebglToServer(port == -1, ipAdress.ToString(), userName, name, nameof(ServerConnected), nameof(RecieveNetworkGameMessage));
    }

    private void ServerConnected()
    {
        // todo :: send join message
        try
        {
            NetworkGameMessage networkMessage = new NetworkGameMessage()
            {
                serviceName = "chat",
                operationName = "join",
                datamembers = new List<string> { JsonConvert.SerializeObject(userName) }
            };
            SendNetworkMessageToServer(networkMessage.AsJsonBytes);
        }
        catch (JsonException e)
        {
            Console.WriteLine(e.Message + e.InnerException?.Message);
        }
    }


    void RecieveNetworkGameMessage(byte[] utf8bytes) // note :: we don't need to thread this because everything was handled in webgl. This is detached from socket/server code.
    {
        try
        {
            byte[] jsonBuffer = new byte[readBufferSize];
            NetworkGameMessage networkMessage = JsonConvertUTF8Bytes.DeserializeObject<NetworkGameMessage>(utf8bytes);

            ServerMessageQueue.Enqueue(networkMessage);
        }
        catch (JsonException e)
        {
            Console.WriteLine(e.Message + e.InnerException?.Message);
        }
    }

    public void SendChatMessage(string message)
    {
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

        SendNetworkMessageToServer(networkMessage.AsJsonBytes);
    }
}
