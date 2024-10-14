using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Project2_SimpleProxy
{
    internal class HttpSocketReader
    {
        private readonly Socket socket;

        public HttpSocketReader(Socket socket)
        {
            this.socket = socket;
        }

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

            if (match == null)
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


        public async Task<List<byte>> ReadAllBytesAsync()
        {
            List<byte> allClientBytesReceived = new List<byte>();

            byte[] clientBuffer = new byte[socket.ReceiveBufferSize];

            int clientReceived = 0;
            while ((clientReceived = await socket.ReceiveAsync(clientBuffer, SocketFlags.None)) > 0)
            {
                allClientBytesReceived.AddRange(clientBuffer.AsSpan()[..clientReceived]);

                if (IsEndOfMessage(allClientBytesReceived))
                {
                    break;
                }
            }

            return allClientBytesReceived;
        }
    }
}
