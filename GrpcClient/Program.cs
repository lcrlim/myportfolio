using Grpc.Net.Client;
using GrpcService;

// Create a channel to the server
using var channel = GrpcChannel.ForAddress("http://localhost:5000");

// Create a client
var client = new Greeter.GreeterClient(channel);

// Call the service
var reply = await client.SayHelloAsync(new HelloRequest { Name = "World" });
Console.WriteLine("Greeting: " + reply.Message);

Console.WriteLine("Press any key to exit...");
Console.ReadKey();