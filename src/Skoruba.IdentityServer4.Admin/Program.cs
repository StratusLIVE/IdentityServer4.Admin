using System.Linq;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using StratusLive.PlatformCore.ServiceDiscovery.Consul;
using System.Threading;
using System.Threading.Tasks;
using Serilog;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.DbContexts;
using Skoruba.IdentityServer4.Admin.EntityFramework.Shared.Entities.Identity;
using Skoruba.IdentityServer4.Admin.Helpers;
using System;

namespace Skoruba.IdentityServer4.Admin
{
    public class Program
    {
        private const string SeedArgs = "/seed";
        private static IHostingEnvironment HostingEnvironment;
        public static IConfigurationRoot Configuration;
        public static CancellationTokenSource CancellationTokenSource = new CancellationTokenSource();

        public static async Task Main(string[] args)
        {
            var seed = args.Any(x => x == SeedArgs);
            if (seed) args = args.Except(new[] { SeedArgs }).ToArray();

            var host = CreateWebHostBuilder(args)
                .ConfigureAppConfiguration((builderContext, config) =>
                {
                    var appConfig = config.Build();
                    HostingEnvironment = builderContext.HostingEnvironment;
                    config.AddConsulConfiguration(builderContext.HostingEnvironment, new Uri(appConfig.GetSection("ConsulUrl").Value), CancellationTokenSource.Token);                                        
                    Configuration = config.Build();
                    StratusLive.PlatformCore.Logging.LoggerExtensions.CreateLogger(Configuration);
                })
                .Build();

            // Uncomment this to seed upon startup, alternatively pass in `dotnet run /seed` to seed using CLI
             //await DbMigrationHelpers.EnsureSeedData<IdentityServerConfigurationDbContext, AdminIdentityDbContext, IdentityServerPersistedGrantDbContext, AdminLogDbContext, UserIdentity, UserIdentityRole>(host);
            if (seed)
            {
                await DbMigrationHelpers.EnsureSeedData<IdentityServerConfigurationDbContext, AdminIdentityDbContext, IdentityServerPersistedGrantDbContext, AdminLogDbContext, UserIdentity, UserIdentityRole>(host);
            }

            host.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                .UseSerilog()
                .UseStartup<Startup>();
    }
}