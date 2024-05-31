using DrDotnet.Logging;
using DrDotnet.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;
using Photino.Blazor;

namespace DrDotnet.Desktop;

class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.IsDesktop = true;
        
        var appBuilder = PhotinoBlazorAppBuilder.CreateDefault(args);

        appBuilder.Services.AddLogging();

        appBuilder.Services.AddMudServices();

        appBuilder.Services.AddSingleton<HttpClient>();
        
        appBuilder.Services.AddLogging(lb => lb
            .AddSimpleConsole()
            .AddFileLogger(Path.Combine(PathUtils.DrDotnetBaseDirectory, "app.log")));

        appBuilder.Services.AddSingleton<ISessionDiscovery, SessionsDiscovery>();
        appBuilder.Services.AddSingleton<IProcessDiscovery, ProcessDiscovery>();
        appBuilder.Services.AddSingleton<IProfilerDiscovery, ProfilersDiscovery>();

        // Register root component and selector
        appBuilder.RootComponents.Add<App>("app");

        var app = appBuilder.Build();

        // Customize window
        app.MainWindow
            .SetIconFile("favicon.ico")
            .SetTitle("Dr-Dotnet");

        app.Run();
    }
}