using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var sql = builder.AddSqlServer("sql")
    //.WithEnvironment("SA_PASSWORD", "YourStrong@Passw0rd123") // SA 계정 패스워드 전달시 사용
    .WithDataVolume()
    .AddDatabase("MyOpenId", "MyOpenId");

builder.AddProject<Projects.Web_Service>("web")
    .WithExternalHttpEndpoints()
    .WithReference(cache)
    .WithReference(sql)
    .WaitFor(sql);

builder.Build().Run();
