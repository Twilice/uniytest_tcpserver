using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Text;

using Newtonsoft.Json;

namespace Assets.Scripts.ServerService
{
    public interface INetworkGameClient
    {
        void InitGameClient(IPAddress ipAdress, int port, string userName);

        void SendChatMessage(string message);

        ConcurrentQueue<NetworkGameMessage> ServerMessageQueue { get; }
    }

    [Serializable]
    public class ChatMessage
    {
        public DateTime timestamp { get; set; }
        public string user { get; set; }
        public string message { get; set; }
        [JsonIgnore]
        public string AsJsonString => JsonConvert.SerializeObject(this);
        [JsonIgnore]
        public byte[] AsJsonBytes => JsonConvertUTF8Bytes.SerializeObject(this);
    }

    // if use this contract, make sure client/server are synced.
    [Serializable]
    public class NetworkGameMessage
    {
        // replace names with enums with underlying int/byte?

        // only properties are serialized
        //[JsonProperty("service")]
        public string serviceName { get; set; }

        public string operationName { get; set; }

        public List<string> datamembers { get; set; }
        [JsonIgnore]
        public string ChatMessageAsJsonString => JsonConvert.DeserializeObject<ChatMessage>(datamembers[0]).AsJsonString;
        [JsonIgnore]
        public string AsJsonString => JsonConvert.SerializeObject(this);
        [JsonIgnore]
        public byte[] AsJsonBytes => JsonConvertUTF8Bytes.SerializeObject(this);
    }

    // note :: have not tested if convert to utf8 is needed. But anyways, it's nice to have data parity in all environments.
    public static class JsonConvertUTF8Bytes
    {
        public static byte[] SerializeObject(object obj)
        {
            return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
        }

        public static T DeserializeObject<T>(byte[] json)
        {
            return (T)JsonConvert.DeserializeObject(Encoding.UTF8.GetString(json), typeof(T));
        }
    }
}
