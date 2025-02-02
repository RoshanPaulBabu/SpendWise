// SpendWise/Program.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SpendWise
{
    public class Program
    {
        public static void Main(string[] args) =>
            CreateHostBuilder(args).Build().Run();

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureLogging((context, logging) =>
                    {
                        logging.ClearProviders();
                        logging.AddDebug();
                        logging.AddConsole();
                        logging.SetMinimumLevel(
                            context.HostingEnvironment.IsDevelopment() ?
                            LogLevel.Debug : LogLevel.Warning);
                    });
                    webBuilder.UseStartup<Startup>();
                });
    }
}