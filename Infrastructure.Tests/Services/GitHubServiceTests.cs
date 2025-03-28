using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Application.Enums;
using Application.Models;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Xunit;

namespace Infrastructure.Tests.Services
{
    public class GitHubServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly HttpClient _httpClient;
        private readonly GitHubService _gitHubService;
        private readonly RepositoryInfo _validRepoInfo;
        private const string ValidGitHubToken = "fake-token-123";
        private const string GitHubTokenConfigKey = "GitProviders:GitHub:Token";

        public GitHubServiceTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockConfiguration = new Mock<IConfiguration>();

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.github.com/")
            };

            _gitHubService = new GitHubService(_httpClient, _mockConfiguration.Object);

            _validRepoInfo = new RepositoryInfo
            {
                Provider = ProviderType.GitHub,
                Owner = "test-owner",
                RepositoryName = "test-repo"
            };
        }

        #region Helper

        private void SetupMockConfiguration(string? tokenValue)
        {
            _mockConfiguration.Setup(c => c[GitHubTokenConfigKey]).Returns(tokenValue);
        }

        // HttpMessageHandler
        private void SetupMockHttpResponse(HttpStatusCode statusCode, string jsonResponseContent)
        {
            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(), // Dopasuj dowolne ¿¹danie lub bardziej szczegó³owo
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(jsonResponseContent)
                })
                .Verifiable(); // Oznaczamy jako weryfikowalne, jeœli chcemy sprawdziæ, czy zosta³o wywo³ane
        }

        // HttpMessageHandler web errors
        private void SetupMockHttpException(Exception exception)
        {
            _mockHttpMessageHandler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>()
               )
               .ThrowsAsync(exception);
        }

        #endregion

        #region Tests

        [Fact]
        public async Task AddIssueAsync_Success_ReturnsMappedIssueDetails()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Issue", Description = "Test Desc" };
            var expectedGitHubId = 12345L;
            var expectedGitHubNumber = 101;
            var expectedNodeId = "NODE_ID_123";
            var expectedUrl = "http://github.com/issues/101";
            var expectedState = "open";

            SetupMockConfiguration(ValidGitHubToken);

            var fakeJsonResponse = $@"{{
                ""id"": {expectedGitHubId},
                ""node_id"": ""{expectedNodeId}"",
                ""number"": {expectedGitHubNumber},
                ""title"": ""{request.Title}"",
                ""body"": ""{request.Description}"",
                ""state"": ""{expectedState}"",
                ""html_url"": ""{expectedUrl}""
            }}";
            SetupMockHttpResponse(HttpStatusCode.Created, fakeJsonResponse);

            // Act
            var result = await _gitHubService.AddIssueAsync(_validRepoInfo, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedNodeId, result.Id); 
            Assert.Equal(expectedGitHubNumber.ToString(), result.DisplayId);
            Assert.Equal(request.Title, result.Title);
            Assert.Equal(request.Description, result.Description);
            Assert.Equal(expectedState, result.State);
            Assert.Equal(expectedUrl, result.Url);
            Assert.Equal(ProviderType.GitHub, result.Provider);
            Assert.Equal(_validRepoInfo.RepositoryName, result.RepositoryName);
            Assert.Equal(_validRepoInfo.Owner, result.Owner);

            _mockHttpMessageHandler.Protected().Verify(
              "SendAsync",
              Times.Exactly(1), 
              ItExpr.Is<HttpRequestMessage>(req =>
                 req.Method == HttpMethod.Post 
                 && req.RequestUri.ToString().Contains($"/repos/{_validRepoInfo.Owner}/{_validRepoInfo.RepositoryName}/issues") 
                 && req.Headers.Authorization.ToString() == $"Bearer {ValidGitHubToken}" 
              ),
              ItExpr.IsAny<CancellationToken>()
           );
        }

        [Fact]
        public async Task AddIssueAsync_MissingToken_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Issue" };

            SetupMockConfiguration(null);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _gitHubService.AddIssueAsync(_validRepoInfo, request)
            );
            Assert.Contains(GitHubTokenConfigKey, exception.Message);
        }

        [Fact]
        public async Task AddIssueAsync_ApiReturnsError_ThrowsHttpRequestException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Issue" };
            var expectedStatusCode = HttpStatusCode.BadRequest; // 400
            var errorJson = @"{""message"": ""Validation Failed""}";

            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpResponse(expectedStatusCode, errorJson);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _gitHubService.AddIssueAsync(_validRepoInfo, request)
            );
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            Assert.Contains(errorJson, exception.Message);
        }

        [Fact]
        public async Task AddIssueAsync_InvalidJsonResponse_ThrowsJsonException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Issue" };
            var invalidJsonResponse = @"{""invalid_structure"": true, ""unexpected_field"": 123}";

            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpResponse(HttpStatusCode.Created, invalidJsonResponse);// 201

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(
                () => _gitHubService.AddIssueAsync(_validRepoInfo, request)
            );
        }

        [Fact]
        public async Task AddIssueAsync_NullTitle_ThrowsArgumentException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = null };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
               () => _gitHubService.AddIssueAsync(_validRepoInfo, request)
           );
        }

        [Fact]
        public async Task AddIssueAsync_NetworkError_ThrowsHttpRequestException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Issue" };
            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpException(new HttpRequestException("Simulated network error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                 () => _gitHubService.AddIssueAsync(_validRepoInfo, request)
            );
        } 
        #endregion
    }
}