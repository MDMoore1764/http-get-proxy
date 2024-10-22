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


        /// <summary>
        /// The service that continuously accepts new connections and handles them.
        /// </summary>
        /// <param name="fixedPort">An optional fixed port to run the TCP server on.If this is left to default or set to 0, the operating system will choose the next available TCP port for you.</param>
        /// <returns>An awaitable task that will never complete.</returns>
        public async Task RunServerAsync(int fixedPort = 0)
        {
            //Here, I create a new TCP socket to listen for incoming connections.
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            //I bind that socket to the local machine's IP address and the port provided, or let the operating system decide if 0 is provided.
            socket.Bind(new IPEndPoint(IPAddress.Any, fixedPort));

            //I then begin listening for incoming connections.
            socket.Listen();

            Console.WriteLine($"admin: started server on '{Environment.MachineName}' at '{((IPEndPoint)socket.LocalEndPoint!).Port}'");

            //I then enter an infinite loop, accepting new connections and handling them each in a new lightweight thread.
            while (true)
            {
                var clientSocket = await socket.AcceptAsync();
                _ = Task.Run(() => HandleClientAsync(clientSocket));
            }
        }

        /// <summary>
        /// Handles a client socket connection.
        /// This function reads the client's request, parses it, and then forwards the request to the target server.
        /// It then reads the target server's response and forwards it back to the client.
        /// </summary>
        /// <param name="clientSocket">The connected client socket to receive data from and forward responses to.</param>
        /// <returns>An awaitable task.</returns>
        private async Task HandleClientAsync(Socket clientSocket)
        {
            Console.WriteLine($"admin: accepted connection from '{((IPEndPoint)clientSocket.RemoteEndPoint!).Address}' at '{((IPEndPoint)clientSocket.RemoteEndPoint).Port}'");

            //I instantiate my HttpSocketReader class, which is a helper class to read HTTP headers from a socket.
            var clientSocketReader = new HttpSocketReader(clientSocket);
            List<byte> allClientBytesReceived = await clientSocketReader.ReadHeaderBytesAsync();

            //I then parse the request using my ProxyRequestParser class, extracing the target URL and port number.
            //Importantly, I also add a Connection: close header to the request, so that the target server closes the connection after sending the response.
            var requestParser = new ProxyRequestParser(allClientBytesReceived);

            try
            {
                //Here, I get the target server's response to the client's request.
                //If a custom port is provided, I use that, otherwise I use the default implemented by the method.
                byte[] serverResponse;

                if (requestParser.Port.HasValue)
                {
                    serverResponse = (await GetTargetResponseAsync(requestParser.Request, requestParser.Uri, requestParser.Port.Value)).ToArray();
                }
                else
                {
                    serverResponse = (await GetTargetResponseAsync(requestParser.Request, requestParser.Uri)).ToArray();
                }

                //Finally, I write the server's response to the client.
                Console.WriteLine("Writing response to client...");

                var response = Encoding.UTF8.GetString(serverResponse.ToArray());
                await clientSocket.SendAsync(serverResponse.ToArray());

                Console.WriteLine("Complete!\n");
            }
            catch (Exception e)
            {
                //In the event of an error, I reply to the client with a 500 Internal Server Error and the error message encountered.
                var response = Encoding.UTF8.GetBytes($"HTTP/1.1 500 INTERNAL SERVER ERROR\r\n\r\nProxy: Uh oh! Something went wrong:\n{e.Message}\r\n\r\n");
                await clientSocket.SendAsync(response);

                Console.WriteLine("Something went wrong!");
                Console.WriteLine(e.ToString());
            }
            finally {
                clientSocket.Dispose();
            }
        }

        /// <summary>
        /// Get the target server's response to a client request as a list of bytes.
        /// </summary>
        /// <param name="request">The client's request.</param>
        /// <param name="targetURL">The target URL.</param>
        /// <param name="targetPort">The target port. The default is the standard HTTP port: 80.</param>
        /// <returns>The target server's response as a list of bytes.</returns>
        private async Task<List<byte>> GetTargetResponseAsync(string request, string targetURL, int targetPort = 80)
        {
            //Here, I get the target server's IP addresses and select the first one.
            var targetHostEntry = await Dns.GetHostEntryAsync(targetURL);
            var targetAddress = targetHostEntry.AddressList[0];
            var targetIPEndpoint = new IPEndPoint(targetAddress, targetPort);

            //I create a new TCP socket to connect to the target server, and then connect to it.
            using var httpSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            await httpSocket.ConnectAsync(targetIPEndpoint);

            Console.WriteLine($"Connected to '{targetIPEndpoint.Address}' at '{targetIPEndpoint.Port}'");

            //I then send the client's request to the target server, read the response, and return it.
            await httpSocket.SendAsync(Encoding.UTF8.GetBytes(request));

            var httpSocketReader = new HttpSocketReader(httpSocket);
            return await httpSocketReader.ReadAllBytesAsync();
        }
    }
}
