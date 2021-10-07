using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.EventLog;
using System.Threading.Tasks;

namespace HyperWindowsService
{
    class Program
    {
        static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder(args)
                .ConfigureLogging(configureLogging => configureLogging.AddFilter<EventLogLoggerProvider>(level => level >= LogLevel.Information))
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging((builder) =>
                    {
                        builder.AddConsole();
                    });
                    services.AddSingleton<HyperClient>();
                    services.AddHostedService<WindowsSystemWatcherWorker>()
                        .Configure<EventLogSettings>(config =>
                        {
                            config.LogName = "Hyper Windows Service";
                            config.SourceName = "Hyper Windows Service Source";
                        });
                })
                .UseWindowsService()
                .Build()
                .RunAsync();
        }
    }

}
