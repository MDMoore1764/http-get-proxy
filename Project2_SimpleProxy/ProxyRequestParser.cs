using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Project2_SimpleProxy
{
    internal class ProxyRequestParser
    {
        private string requestString;
        public ProxyRequestParser(List<byte> allRequestBytes)
        {
            requestString = Encoding.UTF8.GetString(allRequestBytes.ToArray());

            //Parse out the incoming request.
            var lines = requestString.Split('\n');
            var methodAndURLLine = lines[0];
            var methodAndURL = methodAndURLLine.Split(' ');
            this.Uri = methodAndURL[1];

            this.Method = methodAndURL[0];
        }

        public string Method { get; private set; }
        public string Uri { get; private set; }
    }
}
