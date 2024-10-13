using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Project2_SimpleProxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //Get and assign port
            socket.Bind(new IPEndPoint(IPAddress.Any, 8080));

            
            socket.Listen(100);
            Console.WriteLine($"admin: started server on '{Environment.MachineName}' at '{((IPEndPoint)socket.LocalEndPoint).Port}'");


            //Accept incoming connections, read their data, do all the things.

            while (true)
            {
                using var clientSocket = await socket.AcceptAsync();

                Console.WriteLine($"admin: accepted connection from '{((IPEndPoint)clientSocket.RemoteEndPoint).Address}' at '{((IPEndPoint)clientSocket.RemoteEndPoint).Port}'");

                List<byte> allClientBytesReceived = new List<byte>();

                byte[] clientBuffer = new byte[socket.ReceiveBufferSize];

                int clientReceived = 0;
                while(clientSocket.Available > 0 && (clientReceived = await clientSocket.ReceiveAsync(clientBuffer, SocketFlags.None)) > 0)
                {
                    allClientBytesReceived.AddRange(clientBuffer.AsSpan()[..clientReceived]);
                }

                var httpRequest = Encoding.UTF8.GetString(allClientBytesReceived.ToArray());

                //Parse out the incoming request.
                var lines = httpRequest.Split('\n');
                var methodAndURLLine = lines[0];
                var methodAndURL = methodAndURLLine.Split(' ');
                var method = methodAndURL[0];
                var url = new Uri(methodAndURL[1]);

                var safeHost = url.DnsSafeHost;

                var targetHostEntry = await Dns.GetHostEntryAsync(safeHost);
                var targetAddress = targetHostEntry.AddressList[0];
                var targetIPEndpoint = new IPEndPoint(targetAddress, 80);

                //Create a new socket and send a request to the server.
                using var httpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

                await httpSocket.ConnectAsync(targetIPEndpoint);
                Console.WriteLine($"Connected to '{targetIPEndpoint.Address.ToString()}' at '{targetIPEndpoint.Port}'");

                await httpSocket.SendAsync(allClientBytesReceived.ToArray());

                List<byte> allHttpSocketBytesReceived = new List<byte>();
                byte[] httpBuffer = new byte[httpSocket.ReceiveBufferSize];
                int httpSocketReceived = 0;

                while((httpSocketReceived = await httpSocket.ReceiveAsync(httpBuffer, SocketFlags.None)) > 0)
                {
                    allHttpSocketBytesReceived.AddRange(httpBuffer.AsSpan()[..httpSocketReceived]);
                }

                var clientResponse = Encoding.UTF8.GetString(allHttpSocketBytesReceived.ToArray());

                Console.WriteLine(clientResponse);
                await clientSocket.SendAsync(allHttpSocketBytesReceived.ToArray());

                //Send reply here
                //await clientSocket.SendAsync(allData.ToArray());


                //Use a regex to get every single part of this regex.

            }
        }
    }
}
