using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/GameData", order = 2)]
public class GameData : ScriptableObject
{
    public AudioClip errorSound;
    public string ipAdress = "127.0.0.1";
    public int port = 8000;
    public string userName = "unknown";

    public void Awake()
    {

    }
}
