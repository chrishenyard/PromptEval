using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Spectre.Console;
using System.Collections.Frozen;
using System.Text;

namespace PromptEval.ChatStrategies;

internal class PromptEvaluation
{
    private readonly FrozenDictionary<string, string> _prompts = FrozenDictionary.ToFrozenDictionary<string, string>(new Dictionary<string, string>
    {
        ["simple"] = "Please provide a solution to the following task: {task}",
        ["detailed"] = "Please provide a detailed solution to the following problem, including step-by-step reasoning: {problem}"
    });

    private static string GenerateDataset() =>
    @"
    Generate an evaluation dataset for a prompt evaluation. The dataset will be used to evaluate prompts that generate Python, JSON, or Regex specifically for AWS-related tasks. Generate an array of JSON objects, each representing task that requires Python, JSON, or a Regex to complete.

    Example output:
    ```json
    [
      {
        ""task\"": ""Description of task"",
      },
      ...additional
    ]
    ```

    * Focus on tasks that can be solved by writing a single Python function, a single JSON object, or a single regex
    * Focus on tasks that do not require writing much code

    Please generate 3 objects.
    ";

    public static async Task ExecutePromptEvaluationAsync(
        Kernel kernel,
        IHostApplicationLifetime lifetime,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        await ExecuteDatasetAsync(kernel, lifetime, logger, cancellationToken);
    }

    public static async Task ExecuteDatasetAsync(
        Kernel kernel,
        IHostApplicationLifetime lifetime,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var chatMessages = new ChatHistory();
        chatMessages.AddSystemMessage("You are a helpful assistant.");

        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        ConsoleHelpers.WriteBanner();

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
                    userInput = string.Empty;
                }

                userInput = userInput.Trim();

                if (ConsoleHelpers.IsExitCommand(userInput))
                {
                    logger.LogInformation("User requested application shutdown.");
                    AnsiConsole.MarkupLine("[grey]Goodbye.[/]");
                    lifetime.StopApplication();
                    break;
                }

                userInput = GenerateDataset();
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
                    AnsiConsole.Write(new Text(firstChunk, ConsoleHelpers.AssistantStyle));
                    assistantText.Append(firstChunk);

                    while (await enumerator.MoveNextAsync())
                    {
                        var content = enumerator.Current;

                        if (string.IsNullOrWhiteSpace(content.Content))
                        {
                            continue;
                        }

                        AnsiConsole.Write(new Text(content.Content, ConsoleHelpers.AssistantStyle));
                        assistantText.Append(content.Content);
                    }

                    AnsiConsole.WriteLine();
                    chatMessages.AddAssistantMessage(assistantText.ToString());
                }
                else
                {
                    AnsiConsole.Write(new Text("(No response)", ConsoleHelpers.InfoStyle));
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
                AnsiConsole.Write(new Text("Sorry, something went wrong.", ConsoleHelpers.AssistantStyle));
                AnsiConsole.WriteLine();

                logger.LogError(ex, "Unhandled exception in chat loop.");
            }
        }
    }
}
