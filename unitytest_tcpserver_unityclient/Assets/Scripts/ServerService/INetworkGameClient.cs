using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

using Newtonsoft.Json;

namespace Assets.Scripts.ServerService
{
    public interface INetworkGameClient
    {
        void InitGameClient(string ipAdress, int port, string userName);

        void SendChatMessage(string message);
        void SendPixelUpdate(Pixels pixels);

        List<NetworkGameMessage> GetUnproccesdNetworkMessages(int maxMessagesToProcess = -1);
    }

    public class Pixels
    {
        public List<Pixel> pixels { get; set; } = new List<Pixel>();

        [JsonIgnore]
        public string AsJsonString => JsonConvert.SerializeObject(this);
        [JsonIgnore]
        public byte[] AsJsonBytes => JsonConvertUTF8Bytes.SerializeObject(this);
    }

    public class Pixel
    {
        public Color color { get; set; }
        // note :: to save some serialize data on every pixel. Could possibly also flatten color struct to same some more data. Or byte[4] arr
        public int x { get; set; }
        public int y { get; set; }

        private Pixel() {; }
        public Pixel(int x, int y, Color col)
        {
            color = col;
            this.x = x;
            this.y = y;
        }
        public Pixel(Position pos, Color col)
        {
            color = col;
            x = pos.x;
            y = pos.y;
        }
    }

    public struct Position : IEquatable<Position>, IComparable<Position>
    {
        public int x { get; set; }
        public int y { get; set; }

        public Position(int x, int y)
        {
            this.x = x;
            this.y = y;
        }
        public bool Equals(Position other)
        {
            return x == other.x && y == other.y;
        }

        public int CompareTo(Position other)
        {
            if (y < other.y)
                return -1;
            if (y > other.y)
                return 1;

            return 0;
        }

        public override bool Equals(object obj)
        {
            //Check for null and compare run-time types.
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                Position ncol = (Position)obj;
                return (x == ncol.x) && (y == ncol.y);
            }
        }

        public override int GetHashCode()
        {
            int hash = 17;
            hash = hash * 23 + x;
            hash = hash * 23 + y;
            return hash;
        }
    }

    public struct Color
    {
        public Color(byte r, byte g, byte b)
        {
            red = r;
            green = g;
            blue = b;
            alpha = 0;
        }
        public Color(byte r, byte g, byte b, byte a)
        {
            red = r;
            green = g;
            blue = b;
            alpha = a;
        }
        public byte red { get; set; }
        public byte green { get; set; }
        public byte blue { get; set; }
        public byte alpha { get; set; }
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
