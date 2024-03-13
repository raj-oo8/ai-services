using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Plugins.Core;

namespace Azure.AI.Services.SemanticKernel
{
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Azure OpenAI credentials not found. Skipping example.");
                return;
            }

            // <RunningNativeFunction>
            var builder = Kernel.CreateBuilder()
                                .AddAzureOpenAIChatCompletion(modelId, endpoint, apiKey);
            builder.Plugins.AddFromType<TimePlugin>();
            Kernel kernel = builder.Build();

            // Create chat history
            ChatHistory history = [];

            // Get chat completion service
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // Start the conversation
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.Write("User > ");
            string? userInput;
            while ((userInput = Console.ReadLine()) != null)
            {
                // Check if user input is 'exit'
                if (userInput.Trim().Equals("exit", StringComparison.CurrentCultureIgnoreCase))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Exit command received. Terminating application...");
                    Environment.Exit(0);
                }

                // Check if cancellation has been requested
                if (ct.IsCancellationRequested)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Cancellation requested. Exiting loop...");
                    break;
                }

                history.AddUserMessage(userInput);

                // Enable auto function calling
                OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                // Get the response from the AI
                var result = chatCompletionService.GetStreamingChatMessageContentsAsync(history, executionSettings: openAIPromptExecutionSettings, kernel: kernel, cancellationToken: ct);

                // Stream the results
                string fullMessage = "";
                var first = true;

                try
                {
                    Console.ForegroundColor = ConsoleColor.Green;
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
                    Console.WriteLine("\n");
                }
                catch (Exception exception)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Error: {exception.Message}");
                }

                // Add the message from the agent to the chat history
                history.AddAssistantMessage(fullMessage);

                // Get user input again
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.Write("User > ");
            }
        }
    }
}
