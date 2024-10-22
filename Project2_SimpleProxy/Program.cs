namespace Project2_SimpleProxy
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            //Main is the entrance point of the program, where I create the ProxyServer class I created, and begin running the server.
            var proxyServer = new ProxyServer();
            await proxyServer.RunServerAsync();
        }
    }
}