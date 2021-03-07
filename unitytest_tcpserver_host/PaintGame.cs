using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using static unitytest_tcpserver_host.TcpGameServer;

namespace unitytest_tcpserver_host
{
    class PaintGame : INetworkService
    {
        TcpGameServer server;
        public string serviceName { get => "game"; private set => throw new InvalidOperationException(); }

        private PaintGame() {; }
        public PaintGame(TcpGameServer server)
        {
            this.server = server;
        }

        public void ProcessNetworkMessage(NetworkMessage message)
        {
            throw new NotImplementedException();
        }

        public class Texture
        {
            public Color[,] pixels;
            public int width;
            public int height;
            private Texture() {;}
            public Texture(int width, int height)
            {
                this.width = width;
                this.height = height;
                pixels = new Color[width, height];
            }
        }

        public class NewColor
        {
            public Color color;
            public int xPos;
            public int yPos;
            private NewColor() {;}
            public NewColor(int x, int y, Color col)
            {
                color = col;
                xPos = x;
                yPos = y;
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
            public byte red;
            public byte green;
            public byte blue;
            public byte alpha;
        }
    }
}
