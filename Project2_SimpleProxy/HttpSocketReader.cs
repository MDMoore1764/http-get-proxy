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

        private const string END_OF_HEADERS_SEQUENCE = "\r\n\r\n";

        private static int HTTPGetEndOfHeaderIndex(List<byte> buffer)
        {
            return Encoding.UTF8.GetString(buffer.ToArray()).IndexOf(END_OF_HEADERS_SEQUENCE);
        }

        private static Regex ContentLengthRegex = new Regex(@"^Content-Length:\s(\d+)$", RegexOptions.Compiled);

        /// <summary>
        /// Reads the content length from an HTTP header.
        /// </summary>
        /// <param name="header"></param>
        /// <returns>The content length, or 0 it is not found.</returns>
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

        /// <summary>
        /// Tries to get the content length from the provided buffer.
        /// </summary>
        /// <param name="buffer">The buffer of bytes from the server.</param>
        /// <param name="headerIndex">The index of the end of the header</param>
        /// <param name="contentLength">The length of the content, if it has been found.</param>
        /// <returns>A boolean if the content length has been found.</returns>
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

        /// <summary>
        /// Determines if the end of the header has been reached.
        /// </summary>
        /// <param name="buffer">Teh buffer of bytes read from the server.</param>
        /// <returns>True if the end of the header has been read.</returns>
        private static bool IsEndOfHeader(List<byte> buffer)
        {
            return TryGetContentLength(buffer, out var headerIndex, out var contentLength)
                && (contentLength == 0 ||
                    (buffer.Count - (headerIndex + END_OF_HEADERS_SEQUENCE.Length)) == contentLength);
        }

        /// <summary>
        /// Read all of the bytes in the header of an HTTP request.
        /// </summary>
        /// <returns></returns>
        public async Task<List<byte>> ReadHeaderBytesAsync()
        {
            List<byte> allClientBytesReceived = new List<byte>();

            byte[] clientBuffer = new byte[socket.ReceiveBufferSize];

            int clientReceived;
            while ((clientReceived = await socket.ReceiveAsync(clientBuffer, SocketFlags.None)) > 0)
            {
                allClientBytesReceived.AddRange(clientBuffer.AsSpan()[..clientReceived]);

                if (IsEndOfHeader(allClientBytesReceived))
                {
                    break;
                }
            }

            return allClientBytesReceived;
        }

        /// <summary>
        /// Read all bytes from the provided socket until the socket is closed by the target.
        /// </summary>
        /// <returns>A list of bytes read.</returns>
        public async Task<List<byte>> ReadAllBytesAsync()
        {
            List<byte> allClientBytesReceived = new List<byte>();

            byte[] clientBuffer = new byte[socket.ReceiveBufferSize];

            int clientReceived;
            while ((clientReceived = await socket.ReceiveAsync(clientBuffer, SocketFlags.None)) > 0)
            {
                allClientBytesReceived.AddRange(clientBuffer.AsSpan()[..clientReceived]);
            }

            return allClientBytesReceived;
        }

        /// <summary>
        /// Reads all bytes from the provided socket until the socket is closed by the target.
        /// </summary>
        /// <returns>A list of bytes as a UTF8-encoded string.</returns>
        public async Task<string> ReadAsStringAsync()
        {
            var bytes = await ReadAllBytesAsync();
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
    }
}
