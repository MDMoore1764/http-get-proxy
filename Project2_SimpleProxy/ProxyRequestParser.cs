using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Project2_SimpleProxy
{
    internal class ProxyRequestParser
    {
        private static Regex methodAndURLGetter = new Regex(@"^(?<method>[^\s]+)\shttp:\/\/(?<url>[^:\s]+):?(?<port>\d+)?", RegexOptions.Compiled);
        private string requestString;
        public ProxyRequestParser(List<byte> allRequestBytes)
        {
            requestString = Encoding.UTF8.GetString(allRequestBytes.ToArray());

            var match = methodAndURLGetter.Match(requestString) ?? throw new ArgumentException("Invalid request string.");

            this.Method = match.Groups["method"].Value;

            if(int.TryParse(match.Groups["port"].Value, out int port)){
                this.Port = port;
            }

            var localurl = new Uri(match.Groups["url"].Value);
            this.Uri = localurl.DnsSafeHost;

            this.Request = requestString.Replace("\r\n\r\n", "\nConnection: close\r\n\r\n");
        }

        public string Method { get; private set; }
        public string Uri { get; private set; }
        public string Request { get; private set; }
        public int? Port { get; private set; }
    }
}
