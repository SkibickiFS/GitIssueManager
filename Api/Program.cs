using Application.Enums;
using Application.Interfaces;
using Infrastructure.Services;
using System.Net.Http.Headers;
using Api.Factories; 
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Config HttpClientFactory
const string GitHubClientName = "GitHubClient";
const string BitbucketClientName = "BitbucketClient";

builder.Services.AddHttpClient(GitHubClientName, client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitIssueManagerApp/1.0");
});

builder.Services.AddHttpClient(BitbucketClientName, client =>
{
    client.BaseAddress = new Uri("https://api.bitbucket.org/2.0/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitIssueManagerApp/1.0");
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
});

builder.Services.AddKeyedScoped<IGitIssueService, GitHubService>(ProviderType.GitHub, (sp, key) =>
{
    var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = clientFactory.CreateClient(GitHubClientName);
    var configuration = sp.GetRequiredService<IConfiguration>();

    return new GitHubService(httpClient, configuration);
});

builder.Services.AddKeyedScoped<IGitIssueService, BitbucketService>(ProviderType.Bitbucket, (sp, key) =>
{
    var clientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var httpClient = clientFactory.CreateClient(BitbucketClientName);
    var configuration = sp.GetRequiredService<IConfiguration>();

    return new BitbucketService(httpClient, configuration);
});

builder.Services.AddScoped<IGitIssueServiceFactory, GitIssueServiceFactory>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// app.UseAuthorization();

app.MapControllers();

app.Run();
