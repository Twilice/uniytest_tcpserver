using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using static unitytest_tcpserver_host.TcpGameServer;

namespace unitytest_tcpserver_host
{
    public class ChatService : INetworkService
    {
        TcpGameServer server;

        public string serviceName { get => "chat"; private set => throw new InvalidOperationException(); }

        private ChatService() {; }
        public ChatService(TcpGameServer server)
        {
            this.server = server;
        }

        public void ProcessNetworkMessage(NetworkMessage networkMessage)
        {
            try
            {
                if (networkMessage.operationName == "message")
                {
                    var chatMessage = JsonSerializer.Deserialize<ChatMessage>(networkMessage.datamembers[0]);
                    BroadcastChatMessage(chatMessage);
                }
                else if (networkMessage.operationName == "join")
                {
                    // note : add to concurrentDictionary?`Or maybe just have it because it looks "cool".
                    var userName = JsonSerializer.Deserialize<string>(networkMessage.datamembers[0]);
                    var chatMessage = new ChatMessage()
                    {
                        timestamp = DateTime.Now,
                        user = "server",
                        message = $"new client <{userName}> joined the server"
                    };
                    BroadcastChatMessage(chatMessage);
                }
            }
            catch (JsonException e)
            {
                // todo :: dispose of client? What to do with incorrect json... Many environments will likely cause issue.
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }


        public void BroadcastChatMessage(ChatMessage chatMessage)
        {
            try
            {
                Console.WriteLine($"[{chatMessage.timestamp.Hour}:{chatMessage.timestamp.Minute}]<{chatMessage.user}>: {chatMessage.message}");
                var networkMessage = new NetworkMessage() { serviceName = "chat", operationName = "message", datamembers = new List<string> { chatMessage.AsJsonString } };
                Task.Run(() => server.BroadcastNetworkMessage(networkMessage));
            }
            catch (JsonException e)
            {
                // todo :: dispose of client? What to do with incorrect json... Many environments will likely cause issue.
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }

        public class ChatMessage
        {
            public DateTime timestamp { get; set; }
            public string user { get; set; }
            public string message { get; set; }

            [JsonIgnore]
            public string AsJsonString => JsonSerializer.Serialize(this);
            [JsonIgnore]
            public byte[] AsJsonBytes => JsonSerializer.SerializeToUtf8Bytes(this);
        }
    }
}
