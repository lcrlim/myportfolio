using Grpc.Core;
using GrpcService;

namespace GrpcService.Services
{
    public class GreeterService : Greeter.GreeterBase
    {
        private readonly ILogger<GreeterService> _logger;
        public GreeterService(ILogger<GreeterService> logger)
        {
            _logger = logger;
        }

        public override async Task<HelloReply> SayHello(HelloRequest request, ServerCallContext context)
        {
            try
            {
                _logger.LogInformation("Processing SayHello for {Name}", request.Name);

                // Simulate some async work (e.g., database call)
                await Task.Delay(10); // Replace with actual async work

                // Check for cancellation
                context.CancellationToken.ThrowIfCancellationRequested();

                return new HelloReply
                {
                    Message = "Hello " + request.Name
                };
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Request for {Name} was cancelled", request.Name);
                throw new RpcException(new Status(StatusCode.Cancelled, "Request was cancelled"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing SayHello for {Name}", request.Name);
                throw new RpcException(new Status(StatusCode.Internal, "An error occurred while processing the request"));
            }
        }
    }
}
