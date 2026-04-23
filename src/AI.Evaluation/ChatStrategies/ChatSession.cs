using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Spectre.Console;
using System.Text;

namespace PromptEval.ChatStrategies;

/// <summary>
/// Interactive console chat service.
/// Reads user input, sends it to the configured chat completion service,
/// streams the assistant response back to the console, and maintains chat history.
/// </summary>
internal class ChatSession
{
    public static async Task ExecuteChatSessionAsync(
        Kernel kernel,
        IHostApplicationLifetime lifetime,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var chatMessages = new ChatHistory();
        chatMessages.AddSystemMessage("Reply in plain natural language. Do not output JSON unless explicitly requested.");

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        SpectreConsole.WriteBanner();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Markup("[bold cyan]User > [/]"));

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

                if (SpectreConsole.IsExitCommand(userInput))
                {
                    logger.LogInformation("User requested application shutdown.");
                    AnsiConsole.MarkupLine("[grey]Goodbye.[/]");
                    lifetime.StopApplication();
                    break;
                }

                chatMessages.AddUserMessage(userInput);

                var assistantText = new StringBuilder();
                string? firstChunk = null;

                await using var enumerator = chatCompletionService
                    .GetStreamingChatMessageContentsAsync(
                        chatMessages,
                        executionSettings: null,
                        kernel: kernel,
                        cancellationToken: cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("yellow"))
                    .StartAsync("[yellow]Assistant is thinking...[/]", async _ =>
                    {
                        while (await enumerator.MoveNextAsync())
                        {
                            var content = enumerator.Current;

                            if (!string.IsNullOrWhiteSpace(content.Content))
                            {
                                firstChunk = content.Content;
                                break;
                            }
                        }
                    });

                AnsiConsole.Write(new Markup("[bold springgreen3]Assistant > [/]"));

                if (!string.IsNullOrEmpty(firstChunk))
                {
                    AnsiConsole.Write(new Text(firstChunk, SpectreConsole.AssistantStyle));
                    assistantText.Append(firstChunk);

                    while (await enumerator.MoveNextAsync())
                    {
                        var content = enumerator.Current;

                        if (string.IsNullOrWhiteSpace(content.Content))
                        {
                            continue;
                        }

                        AnsiConsole.Write(new Text(content.Content, SpectreConsole.AssistantStyle));
                        assistantText.Append(content.Content);
                    }

                    AnsiConsole.WriteLine();
                    chatMessages.AddAssistantMessage(assistantText.ToString());
                }
                else
                {
                    AnsiConsole.Write(new Text("(No response)", SpectreConsole.InfoStyle));
                    AnsiConsole.WriteLine();
                    logger.LogWarning("The assistant returned no content.");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogInformation("Console chat loop cancelled.");
                break;
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write(new Markup("[bold springgreen3]Assistant > [/]"));
                AnsiConsole.Write(new Text("Sorry, something went wrong.", SpectreConsole.AssistantStyle));
                AnsiConsole.WriteLine();

                logger.LogError(ex, "Unhandled exception in chat loop.");
            }
        }
    }
}
