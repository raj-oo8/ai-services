using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureAISearch;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Memory;
using Microsoft.SemanticKernel.Plugins.Core;

// Memory functionality is experimental
#pragma warning disable SKEXP0001, SKEXP0003, SKEXP0010, SKEXP0050, SKEXP0011, SKEXP0020

namespace Azure.AI.Services.SemanticKernel
{
    internal class Orchestrator
    {
        private readonly IConfiguration _configuration;
        readonly string? endpoint;
        readonly string? chatModelId;
        readonly string? apiKey;
        readonly string? embeddingsModelId;

        public Orchestrator(IConfiguration configuration)
        {
            _configuration = configuration;
            endpoint = _configuration["AzureOpenAI:Endpoint"];
            chatModelId = _configuration["AzureOpenAI:ChatModelId"];
            embeddingsModelId = _configuration["AzureOpenAI:EmbeddingsModelId"];  
            apiKey = _configuration["AzureOpenAI:ApiKey"];
        }

        public async Task ChatAsync(CancellationToken ct)
        {
            if (endpoint is null || chatModelId is null || apiKey is null || embeddingsModelId is null)
            {
                Console.WriteLine("Azure OpenAI credentials not found. Skipping example.");
                return;
            }

            // <RunningNativeFunction>
            var builder = Kernel.CreateBuilder()
                                .AddAzureOpenAIChatCompletion(nameof(Orchestrator), endpoint, apiKey, modelId: chatModelId);
            builder.Plugins.AddFromType<TimePlugin>();
            Kernel kernel = builder.Build();

            var memoryBuilder = new MemoryBuilder();
            memoryBuilder.WithAzureOpenAITextEmbeddingGeneration(nameof(Orchestrator), endpoint, apiKey, embeddingsModelId);
            //memoryBuilder.WithMemoryStore(new AzureAISearchMemoryStore(AZURE_SEARCH_ENDPOINT, AZURE_SEARCH_API_KEY));

            // Create chat history
            ChatHistory history = [];

            // Get chat completion service
            var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

            // Start the conversation
            Console.Write("User > ");
            string? userInput;
            while ((userInput = Console.ReadLine()) != null)
            {
                // Check if user input is 'exit'
                if (userInput.Trim().Equals("exit", StringComparison.CurrentCultureIgnoreCase))
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
                OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
                {
                    ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
                };

                // Get the response from the AI
                var result = chatCompletionService.GetStreamingChatMessageContentsAsync(history, executionSettings: openAIPromptExecutionSettings, kernel: kernel, cancellationToken: ct);

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
