using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Project2_SimpleProxy
{
    internal class ProxyServer
    {
        public ProxyServer()
        {

        }


        public async Task RunServerAsync(int fixedPort = 0)
        {
            using Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(new IPEndPoint(IPAddress.Any, fixedPort));

            socket.Listen();

            Console.WriteLine($"admin: started server on '{Environment.MachineName}' at '{((IPEndPoint)socket.LocalEndPoint!).Port}'");

            while (true)
            {
                using var clientSocket = await socket.AcceptAsync();
                await HandleClientAsync(clientSocket);
            }
        }

        private async Task HandleClientAsync(Socket clientSocket)
        {
            Console.WriteLine($"admin: accepted connection from '{((IPEndPoint)clientSocket.RemoteEndPoint!).Address}' at '{((IPEndPoint)clientSocket.RemoteEndPoint).Port}'");

            var clientSocketReader = new HttpSocketReader(clientSocket);
            List<byte> allClientBytesReceived = await clientSocketReader.ReadAllBytesAsync();

            var httpRequest = Encoding.UTF8.GetString(allClientBytesReceived.ToArray());
            var requestParser = new ProxyRequestParser(allClientBytesReceived);

            try
            {
                var serverResponseToHost = await GetTargetResponseAsync(allClientBytesReceived, requestParser.Uri);

                Console.WriteLine("Writing response to client...");

                await clientSocket.SendAsync(serverResponseToHost.ToArray());

                Console.WriteLine("Complete!\n");
            }
            catch (Exception e)
            {
                var response = Encoding.UTF8.GetBytes($"HTTP/1.1 500 INTERNAL SERVER ERROR\r\n\r\nProxy: Uh oh! Something went wrong:\n{e.Message}\r\n\r\n");
                await clientSocket.SendAsync(response);

                Console.WriteLine("Something went wrong!");
                Console.WriteLine(e.ToString());
            }


        }

        private async Task<List<byte>> GetTargetResponseAsync(List<byte> clientRequestBytes, string targetURL, int targetPort = 80)
        {
            var targetHostEntry = await Dns.GetHostEntryAsync(targetURL);
            var targetAddress = targetHostEntry.AddressList[0];
            var targetIPEndpoint = new IPEndPoint(targetAddress, targetPort);

            using var httpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            await httpSocket.ConnectAsync(targetIPEndpoint);

            Console.WriteLine($"Connected to '{targetIPEndpoint.Address}' at '{targetIPEndpoint.Port}'");

            await httpSocket.SendAsync(clientRequestBytes.ToArray());

            var httpSocketReader = new HttpSocketReader(httpSocket);

            return await httpSocketReader.ReadAllBytesAsync();
        }
    }
}
