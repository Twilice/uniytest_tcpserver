using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using static unitytest_tcpserver_host.TcpGameServer;

namespace unitytest_tcpserver_host
{
    class PaintGame : INetworkService
    {
        TcpGameServer server;
        public string serviceName { get => "game"; private set => throw new InvalidOperationException(); }
        private Texture texture = new Texture(64, 64);

        const int updatePerSecond = 60;
        const int delayTimeMilliseconds = 1000 / updatePerSecond;
        private PaintGame() {; }
        public PaintGame(TcpGameServer server)
        {
            this.server = server;
            Task.Run(TickPaintGame);

        }

        public void TickPaintGame()
        {
            var t = Task.Run(async () =>
            {
                List<Task> tasks = new List<Task>();
                tasks.Add(Task.Run(BroadcastNewPixels));
                tasks.Add(Task.Delay(delayTimeMilliseconds));
                await Task.WhenAll(tasks);
            });
            try
            {
                bool canceled = t.Wait(delayTimeMilliseconds * updatePerSecond);
                if (canceled)
                {
                    Console.WriteLine("PaintGame took to long to TICK - BroadCastNewPixels -" + delayTimeMilliseconds * updatePerSecond + " milliseconds executionTime");
                    // todo :: kick some lazy clients so they stop hogging the server. ( or something depending on why it took so long
                }
            }
            catch (AggregateException ae)
            {
                foreach (var e in ae.InnerExceptions)
                    Console.WriteLine("{0}: {1}", e.GetType().Name, e.Message);
            }
            Task.Run(TickPaintGame);
        }

        public void RequestTexture()
        {
            // todo :: send texture to client in chunks since it's large amount of data.
            // todo :: need to refactor server to keep track of which client sent the request, including if websocket or tcpclient.
        }

       
        public void BroadcastNewPixels()
        {
            var keys = newTextureColors.Keys.ToList(); // snapshot keys to prevent dataloss
            var pixels = new Pixels();
            foreach (var position in keys)
            {
                if (newTextureColors.TryRemove(position, out Color color)) // only remove snapshotted keys
                {
                    pixels.pixels.Add(new Pixel(position, color));
                }
            }

            const int maxPixelPerMessage = 100;
            NetworkMessage networkMessage;
            while (maxPixelPerMessage < pixels.pixels.Count)
            {
                Pixels pixelsToSend = new Pixels { pixels = pixels.pixels.Take(100).ToList() };
                networkMessage = new NetworkMessage() { serviceName = "game", operationName = "pixelUpdate", datamembers = new List<string> { pixelsToSend.AsJsonString } };
                server.BroadcastNetworkMessage(networkMessage);
            }
            networkMessage = new NetworkMessage() { serviceName = "game", operationName = "pixelUpdate", datamembers = new List<string> { pixels.AsJsonString } };
            server.BroadcastNetworkMessage(networkMessage);
        }

        public ConcurrentDictionary<Position, Color> newTextureColors = new ConcurrentDictionary<Position, Color>();
   
        public void ProcessNetworkMessage(NetworkMessage networkMessage)
        {
            try
            {
                if (networkMessage.operationName == "getPainting")
                {
                    // todo :: user id to sendback to correct user. include user endpoint as param?
                }
                else if (networkMessage.operationName == "newPixels")
                {
                    var pixels = JsonSerializer.Deserialize<Pixels>(networkMessage.datamembers[0]);
                    foreach(var pixel in pixels.pixels)
                    {
                        texture[pixel.x, pixel.y] = pixel.color;
                        newTextureColors[new Position(pixel.x, pixel.y)] = pixel.color;
                    }
                }
                else if (networkMessage.operationName == "newPixel")
                {
                    var pixel = JsonSerializer.Deserialize<Pixel>(networkMessage.datamembers[0]);
                    texture[pixel.x, pixel.y] = pixel.color;
                    newTextureColors[new Position(pixel.x, pixel.y)] = pixel.color;
                }
            }
            catch (JsonException e)
            {
                // todo :: dispose of client? What to do with incorrect json... Many environments will likely cause issue.
                Console.WriteLine(e.Message + e.InnerException?.Message);
            }
        }

        // todo :: we should probably have 2 boards, one for old representing what users have and one for new.
        // that way we can bucket find duplicate pixelchanges and discard old values that client don't need

        public class Pixels
        {
            public List<Pixel> pixels { get; set; } = new List<Pixel>();

            [JsonIgnore]
            public string AsJsonString => JsonSerializer.Serialize(this);
            [JsonIgnore]
            public byte[] AsJsonBytes => JsonSerializer.SerializeToUtf8Bytes(this);
        }

        public class Texture
        {
            public Color[,] pixels;
            public int width;
            public int height;
            public Color this[int x, int y]
            {
                get
                {
                    return pixels[x, y];
                }
                set
                {
                    pixels[x, y] = value;
                }
            }

            public Color this[Position pos]
            {
                get
                {
                    return pixels[pos.x, pos.y];
                }
                set
                {
                    pixels[pos.x, pos.y] = value;
                }
            }


            private Texture() {;}
            public Texture(int width, int height)
            {
                this.width = width;
                this.height = height;
                pixels = new Color[width, height];
            }
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
    }
}
