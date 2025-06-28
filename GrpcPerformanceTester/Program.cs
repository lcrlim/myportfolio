using Grpc.Net.Client;
using GrpcService;
using System.Diagnostics;

namespace GrpcClient
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Configure gRPC channel with increased connection limits
            //var channelOptions = new GrpcChannelOptions
            //{
            //    MaxReceiveMessageSize = null, // No limit on message size
            //    MaxSendMessageSize = null,
            //    HttpHandler = new SocketsHttpHandler
            //    {
            //        PooledConnectionIdleTimeout = Timeout.InfiniteTimeSpan,
            //        KeepAlivePingDelay = TimeSpan.FromSeconds(60),
            //        KeepAlivePingTimeout = TimeSpan.FromSeconds(30),
            //        EnableMultipleHttp2Connections = true
            //    }
            //};

            //using var channel = GrpcChannel.ForAddress("http://localhost:5000", channelOptions);

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

            var client = new Greeter.GreeterClient(channel);

            // Run performance test with 100,000 requests, 1,000 per batch
            await RunConcurrentPerformanceTest(client, 100_000, 1_000);

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        static async Task RunConcurrentPerformanceTest(Greeter.GreeterClient client, int totalRequests, int batchSize)
        {
            var stopwatch = new Stopwatch();
            var responseTimes = new List<double>(totalRequests);
            int completedRequests = 0;

            Console.WriteLine($"Starting performance test with {totalRequests} concurrent requests (batch size: {batchSize})...");

            // Warm-up request to avoid initial overhead
            await client.SayHelloAsync(new HelloRequest { Name = "Warmup" });

            stopwatch.Start();
            for (int i = 0; i < totalRequests; i += batchSize)
            {
                int currentBatchSize = Math.Min(batchSize, totalRequests - i);
                var tasks = new List<Task<HelloReply>>(currentBatchSize);
                var requestStopwatches = new List<Stopwatch>(currentBatchSize);

                // Create batch of requests
                for (int j = 0; j < currentBatchSize; j++)
                {
                    var sw = Stopwatch.StartNew();
                    requestStopwatches.Add(sw);
                    tasks.Add(client.SayHelloAsync(new HelloRequest { Name = $"Test-{i + j}" }).ResponseAsync);
                }

                // Execute batch concurrently
                var responses = await Task.WhenAll(tasks);

                // Record response times
                for (int j = 0; j < currentBatchSize; j++)
                {
                    requestStopwatches[j].Stop();
                    responseTimes.Add(requestStopwatches[j].ElapsedTicks);
                }

                completedRequests += currentBatchSize;
                Console.WriteLine($"Processed {completedRequests} requests...");
            }
            stopwatch.Stop();

            // Calculate metrics
            double ticksPerSecond = Stopwatch.Frequency;
            double totalTimeSeconds = stopwatch.ElapsedTicks / ticksPerSecond;
            double averageResponseTimeMs = responseTimes.Average(t => (t / ticksPerSecond) * 1000);
            double rps = totalRequests / totalTimeSeconds;

            // Output results
            Console.WriteLine("\nConcurrent Performance Test Results:");
            Console.WriteLine($"Total Requests: {totalRequests}");
            Console.WriteLine($"Total Time: {totalTimeSeconds:F2} seconds");
            Console.WriteLine($"Average Response Time: {averageResponseTimeMs:F3} ms");
            Console.WriteLine($"Requests Per Second (RPS): {rps:F2}");
            Console.WriteLine($"Min Response Time: {(responseTimes.Min() / ticksPerSecond * 1000):F3} ms");
            Console.WriteLine($"Max Response Time: {(responseTimes.Max() / ticksPerSecond * 1000):F3} ms");
        }
    }
}