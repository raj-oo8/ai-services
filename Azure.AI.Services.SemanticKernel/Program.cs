using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;
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

    internal class Orchestrator
    {
        private readonly IConfiguration _configuration;
        readonly string? endpoint;
        readonly string? modelId;
        readonly string? apiKey;

        public Orchestrator(IConfiguration configuration)
        {
            _configuration = configuration;
            endpoint = _configuration["AzureOpenAI:Endpoint"];
            modelId = _configuration["AzureOpenAI:ChatModelId"];
            apiKey = _configuration["AzureOpenAI:ApiKey"];
        }

        public async Task ChatAsync(CancellationToken ct)
        {
            if (endpoint is null || modelId is null || apiKey is null)
            {
                Console.WriteLine("Azure OpenAI credentials not found. Skipping example.");
                return;
            }

            // <RunningNativeFunction>
            var builder = Kernel.CreateBuilder()
                                .AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);
            //builder.Plugins.AddFromType<MathPlugin>();
            Kernel kernel = builder.Build();

            // Create chat history
            ChatHistory history = [];

            // <Chat>

            // Get chat completion service
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // Start the conversation
            Console.Write("User > ");
            string? userInput;
            while ((userInput = Console.ReadLine()) != null)
            {
                // Check if user input is 'exit'
                if (userInput.Trim().ToLower() == "exit")
                {
                    Console.WriteLine("Exit command received. Terminating application...");
                    Environment.Exit(0);
                }

                // Check if cancellation has been requested
                if (ct.IsCancellationRequested)
                {
                    Console.WriteLine("Cancellation requested. Exiting loop...");
                    break;
                }

                history.AddUserMessage(userInput);

                // Enable auto function calling
                //OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                //{
                //    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                //};

                // Get the response from the AI
                var result = chatCompletionService.GetStreamingChatMessageContentsAsync(
                                    history,
                                    //executionSettings: openAIPromptExecutionSettings,
                                    kernel: kernel);

                // Stream the results
                string fullMessage = "";
                var first = true;
                await foreach (var content in result)
                {
                    if (content.Role.HasValue && first)
                    {
                        Console.Write("Assistant > ");
                        first = false;
                    }
                    Console.Write(content.Content);
                    fullMessage += content.Content;
                }
                Console.WriteLine();

                // Add the message from the agent to the chat history
                history.AddAssistantMessage(fullMessage);

                // Get user input again
                Console.Write("User > ");
            }
        }
    }
}
