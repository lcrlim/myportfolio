/* 
 * .NET Core 기반의 코드를 Aspire 기능을 사용해 Container 기반으로 실행하기 위한 호스트 프로젝트
 */



using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");

var sql = builder.AddSqlServer("sql")
    .WithDataVolume()
    .AddDatabase("MyOpenId", "MyOpenId");

var webService = builder.AddContainer("web", "web-service")
    //.WithExternalHttpEndpoints()
    .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http")
    //.WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development") // 또는 Production
    .WithEnvironment("ASPNETCORE_URLS", "http://0.0.0.0:8080")
    .WithEnvironment("ENABLE_SWAGGER", "true") // 커스텀 환경 변수
    .WithReference(cache)
    .WithReference(sql)
    .WaitFor(sql);

//var webService = builder.AddDockerfile("web", "../Web.Service")
//    .WithExternalHttpEndpoints()
//    .WithReference(cache)
//    .WithReference(sql)
//    .WaitFor(sql);

//builder.AddProject<Projects.Web_Service>("web")
//    .WithExternalHttpEndpoints()
//    .WithReference(cache)
//    .WithReference(sql)
//    .WaitFor(sql);

builder.Build().Run();
