### Simple HTTP Proxy Server

This is a simple HTTP proxy server that can be used to forward HTTP requests to a specified server and forward the response to the requesting client. The server is implemented in C# using low-level socket interfaces.

### Compilation and Execution

This server is written with .net 8.0 and requires the .net 8.0 SDK to compile. To compile the server, navigate to the root directory of the project and run the following command:

```
dotnet publish
```

This command will publish the server to the "./Project2_SimpleProxy/bin/Release/net8.0/publish" directory, where the compiled output will be located, named "Project2_SimpleProxy".

To start running the server, execute this file. The server will report the port on which it is listening, which may be used as the proxy port for clients to connect to.
