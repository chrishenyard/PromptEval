using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace PromptEval.Config;

internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a chat completion service to the list. It can be either an OpenAI or Azure OpenAI backend service.
    /// </summary>
    /// <param name="kernelBuilder"></param>
    /// <param name="kernelSettings"></param>
    /// <exception cref="ArgumentException"></exception>
    internal static IServiceCollection AddChatCompletionService(this IServiceCollection serviceCollection, KernelSettings kernelSettings)
    {
        switch (kernelSettings.ServiceType.ToUpperInvariant())
        {
            case ServiceTypes.AzureOpenAI:
                serviceCollection = serviceCollection.AddAzureOpenAIChatCompletion(
                    kernelSettings.DeploymentId,
                    kernelSettings.Endpoint,
                    kernelSettings.ApiKey,
                    kernelSettings.ServiceId,
                    kernelSettings.ModelId);
                break;

            case ServiceTypes.OpenAI:
                serviceCollection = serviceCollection.AddOpenAIChatCompletion(
                    kernelSettings.ModelId,
                    new Uri(kernelSettings.Endpoint),
                    kernelSettings.ApiKey,
                    kernelSettings.OrgId,
                    kernelSettings.ServiceId);
                break;

            default:
                throw new ArgumentException($"Invalid service type value: {kernelSettings.ServiceType}");
        }

        return serviceCollection;
    }
}
