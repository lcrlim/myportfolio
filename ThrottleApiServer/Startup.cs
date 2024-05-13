using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Owin.Builder;
using Newtonsoft.Json;
using Owin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using WebApiThrottle;

namespace ThrottleApiServer
{
    public class MyLogger : IThrottleLogger
    {
        public void Log(ThrottleLogEntry entry)
        {
            Console.WriteLine($"EndPoint:{entry.Endpoint}, Period:{entry.RateLimitPeriod}, Requests:{entry.TotalRequests}/{entry.RateLimit}");
        }
    }
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Configure Web API for self-host. 
            HttpConfiguration config = new HttpConfiguration();
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            //app.Use(typeof(ThrottlingMiddleware),
            //    ThrottlePolicy.FromStore(new PolicyConfigurationProvider()),
            //    new PolicyMemoryCacheRepository(),
            //    new SqlServerRepository("Server=localhost;Database=MyDB;Trusted_Connection=True;"),
            //    new MyLogger(),
            //    null);


            config.Filters.Add(new ThrottlingFilter(
                policy: new ThrottlePolicy(perSecond: 2, perMinute: 3)
                {
                    //scope to IPs
                    IpThrottling = true,
                    IpRules = new Dictionary<string, RateLimits>(),

                    //white list the "::1" IP to disable throttling on localhost for Win8
                    IpWhitelist = new List<string>(),

                    //scope to clients (if IP throttling is applied then the scope becomes a combination of IP and client key)
                    ClientThrottling = false,
                    ClientRules = new Dictionary<string, RateLimits>(),
                    //white list API keys that don’t require throttling
                    ClientWhitelist = new List<string>(),

                    //Endpoint rate limits will be loaded from EnableThrottling attribute
                    EndpointThrottling = true
                },
                policyRepository: new PolicyMemoryCacheRepository(),
                repository: new SqlServerRepository("Server=localhost;Database=MyDB;Trusted_Connection=True;"),
                logger: new MyLogger()));

            app.UseWebApi(config);
        }
    }
}
