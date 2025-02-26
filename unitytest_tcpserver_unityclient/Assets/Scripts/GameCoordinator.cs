﻿using System;
using Assets.Scripts.ServerService;
using Assets.Scripts.ServerServiceHelper;

using InvocationFlow;
using UnityEngine;

#if UNITY_WEBGL && !UNITY_EDITOR
using unitytest_tcpserver_webglclient;
#else
using unitytest_tcpserver_tcpclient;
#endif

public class GameCoordinator : MonoBehaviour
{
    // *** global construct *** 
#region construct
    public static GameCoordinator instance;
    public static bool initialized = false;
    [RuntimeInitializeOnLoadMethod]
    public static void Construct()
    {
        if (initialized)
            return;

        if (instance == null)
        {
            instance = new GameObject("GameCoordinator").AddComponent<GameCoordinator>();
        }
        instance.Init();
    }
#endregion

    // *** variables *** 
#region variables
    public GameData gameData;
    public AudioSource audioSource;
    public LazyScriptHandler lazyScriptHandler;
    public PaintCanvas paintBoard;
    #endregion

#if UNITY_WEBGL // && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void WebGLStartGame();
#endif

    // *** functions ***
#region functions
    public void Init()
    {
        DontDestroyOnLoad(this);
        audioSource = gameObject.AddComponent<AudioSource>();
        
        const string gameDataName = "GameData";
        gameData = Resources.Load<GameData>(gameDataName);
        if (gameData == null)
            throw new NullReferenceException($"{nameof(GameCoordinator)} {transform.name} - scriptableObject type {nameof(GameData)} with name {gameDataName} is missing.");
        gameData = Instantiate(gameData); // don't change the asset object.

        initialized = true;

#if UNITY_WEBGL && !UNITY_EDITOR
        WebGLStartGame(); // not actually needed, just tells browser to start the custom html loading bar at 0.
#endif
    }

    public void Start()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        _ = ServerServiceHelper.CreateClient<WebglGameClient>();
#else
        _ = ServerServiceHelper.CreateClient<TcpGameClient>();
#endif

        lazyScriptHandler = FindObjectOfType<LazyScriptHandler>();
        paintBoard = FindObjectOfType<PaintCanvas>();
        ServerServiceHelper.RegisterChatCallBacks(RecieveChatMessage, UserJoinedMessage);
        ServerServiceHelper.RegisterGameCallBacks(RecievePixelUpdate);
    }

    public void ConnectToServer()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ServerServiceHelper.InitializeClient<WebglGameClient>(gameData.ipAdress, gameData.port, gameData.userName);
#else
        ServerServiceHelper.InitializeClient<TcpGameClient>(gameData.ipAdress, gameData.port, gameData.userName);
#endif
    }

    public void RecievePixelUpdate(Pixels pixels)
    {
        paintBoard.ReceivePixelUpdate(pixels);
    }

    public void RecieveChatMessage(ChatMessage chatMessage)
    {
        lazyScriptHandler.ReceiveChatMessage($"[{chatMessage.timestamp.Hour}:{chatMessage.timestamp.Minute}]<{chatMessage.user}>: {chatMessage.message}");
    }

    public void UserJoinedMessage(ChatMessage chatMessage)
    {
        lazyScriptHandler.ReceiveChatMessage($"[{chatMessage.timestamp.Hour}:{chatMessage.timestamp.Minute}]<{chatMessage.user}>: {chatMessage.message}");
    }

#endregion
}
