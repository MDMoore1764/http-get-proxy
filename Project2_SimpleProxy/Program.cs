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