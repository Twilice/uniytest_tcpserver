using System.Collections;
using System.Collections.Generic;

using Assets.Scripts.ServerService;

using UnityEngine;
using UnityEngine.UI;

public class LazyScriptHandler : MonoBehaviour
{
    public Text chatMessages;

    public void SetIp(string ip)
    {
        GameCoordinator.instance.gameData.ipAdress = ip;
    }

    public void SetPort(int port)
    {
        GameCoordinator.instance.gameData.port = port;
    }

    public void SetUserName(string userName)
    {
        GameCoordinator.instance.gameData.userName = userName;
    }


    public void SendChatMessage(string message)
    {
        
        ServerServiceHelper.SendChatMessage(message);
    }

    public void RecieveChatMessage(string message)
    {
        chatMessages.text += message + "\n";
    }
}
