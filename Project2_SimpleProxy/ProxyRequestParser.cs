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
        private static Regex methodAndURLGetter = new Regex(@"^(?<method>[^\s]+)\s(?<url>[^\s]+)", RegexOptions.Compiled);
        private string requestString;
        public ProxyRequestParser(List<byte> allRequestBytes)
        {
            requestString = Encoding.UTF8.GetString(allRequestBytes.ToArray());

            var match = methodAndURLGetter.Match(requestString) ?? throw new ArgumentException("Invalid request string.");

            this.Method = match.Groups["method"].Value;

            var localurl = new Uri(match.Groups["url"].Value);
            this.Uri = localurl.DnsSafeHost;
        }

        public string Method { get; private set; }
        public string Uri { get; private set; }
    }
}
