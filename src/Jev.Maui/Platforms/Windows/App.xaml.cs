namespace Jev.Maui.WinUI;

public partial class App : MauiWinUIApplication
{
    public App()
    {
        InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => Jev.Maui.MauiProgram.CreateMauiApp();
}
