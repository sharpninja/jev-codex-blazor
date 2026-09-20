using Jev.Codex;
using Jev.Workers.Cli;
using Jev.Workers.Process;
using Jev.Workers.Strategies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jev.Workers;

public static class WorkerServiceCollectionExtensions
{
    public static IServiceCollection AddCodingWorkers(this IServiceCollection services, IConfiguration configuration)
    {
        if (services.All(descriptor => descriptor.ServiceType != typeof(ICodingHost)))
        {
            services.AddSingleton<ICodingHost>(CodingHost.Desktop("desktop"));
        }

        services.AddCodexCli(configuration);
        services.AddSingleton<ICliProcessRunner, CliProcessRunner>();

        services.AddOptions<ClaudeCliOptions>()
            .Bind(configuration.GetSection(ClaudeCliOptions.SectionName))
            .PostConfigure(options => ApplyEnv(options, "CLAUDE_EXECUTABLE", value => options.ExecutablePath = value));
        services.AddOptions<GrokBuildCliOptions>()
            .Bind(configuration.GetSection(GrokBuildCliOptions.SectionName))
            .PostConfigure(options => ApplyEnv(options, "GROK_EXECUTABLE", value => options.ExecutablePath = value));
        services.AddOptions<ClineCliOptions>()
            .Bind(configuration.GetSection(ClineCliOptions.SectionName))
            .PostConfigure(options => ApplyEnv(options, "CLINE_EXECUTABLE", value => options.ExecutablePath = value));

        services.AddSingleton<ICodingAgentStrategy, CodexCodingStrategy>();
        services.AddSingleton<ICodingAgentStrategy, ClaudeCodingStrategy>();
        services.AddSingleton<ICodingAgentStrategy, GrokBuildCodingStrategy>();
        services.AddSingleton<ICodingAgentStrategy, ClineCodingStrategy>();

        services.AddScoped<ICodingStrategySelector>(sp =>
        {
            var configured = configuration["Jev:CodingStrategy"]
                ?? Environment.GetEnvironmentVariable("JEV_CODING_STRATEGY");
            var kind = CodingStrategyKindParser.ParseOrDefault(configured);
            return new CodingStrategySelector(sp.GetRequiredService<IEnumerable<ICodingAgentStrategy>>(), kind);
        });

        return services;
    }

    private static void ApplyEnv<T>(T _, string name, Action<string> assign)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrWhiteSpace(value))
        {
            assign(value);
        }
    }
}
