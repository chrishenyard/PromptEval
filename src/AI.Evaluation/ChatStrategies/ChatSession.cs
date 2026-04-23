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
    private const int MaxContextTokens = 128_000;
    private const double ResetThresholdRatio = 0.85;
    private const int ApproxCharsPerToken = 4;
    private const int EstimatedTokensPerMessageOverhead = 8;

    private const string SystemPrompt =
        "Reply in plain natural language. Do not output JSON unless explicitly requested.";

    public static async Task ExecuteChatSessionAsync(
        Kernel kernel,
        IHostApplicationLifetime lifetime,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var chatMessages = CreateInitializedHistory();
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        SpectreConsole.WriteBanner();
        AnsiConsole.Write(new Text(
            $"Token limit: {MaxContextTokens:n0}. If usage reaches {(int)(ResetThresholdRatio * 100)}% of max, history will be reset.",
            SpectreConsole.InfoStyle));
        AnsiConsole.WriteLine();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                AnsiConsole.WriteLine();
                WriteTokenUsage(chatMessages);
                AnsiConsole.Write(new Markup("[bold cyan]User > [/]"));

                var userInput = await Console.In.ReadLineAsync(cancellationToken);

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

                if (SpectreConsole.IsResetCommand(userInput))
                {
                    ResetChatHistory(chatMessages, logger, "User requested chat history reset.");
                    continue;
                }

                chatMessages.AddUserMessage(userInput);

                if (ShouldResetHistory(chatMessages))
                {
                    AnsiConsole.Write(new Text(
                        "Token threshold reached. Chat history was cleared before processing this turn.",
                        SpectreConsole.InfoStyle));
                    AnsiConsole.WriteLine();

                    ResetChatHistory(chatMessages, logger, "Token threshold reached before assistant completion.");
                    chatMessages.AddUserMessage(userInput);
                }

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
                    WriteAssistantChunk(assistantText, firstChunk);

                    while (await enumerator.MoveNextAsync())
                    {
                        var content = enumerator.Current;

                        if (string.IsNullOrWhiteSpace(content.Content))
                        {
                            continue;
                        }

                        WriteAssistantChunk(assistantText, content.Content);
                    }

                    AnsiConsole.WriteLine();
                    chatMessages.AddAssistantMessage(assistantText.ToString());
                }
                else
                {
                    AnsiConsole.Write(new Text("(No response)", SpectreConsole.InfoStyle));
                    AnsiConsole.WriteLine();
                    logger.LogWarning("The assistant returned no content.");
                    chatMessages.AddAssistantMessage(string.Empty);
                }

                if (ShouldResetHistory(chatMessages))
                {
                    AnsiConsole.Write(new Text(
                        "Token threshold reached after response. Chat history has been cleared.",
                        SpectreConsole.InfoStyle));
                    AnsiConsole.WriteLine();

                    ResetChatHistory(chatMessages, logger, "Token threshold reached after assistant completion.");
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

    private static void WriteAssistantChunk(StringBuilder assistantText, string chunk)
    {
        var normalizedChunk = NormalizeChunkBoundary(assistantText, chunk);
        if (normalizedChunk.Length == 0)
        {
            return;
        }

        AnsiConsole.Write(new Text(normalizedChunk, SpectreConsole.AssistantStyle));
        assistantText.Append(normalizedChunk);
    }

    private static string NormalizeChunkBoundary(StringBuilder assistantText, string chunk)
    {
        if (assistantText.Length == 0 || string.IsNullOrEmpty(chunk))
        {
            return chunk;
        }

        var previousChar = assistantText[^1];
        var nextChar = chunk[0];

        if (ShouldInsertSpace(previousChar, nextChar))
        {
            return " " + chunk;
        }

        return chunk;
    }

    private static bool ShouldInsertSpace(char previousChar, char nextChar)
    {
        if (char.IsWhiteSpace(previousChar) || char.IsWhiteSpace(nextChar))
        {
            return false;
        }

        // No space if previous appears to terminate a sentence.
        if (previousChar is '.' or '!' or '?')
        {
            return false;
        }

        // Characters that should not have a preceding space.
        if (".,!?;:)]}\"'".Contains(nextChar))
        {
            return false;
        }

        // Characters that should not have a trailing space before next token.
        if ("([{/'\"".Contains(previousChar))
        {
            return false;
        }

        // Most common missing-space case.
        if (char.IsLetterOrDigit(previousChar) && char.IsLetterOrDigit(nextChar))
        {
            return true;
        }

        // Typical punctuation spacing in natural language.
        if ((previousChar is ',' or ';' or ':') && char.IsLetterOrDigit(nextChar))
        {
            return true;
        }

        return false;
    }

    private static ChatHistory CreateInitializedHistory()
    {
        var chatMessages = new ChatHistory();
        chatMessages.AddSystemMessage(SystemPrompt);
        chatMessages.AddUserMessage(
            $"Maximum available context tokens: {MaxContextTokens:n0}. " +
            $"When usage reaches {(int)(ResetThresholdRatio * 100)}% of this limit, history will be lost.");
        return chatMessages;
    }

    private static void ResetChatHistory(ChatHistory chatMessages, ILogger logger, string reason)
    {
        chatMessages.Clear();
        chatMessages.AddSystemMessage(SystemPrompt);
        chatMessages.AddUserMessage(
            $"Maximum available context tokens: {MaxContextTokens:n0}. " +
            $"When usage reaches {(int)(ResetThresholdRatio * 100)}% of this limit, history will be lost.");

        logger.LogInformation("{Reason}", reason);

        AnsiConsole.Write(new Text("Chat history reset.", SpectreConsole.InfoStyle));
        AnsiConsole.WriteLine();
    }

    private static bool ShouldResetHistory(ChatHistory chatMessages)
    {
        var usedTokens = EstimateChatHistoryTokens(chatMessages);
        var thresholdTokens = (int)(MaxContextTokens * ResetThresholdRatio);
        return usedTokens >= thresholdTokens;
    }

    private static void WriteTokenUsage(ChatHistory chatMessages)
    {
        var usedTokens = EstimateChatHistoryTokens(chatMessages);
        var percent = (double)usedTokens / MaxContextTokens * 100;

        AnsiConsole.Write(new Text(
            $"Token usage: ~{usedTokens:n0}/{MaxContextTokens:n0} ({percent:F1}%)",
            SpectreConsole.InfoStyle));
        AnsiConsole.WriteLine();
    }

    private static int EstimateChatHistoryTokens(ChatHistory chatMessages)
    {
        var total = 0;

        foreach (var message in chatMessages)
        {
            var text = message.Content ?? string.Empty;
            total += EstimatedTokensPerMessageOverhead;
            total += (int)Math.Ceiling(text.Length / (double)ApproxCharsPerToken);
        }

        return total;
    }
}
