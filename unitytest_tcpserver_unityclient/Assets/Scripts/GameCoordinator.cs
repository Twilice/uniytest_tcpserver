using System;
using System.Collections.Generic;

using Assets.Scripts.ServerService;

using InvocationFlow;
using UnityEngine;

using unitytest_tcpserver_client;

using Random = UnityEngine.Random;

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
    #endregion



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

        ServerServiceHelper.InitService(gameData.ipAdress, gameData.port, gameData.userName);
        ServerServiceHelper.ListenOnChatService(RecieveChatMessage, UserJoinedMessage);

        lazyScriptHandler = FindObjectOfType<LazyScriptHandler>();
        initialized = true;
    }

    public void RecieveChatMessage(ChatMessage chatMessage)
    {
        lazyScriptHandler.RecieveChatMessage($"[{chatMessage.timestamp.Hour}:{chatMessage.timestamp.Minute}]<{chatMessage.user}>: {chatMessage.message}");
    }

    public void UserJoinedMessage(ChatMessage chatMessage)
    {
        lazyScriptHandler.RecieveChatMessage($"[{chatMessage.timestamp.Hour}:{chatMessage.timestamp.Minute}]<{chatMessage.user}>: {chatMessage.message}");
    }

    #endregion
}
