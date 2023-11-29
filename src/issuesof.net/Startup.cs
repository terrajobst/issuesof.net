using IssuesOfDotNet.Data;

using Terrajobst.GitHubEvents;
using Terrajobst.GitHubEvents.AspNetCore;

namespace IssuesOfDotNet;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddApplicationInsightsTelemetry(Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"]);
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddHostedService(p => p.GetRequiredService<GitHubEventProcessingService>());
        services.AddSingleton<IndexService>();
        services.AddSingleton<CompletionService>();
        services.AddSingleton<GitHubEventProcessor, EventService>();
        services.AddSingleton<GitHubEventProcessingService>();
        services.AddSingleton<SearchService>();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        // Warm up key services
        app.ApplicationServices.GetService<IndexService>();
        app.ApplicationServices.GetService<CompletionService>();
        app.ApplicationServices.GetService<GitHubEventProcessingService>();

        var gitHubWebHookSecret = Configuration["GitHubWebHookSecret"];

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapBlazorHub();
            endpoints.MapDefaultControllerRoute();
            endpoints.MapGitHubWebHook(secret: gitHubWebHookSecret);
            endpoints.MapFallbackToPage("/_Host");
        });
    }
}
