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
        static async Task Main(string[] args)
        {
            var proxyServer = new ProxyServer();
            await proxyServer.RunServerAsync();
        }
    }
}
