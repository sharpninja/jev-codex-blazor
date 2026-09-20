using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jev.Codex;

public static class CodexServiceCollectionExtensions
{
    public static IServiceCollection AddCodexCli(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CodexCliOptions>()
            .Bind(configuration.GetSection(CodexCliOptions.SectionName))
            .PostConfigure(options =>
            {
                var executable = Environment.GetEnvironmentVariable("CODEX_EXECUTABLE");
                if (!string.IsNullOrWhiteSpace(executable))
                {
                    options.ExecutablePath = executable;
                }

                var sandbox = Environment.GetEnvironmentVariable("CODEX_DEFAULT_SANDBOX");
                if (!string.IsNullOrWhiteSpace(sandbox))
                {
                    options.DefaultSandbox = sandbox;
                }

                if (int.TryParse(Environment.GetEnvironmentVariable("CODEX_TIMEOUT_SECONDS"), out var timeout))
                {
                    options.TimeoutSeconds = timeout;
                }
            });

        services.AddSingleton<ICodexProcessRunner, CodexProcessRunner>();
        services.AddSingleton<ICodexCli, CodexCliClient>();
        return services;
    }
}
