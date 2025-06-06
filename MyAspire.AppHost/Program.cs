using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

//var apiService = builder.AddProject<Projects.MyAspire_ApiService>("apiservice");

//builder.AddProject<Projects.MyAspire_Web>("webfrontend")
//    .WithExternalHttpEndpoints()
//    .WithReference(cache)
//    .WithReference(apiService);

var sql = builder.AddSqlServer("sql")
    //.WithEnvironment("SA_PASSWORD", "YourStrong@Passw0rd123")
    .WithDataVolume()
    .AddDatabase("MyOpenId", "MyOpenId");

builder.AddProject<Projects.MyAspire_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WithReference(sql)
    .WaitFor(sql);

builder.Build().Run();
