using System;
using System.Collections.Generic;
using System.Text;

using static unitytest_tcpserver_host.TcpGameServer;

namespace unitytest_tcpserver_host
{
    public interface INetworkService
    {
        string serviceName { get; }
        public void ProcessNetworkMessage(NetworkMessage message);
    }
}
