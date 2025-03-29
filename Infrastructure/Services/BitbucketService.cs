using Application.Enums;
using Application.Interfaces;
using Application.Models;
using Microsoft.Extensions.Configuration;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;


namespace Infrastructure.Services
{
    /// <summary>
    /// Service for interacting with the Bitbucket Issues API (v2.0).
    /// Implements the IGitIssueService interface for Bitbucket specific operations.
    /// </summary>
    public class BitbucketService : IGitIssueService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private const string BitbucketUsernameConfigKey = "GitProviders:Bitbucket:Username";
        private const string BitbucketAppPasswordConfigKey = "GitProviders:Bitbucket:AppPassword";
        private const string BitbucketAcceptHeader = "application/json";

        public BitbucketService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            if (_httpClient.DefaultRequestHeaders.Accept.All(h => h.MediaType != BitbucketAcceptHeader))
            {
                _httpClient.DefaultRequestHeaders.Accept.Add(
                   new MediaTypeWithQualityHeaderValue(BitbucketAcceptHeader));
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
            ArgumentException.ThrowIfNullOrWhiteSpace(request.Title);

            if (repoInfo.Provider != ProviderType.Bitbucket)
            {
                throw new ArgumentException("RepositoryInfo must specify Bitbucket provider.", nameof(repoInfo));
            }

            var username = _configuration[BitbucketUsernameConfigKey];
            var appPassword = _configuration[BitbucketAppPasswordConfigKey];

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(appPassword))
            {
                throw new InvalidOperationException($"Bitbucket username or App Password is missing. Configure them using keys: '{BitbucketUsernameConfigKey}', '{BitbucketAppPasswordConfigKey}'.");
            }

            // /repositories/{workspace}/{repo_slug}/issues
            var url = $"repositories/{repoInfo.Owner}/{repoInfo.RepositoryName}/issues";

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{appPassword}"));
            var authHeader = new AuthenticationHeaderValue("Basic", credentials);

            object payload;
            if (request.Description != null)
            {
                payload = new { title = request.Title, content = new { raw = request.Description } };
            }
            else
            {
                payload = new { title = request.Title };
            }

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Authorization = authHeader;
            requestMessage.Content = JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });

            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var serializerOptions = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    };

                    BitbucketIssueApiResponse bitbucketIssue = await response.Content.ReadFromJsonAsync<BitbucketIssueApiResponse>(
                                                                serializerOptions,
                                                                cancellationToken)
                                                             ?? throw new InvalidOperationException("Bitbucket API returned null content unexpectedly.");
                    return MapToIssueDetailsResponse(bitbucketIssue, repoInfo);
                }
                catch (JsonException jsonEx)
                {
                    throw;
                }
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    throw new HttpRequestException($"Bitbucket API authentication failed. Check username/App Password. Status code: {response.StatusCode}. Response: {errorContent}", null, response.StatusCode);
                }
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new HttpRequestException($"Bitbucket repository '{repoInfo.Owner}/{repoInfo.RepositoryName}' not found. Status code: {response.StatusCode}. Response: {errorContent}", null, response.StatusCode);
                }
                throw new HttpRequestException(
                    $"Bitbucket API request failed with status code {response.StatusCode}. URL: {url}. Response: {errorContent}",
                    null,
                    response.StatusCode);
            }
        }

        /// <inheritdoc />
        public async Task<IssueDetailsResponse> UpdateIssueAsync(
            RepositoryInfo repoInfo,
            string issueId, // 'issue_id'
            UpdateIssueRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(repoInfo);
            ArgumentException.ThrowIfNullOrWhiteSpace(issueId);
            ArgumentNullException.ThrowIfNull(request);

            if (repoInfo.Provider != ProviderType.Bitbucket)
            {
                throw new ArgumentException("RepositoryInfo must specify Bitbucket provider.", nameof(repoInfo));
            }

            if (request.Title == null && request.Description == null)
            {
                throw new ArgumentException("At least Title or Description must be provided for update.", nameof(request));
            }

            var username = _configuration[BitbucketUsernameConfigKey];
            var appPassword = _configuration[BitbucketAppPasswordConfigKey];
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(appPassword))
            {
                throw new InvalidOperationException($"Bitbucket username or App Password is missing. Configure them using keys: '{BitbucketUsernameConfigKey}', '{BitbucketAppPasswordConfigKey}'.");
            }

            var url = $"repositories/{repoInfo.Owner}/{repoInfo.RepositoryName}/issues/{issueId}";
            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{appPassword}"));
            var authHeader = new AuthenticationHeaderValue("Basic", credentials);

            var payload = new Dictionary<string, object>();
            if (request.Title != null)
            {
                payload["title"] = request.Title;
            }
            if (request.Description != null)
            {
                payload["content"] = new { raw = request.Description };
            }

            using var requestMessage = new HttpRequestMessage(HttpMethod.Put, url);
            requestMessage.Headers.Authorization = authHeader;
            requestMessage.Content = JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            HttpResponseMessage response = await _httpClient.SendAsync(requestMessage, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                try
                {
                    var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    BitbucketIssueApiResponse bitbucketIssue = await response.Content.ReadFromJsonAsync<BitbucketIssueApiResponse>(
                                                                serializerOptions,
                                                                cancellationToken)
                                                             ?? throw new InvalidOperationException("Bitbucket API returned null content unexpectedly after update.");
                    return MapToIssueDetailsResponse(bitbucketIssue, repoInfo);
                }
                catch (JsonException jsonEx)
                {
                    throw;
                }
            }
            else
            {
                string errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    throw new HttpRequestException($"Bitbucket issue '{issueId}' or repository '{repoInfo.Owner}/{repoInfo.RepositoryName}' not found. Status code: {response.StatusCode}. Response: {errorContent}", null, response.StatusCode);
                }
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new HttpRequestException($"Bitbucket API authentication/authorization failed when updating issue. Status code: {response.StatusCode}. Response: {errorContent}", null, response.StatusCode);
                }
                throw new HttpRequestException(
                    $"Bitbucket API request failed with status code {response.StatusCode} while updating issue '{issueId}'. URL: {url}. Response: {errorContent}",
                    null,
                    response.StatusCode);
            }
        }

        public Task<IssueDetailsResponse> CloseIssueAsync(RepositoryInfo repoInfo, string issueId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        #region private helper methods

        private IssueDetailsResponse MapToIssueDetailsResponse(BitbucketIssueApiResponse bbIssue, RepositoryInfo repoInfo)
        {
            return new IssueDetailsResponse
            {
                Id = bbIssue.Id.ToString(),
                DisplayId = bbIssue.Id.ToString(),
                Title = bbIssue.Title ?? string.Empty,
                Description = bbIssue.Content?.Raw,
                State = bbIssue.State ?? "unknown",
                Url = bbIssue.Links?.Html?.Href,
                Provider = ProviderType.Bitbucket,
                RepositoryName = repoInfo.RepositoryName,
                Owner = repoInfo.Owner
            };
        }

        private class BitbucketIssueApiResponse
        {
            public int Id { get; set; }
            public string? Title { get; set; }
            public BitbucketContent? Content { get; set; }
            public string? State { get; set; }
            public BitbucketLinks? Links { get; set; }
        }

        private class BitbucketContent
        {
            public string? Raw { get; set; }
            public string? Markup { get; set; }
            public string? Html { get; set; }
        }

        private class BitbucketLinks
        {
            public BitbucketLink? Html { get; set; }
        }

        private class BitbucketLink
        {
            public string? Href { get; set; }
        }

        #endregion
    }
}
