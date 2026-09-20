using Jev.App.Chat;
using Jev.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Jev.App;

public static class JevAppServiceCollectionExtensions
{
    public static IServiceCollection AddJevApp(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddJevAgent(configuration);
        services.AddScoped<JevChatService>();
        return services;
    }
}
