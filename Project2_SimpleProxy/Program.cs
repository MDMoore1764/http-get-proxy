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
        private static string END_OF_HEADERS_SEQUENCE = "\r\n\r\n";

        private static int HTTPGetEndOfHeaderIndex(List<byte> buffer)
        {
            return Encoding.UTF8.GetString(buffer.ToArray()).IndexOf(END_OF_HEADERS_SEQUENCE);
        }

        private static Regex ContentLengthRegex = new Regex(@"^Content-Length:\s(\d+)$", RegexOptions.Compiled);

        private static int GetContentLength(string header)
        {
            if (!ContentLengthRegex.IsMatch(header))
            {
                return 0;
            }


            var match = ContentLengthRegex.Match(header);

            if(match == null)
            {
                return 0;
            }


            return int.Parse(match.Groups[1].Value);
        }

        private static bool TryGetContentLength(List<byte> buffer, out int headerIndex, out int contentLength)
        {
            headerIndex = HTTPGetEndOfHeaderIndex(buffer);

            if (headerIndex == -1)
            {
                contentLength = 0;
                return false;
            }

            var header = Encoding.UTF8.GetString(buffer.ToArray()[..headerIndex]);
            contentLength = GetContentLength(header);

            return true;
        }

        private static bool IsEndOfMessage(List<byte> buffer)
        {
            return TryGetContentLength(buffer, out var headerIndex, out var contentLength) 
                && (contentLength == 0 || (buffer.Count - (headerIndex + END_OF_HEADERS_SEQUENCE.Length)) == contentLength);
        }



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

                List<byte> allClientBytesReceived = new List<byte>();

                byte[] clientBuffer = new byte[socket.ReceiveBufferSize];

                int clientReceived = 0;
                while((clientReceived = await clientSocket.ReceiveAsync(clientBuffer, SocketFlags.None)) > 0)
                {
                    allClientBytesReceived.AddRange(clientBuffer.AsSpan()[..clientReceived]);

                    if (IsEndOfMessage(allClientBytesReceived))
                    {
                        break;
                    }
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


                    var message = Encoding.UTF8.GetString(allHttpSocketBytesReceived.ToArray());

                    if (IsEndOfMessage(allHttpSocketBytesReceived))
                    {
                        break;
                    }
                }

                var clientResponse = Encoding.UTF8.GetString(allHttpSocketBytesReceived.ToArray());

                Console.WriteLine("Writing response to client...");
                await clientSocket.SendAsync(allHttpSocketBytesReceived.ToArray());

                Console.WriteLine("Complete! Awaiting next conneciton request.");

            }
        }
    }
}
