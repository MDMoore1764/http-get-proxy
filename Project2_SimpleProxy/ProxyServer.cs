using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace Project2_SimpleProxy
{
    internal class ProxyServer
    {
        public ProxyServer()
        {

        }


        public async Task RunServerAsync(int fixedPort = 0)
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(new IPEndPoint(IPAddress.Any, fixedPort));

            socket.Listen();

            Console.WriteLine($"admin: started server on '{Environment.MachineName}' at '{((IPEndPoint)socket.LocalEndPoint!).Port}'");

            while (true)
            {
                var clientSocket = await socket.AcceptAsync();
                _ = Task.Run(() => HandleClientAsync(clientSocket));
            }
        }

        private async Task HandleClientAsync(Socket clientSocket)
        {
            Console.WriteLine($"admin: accepted connection from '{((IPEndPoint)clientSocket.RemoteEndPoint!).Address}' at '{((IPEndPoint)clientSocket.RemoteEndPoint).Port}'");

            var clientSocketReader = new HttpSocketReader(clientSocket);
            List<byte> allClientBytesReceived = await clientSocketReader.ReadHeaderBytesAsync();

            var requestParser = new ProxyRequestParser(allClientBytesReceived);

            try
            {
                byte[] serverResponse;

                if (requestParser.Port.HasValue)
                {
                    serverResponse = (await GetTargetResponseAsync(requestParser.Request, requestParser.Uri, requestParser.Port.Value)).ToArray();
                }
                else
                {
                    serverResponse = (await GetTargetResponseAsync(requestParser.Request, requestParser.Uri)).ToArray();
                }

                Console.WriteLine("Writing response to client...");

                var response = Encoding.UTF8.GetString(serverResponse.ToArray());

                await clientSocket.SendAsync(serverResponse.ToArray());

                Console.WriteLine("Complete!\n");
            }
            catch (Exception e)
            {
                var response = Encoding.UTF8.GetBytes($"HTTP/1.1 500 INTERNAL SERVER ERROR\r\n\r\nProxy: Uh oh! Something went wrong:\n{e.Message}\r\n\r\n");
                await clientSocket.SendAsync(response);

                Console.WriteLine("Something went wrong!");
                Console.WriteLine(e.ToString());
            }
            finally {
                clientSocket.Dispose();
            }
        }

        private async Task<List<byte>> GetTargetResponseAsync(string request, string targetURL, int targetPort = 80)
        {
            var targetHostEntry = await Dns.GetHostEntryAsync(targetURL);
            var targetAddress = targetHostEntry.AddressList[0];
            var targetIPEndpoint = new IPEndPoint(targetAddress, targetPort);

            using var httpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            await httpSocket.ConnectAsync(targetIPEndpoint);

            Console.WriteLine($"Connected to '{targetIPEndpoint.Address}' at '{targetIPEndpoint.Port}'");

            await httpSocket.SendAsync(Encoding.UTF8.GetBytes(request));

            var httpSocketReader = new HttpSocketReader(httpSocket);

            return await httpSocketReader.ReadAllBytesAsync();
        }
    }
}
