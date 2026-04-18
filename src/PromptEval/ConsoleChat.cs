using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Text;

namespace PromptEval;

/// <summary>
/// Interactive console chat service.
/// Reads user input, sends it to the configured chat completion service,
/// streams the assistant response back to the console, and maintains chat history.
/// </summary>
internal sealed class ConsoleChat(
    Kernel kernel,
    IHostApplicationLifetime lifeTime,
    ILogger<ConsoleChat> logger) : IHostedService
{
    private readonly Kernel _kernel = kernel;
    private readonly IHostApplicationLifetime _lifeTime = lifeTime;
    private readonly ILogger<ConsoleChat> _logger = logger;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Task.Run(() => ExecuteAsync(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Console chat service is stopping.");
        return Task.CompletedTask;
    }

    private async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        var chatMessages = new ChatHistory();
        chatMessages.AddSystemMessage("Reply in plain natural language. Do not output JSON unless explicitly requested.");

        var chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();

        WriteBanner();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                Console.WriteLine();
                Console.Write("User > ");

                var userInput = Console.ReadLine();

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(userInput))
                {
                    continue;
                }

                userInput = userInput.Trim();

                if (IsExitCommand(userInput))
                {
                    _logger.LogInformation("User requested application shutdown.");
                    Console.WriteLine("Goodbye.");
                    _lifeTime.StopApplication();
                    break;
                }

                chatMessages.AddUserMessage(userInput);

                Console.Write("Assistant > ");

                var assistantText = new StringBuilder();
                var wroteAnyContent = false;

                await foreach (var content in chatCompletionService
                    .GetStreamingChatMessageContentsAsync(
                        chatMessages,
                        executionSettings: null,
                        kernel: _kernel,
                        cancellationToken: cancellationToken)
                    .WithCancellation(cancellationToken))
                {
                    if (string.IsNullOrEmpty(content.Content))
                    {
                        continue;
                    }

                    Console.Write(content.Content);
                    assistantText.Append(content.Content);
                    wroteAnyContent = true;
                }

                Console.WriteLine();

                if (assistantText.Length > 0)
                {
                    chatMessages.AddAssistantMessage(assistantText.ToString());
                }
                else if (!wroteAnyContent)
                {
                    Console.WriteLine("(No response)");
                    _logger.LogWarning("The assistant returned no content.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogInformation("Console chat loop cancelled.");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Assistant > Sorry, something went wrong.");
                _logger.LogError(ex, "Unhandled exception in chat loop.");
            }
        }
    }

    private static bool IsExitCommand(string input) =>
        input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("q", StringComparison.OrdinalIgnoreCase);

    private static void WriteBanner()
    {
        Console.WriteLine("Interactive chat started.");
        Console.WriteLine("Type your message and press Enter.");
        Console.WriteLine("Type 'exit' or 'quit' to close the application.");
    }
}
