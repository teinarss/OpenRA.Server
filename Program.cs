using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace OpenRa.Server
{
    class Program
    {
        private const string data = @"Game:
	Protocol: 2
	Name: test11111
	Address: 0.0.0.0:1234
	Mod: cnc
	Version: {DEV_VERSION}
	ModTitle: Tiberian Dawn
	ModWebsite: https://www.openra.net
	ModIcon32: https://www.openra.net/images/icons/cnc_32x32.png
	Map: c6dcbc855b6a70c5ee6509d614c2d8e0bb5fd970
	State: 1
	MaxPlayers: 2
	Protected: False
	Authentication: False
	Clients:";

        static async Task Main(string[] args)
        {
            var httpClientFactory = new HttpClientFactory();
            var client = httpClientFactory.GetForHost(new Uri("https://master.openra.net/ping"));

            _ = Task.Run(async () =>
             {
                 while (true)
                 {
                     await client.PostAsync("https://master.openra.net/ping", new StringContent(data));
                     await Task.Delay(3000);
                 }
             });

             
            var server = new Server();
            server.Start().Wait();

            Console.WriteLine("Hello World!");
        }
    }

    public class Server
    {
        private TcpListener listener;
        private ConcurrentBag<TcpClient> _Clients;
        public readonly MersenneTwister Random = new MersenneTwister();

        public Server()
        {
            listener = new TcpListener(new IPEndPoint(IPAddress.Any, 1234));
        }

        public Task Start()
        {
            return Task.Run(async () =>
            {
                listener.Start();

                while (true)
                {
                    var tcpClient = await listener.AcceptTcpClientAsync();
                    var clientIp = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();

                    Console.WriteLine($"{clientIp} has connected");

                    var networkStream = tcpClient.GetStream();

                    var ms = new MemoryStream(8);
                    ms.Write(BitConverter.GetBytes(7));
                    ms.Write(BitConverter.GetBytes(0));

                    var handshake = ms.ToArray();

                    await networkStream.WriteAsync(handshake, 0, handshake.Length);

                    // Validate player identity by asking them to sign a random blob of data
                    // which we can then verify against the player public key database
                    var token = Convert.ToBase64String(MakeArray(256, _ => (byte)Random.Next()));

                    var handshakeRequest = new HandshakeRequest
                    {
                        Mod = "cnc",
                        Version = "{DEV_VERSION}",
                        AuthToken = "test"
                    };

                    var testStream = new MemoryStream();
                    var testWriter = new BinaryWriter(testStream);
                    testWriter.Write("eueo");
                    var data = new byte[]
                    {
                        254, 16, 72, 97, 110, 100, 115, 104, 97, 107, 101, 82, 101, 113, 117, 101, 115, 116, 146, 3, 72,
                        97, 110, 100, 115, 104, 97, 107, 101, 58, 10, 9, 77, 111, 100, 58, 32, 99, 110, 99, 10, 9, 86,
                        101, 114, 115, 105, 111, 110, 58, 32, 123, 68, 69, 86, 95, 86, 69, 82, 83, 73, 79, 78, 125, 10,
                        9, 65, 117, 116, 104, 84, 111, 107, 101, 110, 58, 32, 53, 101, 101, 119, 57, 78, 49, 69, 85, 52,
                        82, 71, 117, 78, 73, 68, 77, 121, 81, 56, 84, 57, 73, 65, 88, 102, 82, 72, 71, 112, 55, 101,
                        114, 102, 118, 68, 122, 73, 81, 68, 110, 55, 55, 75, 97, 122, 111, 105, 103, 114, 80, 50, 107,
                        70, 112, 49, 111, 66, 108, 68, 52, 73, 97, 53, 72, 56, 76, 110, 66, 74, 90, 80, 79, 101, 87,
                        122, 80, 108, 80, 49, 67, 118, 115, 51, 122, 122, 49, 78, 48, 70, 102, 43, 80, 122, 119, 80,
                        112, 103, 78, 102, 67, 108, 113, 56, 112, 116, 73, 113, 82, 98, 88, 80, 106, 115, 83, 90, 76,
                        66, 49, 119, 49, 66, 54, 53, 119, 77, 48, 55, 48, 84, 70, 49, 51, 76, 111, 109, 69, 115, 105,
                        88, 111, 118, 77, 52, 56, 90, 120, 98, 57, 76, 68, 52, 121, 53, 52, 81, 70, 47, 56, 108, 120,
                        49, 107, 118, 72, 115, 81, 111, 80, 69, 120, 98, 104, 100, 48, 51, 102, 115, 65, 50, 120, 119,
                        74, 120, 76, 47, 81, 100, 86, 80, 84, 79, 100, 97, 55, 104, 110, 48, 75, 104, 70, 100, 105, 53,
                        98, 120, 100, 83, 101, 106, 72, 48, 88, 65, 65, 43, 109, 122, 103, 49, 75, 120, 110, 98, 67,
                        109, 116, 79, 111, 120, 70, 69, 71, 51, 69, 85, 80, 111, 56, 115, 76, 108, 50, 66, 99, 99, 81,
                        47, 98, 71, 50, 69, 110, 82, 73, 110, 52, 88, 88, 66, 72, 53, 107, 67, 69, 117, 113, 122, 111,
                        114, 43, 79, 84, 65, 108, 118, 118, 113, 76, 47, 97, 122, 55, 117, 101, 78, 78, 70, 80, 52, 75,
                        97, 68, 53, 52, 84, 68, 50, 65, 100, 84, 66, 50, 50, 70, 49, 110, 68, 81, 98, 102, 43, 79, 47,
                        102, 70, 98, 106, 51, 122, 66, 53, 116, 53, 55, 76, 78, 87, 103, 74, 113, 118, 122, 56, 100, 99,
                        50, 79, 88, 85, 43, 119, 61, 61, 10
                    };

                    var targetString = handshakeRequest.Serialize();
                    var bytes = new ServerOrder("HandshakeRequest", targetString).Serialize();
                    await DispatchOrdersToClient(tcpClient, networkStream, 0, 0, bytes);
                    await Read(tcpClient, networkStream);
                }
            });
        }

        public static T[] MakeArray<T>(int count, Func<int, T> f)
        {
            var result = new T[count];
            for (var i = 0; i < count; i++)
                result[i] = f(i);

            return result;
        }

        async Task Read(TcpClient client, NetworkStream networkStream)
        {
            var buffer = new byte[1024];
            var bytesRead = await networkStream.ReadAsync(buffer);
        }

        async Task DispatchOrdersToClient(TcpClient c, NetworkStream networkStream, int client, int frame, byte[] data)
        {
            try
            {
                var ms = new MemoryStream(data.Length + 12);
                ms.Write(BitConverter.GetBytes(data.Length + 4));
                ms.Write(BitConverter.GetBytes(client));
                ms.Write(BitConverter.GetBytes(frame));

                ms.Write(data);

                await networkStream.WriteAsync(ms.ToArray());
            }
            catch (Exception e)
            {
                DropClient(c);
            }
        }

        private void DropClient(TcpClient tcpClient)
        {
            tcpClient.Close();
        }
    }

    public interface IHttpClientFactory
    {
        HttpClient GetForHost(Uri uri);
    }

    public class HandshakeRequest
    {
        public string Mod;
        public string Version;
        public string AuthToken;

        public string Serialize()
        {
            return $@"Handshake:
	Mod: {Mod}
	Version: {Version}
	AuthToken: {AuthToken}
";
        }
    }

    public class ServerOrder
    {
        private readonly string _order;
        private readonly string _targetString;

        public ServerOrder(string order, string targetString)
        {
            _order = order;
            _targetString = targetString;
        }

        public byte[] Serialize()
        {
            var minLength = 1 + _order.Length + 1;
            minLength += _targetString.Length + 1;
            var ms = new MemoryStream(minLength);
            var w = new BinaryWriter(ms);

            w.Write((byte)0xFE);
            w.Write(_order);
            w.Write(_targetString);

            return ms.ToArray();
        }
    }
}
