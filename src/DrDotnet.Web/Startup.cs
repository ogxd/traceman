using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net.Http;
using DrDotnet.Logging;
using DrDotnet.Utils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using MudBlazor.Services;

namespace DrDotnet.Web;

public class Startup
{
    private readonly bool _webUiEnabled;
    private readonly bool _restApiEnabled;
    private readonly bool _consoleLoggingEnabled;
    private readonly bool _fileLoggingEnabled;
    
    public Startup(IConfiguration configuration)
    {
        _consoleLoggingEnabled = configuration.GetValue("CONSOLE_LOGGING_ENABLED", true) && !Environment.GetCommandLineArgs().Contains("--no-console-logging");
        _fileLoggingEnabled = configuration.GetValue("FILE_LOGGING_ENABLED", true) && !Environment.GetCommandLineArgs().Contains("--no-file-logging");
        _webUiEnabled = configuration.GetValue("WEB_UI_ENABLED", true) && !Environment.GetCommandLineArgs().Contains("--no-web-ui");
        _restApiEnabled = configuration.GetValue("REST_API_ENABLED", true) && !Environment.GetCommandLineArgs().Contains("--no-rest-api");

        if (!_webUiEnabled) Console.WriteLine("Web UI is disabled");
        if (!_restApiEnabled) Console.WriteLine("REST API is disabled");

        if (!_webUiEnabled && !_restApiEnabled)
        {
            throw new InvalidOperationException("Both Web UI and REST API are disabled. There should be at least one of the two enabled.");
        }
    }

    // This method gets called by the runtime. Use this method to add services to the container.
    // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<HttpClient>();

        services.AddLogging(lb =>
        {
            if (_consoleLoggingEnabled)
            {
                lb.AddSimpleConsole();
            }

            if (_fileLoggingEnabled)
            {
                lb.AddFileLogger(Path.Combine(PathUtils.DrDotnetBaseDirectory, "app.log"));
            }
        });
        
        services.AddSingleton<ISessionDiscovery, SessionsDiscovery>();
        services.AddSingleton<IProcessDiscovery, ProcessDiscovery>();
        services.AddSingleton<IProfilerDiscovery, ProfilersDiscovery>();

        services.AddKeyedSingleton("app.log", (sp, key) => new FileContentWatcher(Path.Combine(PathUtils.DrDotnetBaseDirectory, "app.log")));
        services.AddKeyedSingleton("profiler.log", (sp, key) => new FileContentWatcher(Path.Combine(PathUtils.DrDotnetBaseDirectory, "profiler.log")));
        
        if (_webUiEnabled)
        {
            services.AddRazorPages();
            services.AddServerSideBlazor();
            services.AddMudServices();
        }

        if (_restApiEnabled)
        {
            services.AddControllers();
            services.AddSwaggerGen();
        }
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            if (_restApiEnabled)
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }
        }
        else
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        //app.UseHttpsRedirection();
        app.UseStaticFiles();
        
        app.UseStaticFiles(new StaticFileOptions() {
            FileProvider = new PhysicalFileProvider(PathUtils.DrDotnetBaseDirectory)
        });

        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            if (_webUiEnabled)
            {
                endpoints.MapBlazorHub();
                endpoints.MapFallbackToPage("/_Host");
            }
            
            if (_restApiEnabled)
            {
                endpoints.MapControllers();
            }
        });
    }
}