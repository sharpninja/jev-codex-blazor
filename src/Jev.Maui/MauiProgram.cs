using Jev.App;
using Jev.Workers;

namespace Jev.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

#if ANDROID
        builder.Services.AddSingleton<ICodingHost>(CodingHost.Restricted("android"));
#else
        builder.Services.AddSingleton<ICodingHost>(CodingHost.Desktop("windows"));
#endif
        builder.Services.AddJevApp(builder.Configuration);
        return builder.Build();
    }
}
