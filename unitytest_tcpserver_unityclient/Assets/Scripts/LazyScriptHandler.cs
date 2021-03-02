using System.Collections;
using System.Collections.Generic;

using Assets.Scripts.ServerService;

using UnityEngine;
using UnityEngine.UI;

public class LazyScriptHandler : MonoBehaviour
{
    public Text chatMessages;

    public void SendChatMessage(string message)
    {
        ServerServiceHelper.SendChatMessage(message, null, null);
    }

    public void RecieveChatMessage(string message)
    {
        chatMessages.text += message + "\n";
    }
}
