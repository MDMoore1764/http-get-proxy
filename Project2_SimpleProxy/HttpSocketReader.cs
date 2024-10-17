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

        private static bool TryGetHeader(List<byte> buffer, out string header)
        {
            var indexOfBuffer = HTTPGetEndOfHeaderIndex(buffer);

            if(indexOfBuffer == -1)
            {
                header = string.Empty;
                return false;
            }

            var test = Encoding.UTF8.GetString(buffer.ToArray());
            header = Encoding.UTF8.GetString(buffer.ToArray()[..indexOfBuffer]);
            return true;
        }

        private static Regex ContentLengthRegex = new Regex(@"^Content-Length:\s*(\d+)$", RegexOptions.Multiline | RegexOptions.Compiled);
        private static Regex TransferEncodingRegex = new Regex(@"^Transfer-Encoding:\s*chunked$", RegexOptions.Multiline | RegexOptions.Compiled);

        private static int GetContentLength(string header)
        {
            var match = ContentLengthRegex.Match(header);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private static bool IsChunkedTransferEncoding(string header)
        {
            return TransferEncodingRegex.IsMatch(header);
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
                && (contentLength == 0 ||
                    (buffer.Count - (headerIndex + END_OF_HEADERS_SEQUENCE.Length)) == contentLength);
        }

        private static HttpMessageReadType GetMessageReadType(string header, out string extras)
        {
            extras = string.Empty;

            if (IsChunkedTransferEncoding(header))
            {
                return HttpMessageReadType.Chunked;
            }

            var contentLength = GetContentLength(header);
            if (contentLength > 0)
            {
                extras = contentLength.ToString();
                return HttpMessageReadType.ContentLength;
            }

            return HttpMessageReadType.None;
        }

        public async Task<string> ReadAsStringAsync()
        {
            var bytes = await ReadAllHttpRequestBytesAsync();
            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        public async Task<List<byte>> ReadAllHttpRequestBytesAsync()
        {
            List<byte> allClientBytesReceived = new List<byte>();

            byte[] clientBuffer = new byte[socket.ReceiveBufferSize];

            int clientReceived = 0;
            string header = string.Empty;
            HttpMessageReadType? messageReadType = null;
            while ((clientReceived = await socket.ReceiveAsync(clientBuffer, SocketFlags.None)) > 0)
            {
                allClientBytesReceived.AddRange(clientBuffer.AsSpan()[..clientReceived]);
                var wholeMessage = Encoding.UTF8.GetString(allClientBytesReceived.ToArray());
                if (string.IsNullOrEmpty(header) && TryGetHeader(allClientBytesReceived, out header))
                {
                    break;
                }
            }

            return allClientBytesReceived;
        }

        public async Task<List<byte>> ReadAllHttpResponseBytesAsync()
        {
            List<byte> allClientBytesReceived = new List<byte>();

            byte[] clientBuffer = new byte[socket.ReceiveBufferSize];

            int clientReceived = 0;
            string header = string.Empty;
            HttpMessageReadType? messageReadType = null;
            while ((clientReceived = await socket.ReceiveAsync(clientBuffer, SocketFlags.None)) > 0)
            {
                allClientBytesReceived.AddRange(clientBuffer.AsSpan()[..clientReceived]);
                var wholeMessage = Encoding.UTF8.GetString(allClientBytesReceived.ToArray());
                if (string.IsNullOrEmpty(header) && !TryGetHeader(allClientBytesReceived, out header))
                {
                    continue;
                }

                if (!messageReadType.HasValue)
                {
                    messageReadType = GetMessageReadType(header, out string extras);
                }

                if (messageReadType == HttpMessageReadType.ContentLength && IsEndOfMessage(allClientBytesReceived))
                {
                    break;
                }

                if (messageReadType == HttpMessageReadType.Chunked)
                {
                    if (await ReadChunkedDataAsync(allClientBytesReceived))
                    {
                        break;
                    }
                }
            }

            return allClientBytesReceived;
        }

        private async Task<bool> ReadChunkedDataAsync(List<byte> allClientBytesReceived)
        {
            while (true)
            {
                // Read the chunk size
                var chunkSizeLine = await ReadLineAsync(allClientBytesReceived);
                if (string.IsNullOrEmpty(chunkSizeLine))
                {
                    return false;
                }

                if (!int.TryParse(chunkSizeLine, System.Globalization.NumberStyles.HexNumber, null, out int chunkSize))
                {
                    return false;
                }

                if (chunkSize == 0)
                {
                    // End of chunks
                    await ReadLineAsync(allClientBytesReceived); // Read the trailing CRLF
                    return true;
                }

                // Read the chunk data
                var chunkData = new byte[chunkSize];
                int bytesRead = 0;
                while (bytesRead < chunkSize)
                {
                    int read = await socket.ReceiveAsync(chunkData.AsMemory(bytesRead, chunkSize - bytesRead), SocketFlags.None);
                    if (read == 0)
                    {
                        return false;
                    }
                    bytesRead += read;
                }
                allClientBytesReceived.AddRange(chunkData);

                // Read the trailing CRLF
                await ReadLineAsync(allClientBytesReceived);
            }
        }

        private async Task<string> ReadLineAsync(List<byte> allClientBytesReceived)
        {
            List<byte> lineBytes = new List<byte>();
            while (socket.Available > 0)
            {
                byte[] buffer = new byte[1];
                int bytesRead = await socket.ReceiveAsync(buffer, SocketFlags.None);
                if (bytesRead == 0)
                {
                    break;
                }
                allClientBytesReceived.Add(buffer[0]);
                if (buffer[0] == '\n' && lineBytes.Count > 0 && lineBytes[^1] == '\r')
                {
                    break;
                }
                lineBytes.Add(buffer[0]);
            }
            return Encoding.UTF8.GetString(lineBytes.ToArray()).TrimEnd('\r', '\n');
        }
    }
}

