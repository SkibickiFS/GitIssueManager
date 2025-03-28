using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Application.Interfaces;
using Application.Models;
using Application.Enums;
using Microsoft.Extensions.Configuration;

namespace Infrastructure.Services
{
    /// <summary>
    /// Service for interacting with the GitHub Issues API.
    /// Implements the IGitIssueService interface for GitHub operations.
    /// </summary>
    public class GitHubService : IGitIssueService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private const string GitHubTokenConfigKey = "GitProviders:GitHub:Token";
        private const string GitHubAcceptHeader = "application/vnd.github+json";

        /// <summary>
        /// Initializes a new instance of the <see cref="GitHubService"/> class.
        /// </summary>
        /// <param name="httpClient">The HttpClient instance configured for GitHub API calls.</param>
        /// <param name="configuration">The application configuration to retrieve API tokens.</param>
        public GitHubService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            if (_httpClient.DefaultRequestHeaders.Accept.All(h => h.MediaType != GitHubAcceptHeader))
            {
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue(GitHubAcceptHeader));
            }
        }

        /// <inheritdoc />
        public async Task<IssueDetailsResponse> AddIssueAsync(
            RepositoryInfo repoInfo,
            CreateIssueRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(repoInfo);
            ArgumentNullException.ThrowIfNull(request);
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ArgumentException("Issue title cannot be empty.", nameof(request.Title));
            }
            if (repoInfo.Provider != ProviderType.GitHub)
            {
                throw new ArgumentException("RepositoryInfo must specify GitHub provider.", nameof(repoInfo));
            }

            var apiToken = _configuration[GitHubTokenConfigKey];
            if (string.IsNullOrWhiteSpace(apiToken))
            {
                throw new InvalidOperationException($"GitHub API token is missing. Configure it using key: '{GitHubTokenConfigKey}'.");
            }

            var url = $"repos/{repoInfo.Owner}/{repoInfo.RepositoryName}/issues";

            var payload = new
            {
                title = request.Title,
                body = request.Description
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiToken);
            requestMessage.Content = JsonContent.Create(payload);

            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var serializerOptions = new JsonSerializerOptions
                    {
                        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
                    };

                    GitHubIssueApiResponse gitHubIssue = await response.Content.ReadFromJsonAsync<GitHubIssueApiResponse>(
                                                            serializerOptions,
                                                            cancellationToken)
                                                        ?? throw new InvalidOperationException("GitHub API returned null content unexpectedly.");

                    return MapToIssueDetailsResponse(gitHubIssue, repoInfo);
                }
                catch (JsonException jsonEx)
                {
                    throw;
                }
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException(
                    $"GitHub API request failed with status code {response.StatusCode}. URL: {url}. Response: {errorContent}",
                    null,
                    response.StatusCode);
            }
        }

        public Task<IssueDetailsResponse> UpdateIssueAsync(RepositoryInfo repoInfo, string issueId, UpdateIssueRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IssueDetailsResponse> CloseIssueAsync(RepositoryInfo repoInfo, string issueId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #region private helper methods

        /// <summary>
        /// Maps the GitHub API response object to the application's common IssueDetailsResponse model.
        /// </summary>
        private IssueDetailsResponse MapToIssueDetailsResponse(GitHubIssueApiResponse gitHubIssue, RepositoryInfo repoInfo)
        {
            return new IssueDetailsResponse
            {
                Id = gitHubIssue.NodeId ?? gitHubIssue.Id.ToString(), // Prefer NodeId as globally unique ID
                DisplayId = gitHubIssue.Number.ToString(),
                Title = gitHubIssue.Title ?? string.Empty,
                Description = gitHubIssue.Body,
                State = gitHubIssue.State ?? string.Empty,
                Url = gitHubIssue.HtmlUrl,
                Provider = ProviderType.GitHub,
                RepositoryName = repoInfo.RepositoryName,
                Owner = repoInfo.Owner
            };

        }

        /// <summary>
        /// Represents a response from the GitHub API for issues, mapping JSON properties to C# properties. Includes
        /// fields like Id, Number, Title, and State.
        /// </summary>
        private class GitHubIssueApiResponse
        {
            [JsonPropertyName("id")]
            public long Id { get; set; }

            [JsonPropertyName("node_id")]
            public string? NodeId { get; set; }

            [JsonPropertyName("number")]
            public int Number { get; set; }

            [JsonPropertyName("title")]
            public string? Title { get; set; }

            [JsonPropertyName("body")]
            public string? Body { get; set; }

            [JsonPropertyName("state")]
            public string? State { get; set; } // "open"

            [JsonPropertyName("html_url")]
            public string? HtmlUrl { get; set; }

        }

        #endregion    
    }
}