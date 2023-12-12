using IssuesOfDotNet.Data;

using Terrajobst.GitHubEvents;
using Terrajobst.GitHubEvents.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationInsightsTelemetry(builder.Configuration["APPINSIGHTS_INSTRUMENTATIONKEY"]);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHostedService(p => p.GetRequiredService<GitHubEventProcessingService>());
builder.Services.AddActivatedSingleton<IndexService>();
builder.Services.AddActivatedSingleton<CompletionService>();
builder.Services.AddActivatedSingleton<GitHubEventProcessingService>();
builder.Services.AddSingleton<GitHubEventProcessor, EventService>();
builder.Services.AddSingleton<SearchService>();

var app = builder.Build();

var gitHubWebHookSecret = app.Configuration["GitHubWebHookSecret"];

if (app.Environment.IsDevelopment())
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

app.MapBlazorHub();
app.MapDefaultControllerRoute();
app.MapGitHubWebHook(secret: gitHubWebHookSecret);
app.MapFallbackToPage("/_Host");

app.Run();