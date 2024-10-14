using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using MoreLinq;

namespace Project2_SimpleProxy
{
    internal class Program
    {



        static async Task Main(string[] args)
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(new IPEndPoint(IPAddress.Any, 8080));

            
            socket.Listen(100);
            Console.WriteLine($"admin: started server on '{Environment.MachineName}' at '{((IPEndPoint)socket.LocalEndPoint).Port}'");


            while (true)
            {
                using var clientSocket = await socket.AcceptAsync();


                Console.WriteLine($"admin: accepted connection from '{((IPEndPoint)clientSocket.RemoteEndPoint).Address}' at '{((IPEndPoint)clientSocket.RemoteEndPoint).Port}'");


                var clientSocketReader = new HttpSocketReader(clientSocket);


                List<byte> allClientBytesReceived = await clientSocketReader.ReadAllBytesAsync();

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

                var httpSocketReader = new HttpSocketReader(httpSocket);


                List<byte> allHttpSocketBytesReceived = await httpSocketReader.ReadAllBytesAsync();
               

                var clientResponse = Encoding.UTF8.GetString(allHttpSocketBytesReceived.ToArray());

                Console.WriteLine("Writing response to client...");
                await clientSocket.SendAsync(allHttpSocketBytesReceived.ToArray());

                Console.WriteLine("Complete! Awaiting next connection request.");

            }
        }
    }
}
