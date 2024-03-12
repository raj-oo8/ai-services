using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Azure.AI.Services.SemanticKernel
{
    internal class Program
    {
        public static void Main()
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);

            var serviceProvider = serviceCollection.BuildServiceProvider();

            // Create a cancellation token that listens for Ctrl+C or Ctrl+Break
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, args) =>
            {
                Console.WriteLine("Shutdown requested. Terminating...");
                cts.Cancel();
                args.Cancel = true; // Prevent the process from terminating immediately
            };

            // Resolve your class from the service provider
            Orchestrator? orchestrator = serviceProvider.GetService<Orchestrator>();
            if (orchestrator != null)
            {
                try
                {
                    orchestrator.ChatAsync(cts.Token).Wait();
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Operation was cancelled. Exiting...");
                }
            }
            else
            {
                Console.WriteLine("Failed to resolve Orchestrator from the service provider.");
            }
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddTransient<Orchestrator>();
        }
    }
}
