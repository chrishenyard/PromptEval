using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using PromptEval.ChatStrategies;
using PromptEval.config;
using PromptEval.Config;

namespace PromptEval;

internal sealed class ConsoleChat(
    Kernel kernel,
    IHostApplicationLifetime lifeTime,
    IOptions<AppSettings> appSettings,
    ILogger<ConsoleChat> logger) : IHostedService
{
    private readonly Kernel _kernel = kernel;
    private readonly IHostApplicationLifetime _lifeTime = lifeTime;
    private readonly ILogger<ConsoleChat> _logger = logger;
    private readonly AppSettings _appSettings = appSettings.Value;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        switch (_appSettings.ChatType.ToUpperInvariant())
        {
            case ChatTypes.ChatSession:
                _ = Task.Run(() => ChatSession.ExecuteChatSessionAsync(
                    _kernel, _lifeTime, _logger, cancellationToken), cancellationToken);
                break;
            case ChatTypes.PromptEvaluation:
                _ = Task.Run(() => PromptEvaluation.ExecutePromptEvaluationAsync(
                    _kernel, _lifeTime, _logger, cancellationToken), cancellationToken);
                break;
            default:
                _logger.LogError("Unsupported chat type: {ChatType}", _appSettings.ChatType);
                _lifeTime.StopApplication();
                return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Console chat service is stopping.");
        return Task.CompletedTask;
    }
}