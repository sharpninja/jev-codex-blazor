using Jev.Core.Persona;
using Jev.Core.Runtime;
using Jev.Core.Simulation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Jev.Workers;

namespace Jev.Core;

public static class JevServiceCollectionExtensions
{
    public static IServiceCollection AddJevAgent(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCodingWorkers(configuration);

        services.AddOptions<JevAgentOptions>()
            .Bind(configuration.GetSection(JevAgentOptions.SectionName));

        services.AddSingleton<JevPersona>();
        services.AddScoped<IJevRunStatus, JevRunStatus>();
        services.AddScoped<ICliTranscript, CliTranscript>();
        services.AddScoped<JevCliSimulator>();
        return services;
    }
}
