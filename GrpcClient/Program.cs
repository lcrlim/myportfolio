using Grpc.Net.Client;
using GrpcService;
using System.Threading.Channels;

// Create a channel to the server
//using var channel = GrpcChannel.ForAddress("http://localhost:5000");
var channelOptions = new GrpcChannelOptions
{
    MaxReceiveMessageSize = null,
    MaxSendMessageSize = null,
    HttpHandler = new SocketsHttpHandler
    {
        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
        EnableMultipleHttp2Connections = true,
        SslOptions = new System.Net.Security.SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (sender, cert, chain, errors) => true // 테스트용
        }
    }
};

using var channel = GrpcChannel.ForAddress("https://localhost:5000", channelOptions);

// Create a client
var client = new Greeter.GreeterClient(channel);

// Call the service
var reply = await client.SayHelloAsync(new HelloRequest { Name = "World" });
Console.WriteLine("Greeting: " + reply.Message);

Console.WriteLine("Press any key to exit...");
Console.ReadKey();