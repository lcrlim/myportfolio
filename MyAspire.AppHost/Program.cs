var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

//var apiService = builder.AddProject<Projects.MyAspire_ApiService>("apiservice");

//builder.AddProject<Projects.MyAspire_Web>("webfrontend")
//    .WithExternalHttpEndpoints()
//    .WithReference(cache)
//    .WithReference(apiService);

var sql = builder.AddSqlServer("sql");

builder.AddProject<Projects.MyAspire_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WithReference(sql);

builder.Build().Run();
