using System.Runtime.InteropServices;
using Jev.App;
using Jev.Linux;
using Jev.Workers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Photino.Blazor;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        var builder = PhotinoBlazorAppBuilder.CreateDefault(args);
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.AddSingleton<ICodingHost>(CodingHost.Desktop(
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux" : "desktop"));
        builder.Services.AddJevApp(configuration);
        builder.RootComponents.Add<App>("app");

        var app = builder.Build();
        app.MainWindow.SetTitle("Jev");
        app.Run();
    }
}
