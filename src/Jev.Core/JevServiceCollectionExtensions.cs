using Jev.Codex;
using Jev.Core.Agent;
using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Tools;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jev.Core;

public static class JevServiceCollectionExtensions
{
    public static IServiceCollection AddJevAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCodexCli(configuration);

        services.AddOptions<JevAgentOptions>()
            .Bind(configuration.GetSection(JevAgentOptions.SectionName));
        services.AddOptions<OpenAIOptions>()
            .Bind(configuration.GetSection(OpenAIOptions.SectionName))
            .PostConfigure(options =>
            {
                options.ApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                var model = Environment.GetEnvironmentVariable("OPENAI_MODEL");
                if (!string.IsNullOrWhiteSpace(model))
                {
                    options.Model = model;
                }
            });

        services.AddSingleton<JevPersona>();
        services.AddScoped<IJevRunStatus, JevRunStatus>();
        services.AddScoped<JevCodexTools>();
        services.AddScoped<JevAgentFactory>();
        services.AddScoped(sp => sp.GetRequiredService<JevAgentFactory>().Create());
        services.AddScoped(sp => sp.GetRequiredService<JevAgentHandle>().Agent);
        return services;
    }
}
