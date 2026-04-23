using Spectre.Console;

namespace PromptEval.ChatStrategies;

internal static class SpectreConsole
{
    public static readonly Style AssistantStyle = new(Color.SpringGreen3);
    public static readonly Style BannerStyle = new(Color.Grey);
    public static readonly Style InfoStyle = new(Color.Yellow);

    public static bool IsExitCommand(string input) =>
        input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("quit", StringComparison.OrdinalIgnoreCase) ||
        input.Equals("q", StringComparison.OrdinalIgnoreCase);

    public static void WriteBanner()
    {
        var rule = new Rule("[bold white]Interactive Chat[/]");
        rule.Justification = Justify.Left;
        AnsiConsole.Write(rule);

        AnsiConsole.Write(new Text("Type your message and press Enter.", SpectreConsole.BannerStyle));
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Text("Type 'exit' or 'quit' to close the application.", SpectreConsole.BannerStyle));
        AnsiConsole.WriteLine();
    }
}
