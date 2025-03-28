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
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(jsonResponseContent)
                })
                .Verifiable();
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

        #region Tests AddIssueAsync

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

        #region Tests UpdateIssueAsync

        [Fact]
        public async Task UpdateIssueAsync_Success_UpdatesTitleAndDescription_ReturnsMappedIssueDetails()
        {
            // Arrange
            var issueId = "1347";
            var request = new UpdateIssueRequest { Title = "Updated Title", Description = "Updated Desc" };
            SetupMockConfiguration(ValidGitHubToken);
            var fakeJsonResponse = $@"{{ ""id"": 12345, ""node_id"": ""NODE_ID_456"", ""number"": {issueId}, ""title"": ""{request.Title}"", ""body"": ""{request.Description}"", ""state"": ""open"", ""html_url"": ""http://github.com/issues/{issueId}"" }}";

            string? capturedContent = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    if (req.Content != null)
                    {
                        capturedContent = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                    }
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(fakeJsonResponse)
                });

            // Act
            var result = await _gitHubService.UpdateIssueAsync(_validRepoInfo, issueId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("NODE_ID_456", result.Id);
            Assert.Equal(request.Title, result.Title);
            Assert.Equal(request.Description, result.Description);

            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Patch
                    && req.RequestUri.ToString().Contains($"/repos/{_validRepoInfo.Owner}/{_validRepoInfo.RepositoryName}/issues/{issueId}")
                    && req.Headers.Authorization.ToString() == $"Bearer {ValidGitHubToken}"
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            Assert.NotNull(capturedContent);

            Assert.Contains($"\"title\":\"{request.Title}\"", capturedContent);
            Assert.Contains($"\"body\":\"{request.Description}\"", capturedContent);
        }

        [Fact]
        public async Task UpdateIssueAsync_Success_UpdatesOnlyDescription_SendsCorrectPayload()
        {
            var issueId = "1349";
            var request = new UpdateIssueRequest { Title = null, Description = "Only Updated Desc" };

            SetupMockConfiguration(ValidGitHubToken);

            var fakeJsonResponse = $@"{{ ""id"": 9101, ""node_id"": ""NODE_9101"", ""number"": {issueId}, ""title"": ""Old Title"", ""body"": ""{request.Description}"", ""state"": ""open"", ""html_url"": ""url"" }}";

            string? capturedContent = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    if (req.Content != null)
                    {
                        capturedContent = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                    }
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(fakeJsonResponse)
                });

            // Act
            var result = await _gitHubService.UpdateIssueAsync(_validRepoInfo, issueId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.Description, result.Description);
            Assert.Equal("Old Title", result.Title);

            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Patch
                    && req.RequestUri.ToString().Contains($"/issues/{issueId}")
                    && req.Headers.Authorization.ToString() == $"Bearer {ValidGitHubToken}"
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            Assert.NotNull(capturedContent);
            Assert.DoesNotContain("\"title\"", capturedContent);
            Assert.Contains($"\"body\":\"{request.Description}\"", capturedContent);
        }

        [Fact]
        public async Task UpdateIssueAsync_NotFound_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "non-existent-issue";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            var expectedStatusCode = HttpStatusCode.NotFound;
            var errorJson = @"{""message"": ""Not Found""}";

            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpResponse(expectedStatusCode, errorJson); // 404

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _gitHubService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            Assert.Contains($"issue '{issueId}' not found", exception.Message);
        }

        [Fact]
        public async Task UpdateIssueAsync_OtherApiError_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "1350";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            var expectedStatusCode = HttpStatusCode.Forbidden; // 403
            var errorJson = @"{""message"": ""Forbidden""}";

            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpResponse(expectedStatusCode, errorJson);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _gitHubService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            Assert.DoesNotContain("not found", exception.Message);
            Assert.Contains(errorJson, exception.Message);
        }


        [Fact]
        public async Task UpdateIssueAsync_InvalidJsonResponse_ThrowsJsonException()
        {
            // Arrange
            var issueId = "1351";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            var invalidJsonResponse = @"{""invalid_structure"": true}"; 

            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpResponse(HttpStatusCode.OK, invalidJsonResponse);

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(
                () => _gitHubService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
        }

        [Fact]
        public async Task UpdateIssueAsync_MissingToken_ThrowsInvalidOperationException()
        {
            // Arrange
            var issueId = "1352";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            SetupMockConfiguration(null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _gitHubService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
        }

        [Fact]
        public async Task UpdateIssueAsync_NoFieldsToUpdate_ThrowsArgumentException()
        {
            // Arrange
            var issueId = "1353";
            var request = new UpdateIssueRequest { Title = null, Description = null };

            // Act & Assert
            var exception = await Assert.ThrowsAsync<ArgumentException>(
                () => _gitHubService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
            Assert.Contains("At least Title or Description must be provided", exception.Message);
        }

        [Fact]
        public async Task UpdateIssueAsync_NullIssueId_ThrowsArgumentNullException()
        {
            // Arrange
            var request = new UpdateIssueRequest { Title = "Update Title" };
            string? invalidIssueId = null; 

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _gitHubService.UpdateIssueAsync(_validRepoInfo, invalidIssueId!, request)
            );
        }

        [Theory] 
        [InlineData("")]
        [InlineData(" ")]
        public async Task UpdateIssueAsync_EmptyOrWhitespaceIssueId_ThrowsArgumentException(string invalidIssueId)
        {
            // Arrange
            var request = new UpdateIssueRequest { Title = "Update Title" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _gitHubService.UpdateIssueAsync(_validRepoInfo, invalidIssueId, request)
            );
        }

        [Fact]
        public async Task UpdateIssueAsync_NetworkError_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "1354";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpException(new HttpRequestException("Simulated network error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                 () => _gitHubService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
        }

        #endregion

        #region Tests CloseIssueAsync

        [Fact]
        public async Task CloseIssueAsync_Success_SendsCorrectPayload_ReturnsMappedClosedIssue()
        {
            // Arrange
            var issueId = "1355";
            var expectedState = "closed";

            SetupMockConfiguration(ValidGitHubToken);

            var fakeJsonResponse = $@"{{
            ""id"": 9876,
            ""node_id"": ""NODE_9876"",
            ""number"": {issueId},
            ""title"": ""Issue To Be Closed"",
            ""body"": ""Some description"",
            ""state"": ""{expectedState}"",
            ""html_url"": ""url""
        }}";

            string? capturedContent = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) =>
                {
                    if (req.Content != null)
                    {
                        capturedContent = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                    }
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(fakeJsonResponse)
                });

            // Act
            var result = await _gitHubService.CloseIssueAsync(_validRepoInfo, issueId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedState, result.State);
            Assert.Equal("NODE_9876", result.Id);
            Assert.Equal(issueId, result.DisplayId);

            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Patch
                    && req.RequestUri.ToString().Contains($"/issues/{issueId}")
                    && req.Headers.Authorization.ToString() == $"Bearer {ValidGitHubToken}"
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            Assert.NotNull(capturedContent);
            using (var doc = JsonDocument.Parse(capturedContent))
            {
                Assert.True(doc.RootElement.TryGetProperty("state", out var stateProp));
                Assert.Equal("closed", stateProp.GetString());
                Assert.Equal(1, doc.RootElement.EnumerateObject().Count());
            }
        }

        [Fact]
        public async Task CloseIssueAsync_NotFound_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "non-existent";
            var expectedStatusCode = HttpStatusCode.NotFound;
            var errorJson = @"{""message"": ""Not Found""}"; // 404

            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpResponse(expectedStatusCode, errorJson);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _gitHubService.CloseIssueAsync(_validRepoInfo, issueId)
            );
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            Assert.Contains($"issue '{issueId}' not found", exception.Message);
            Assert.Contains("when trying to close", exception.Message);
        }

        [Fact]
        public async Task CloseIssueAsync_OtherApiError_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "1356";
            var expectedStatusCode = HttpStatusCode.Forbidden; // 403
            var errorJson = @"{""message"": ""Forbidden""}";

            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpResponse(expectedStatusCode, errorJson);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                 () => _gitHubService.CloseIssueAsync(_validRepoInfo, issueId)
            );
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            Assert.DoesNotContain("not found", exception.Message);
            Assert.Contains(errorJson, exception.Message);
            Assert.Contains("while closing issue", exception.Message);
        }

        [Fact]
        public async Task CloseIssueAsync_InvalidJsonResponse_ThrowsJsonException()
        {
            // Arrange
            var issueId = "1357";
            var invalidJsonResponse = @"{""malformed"; 

            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpResponse(HttpStatusCode.OK, invalidJsonResponse);

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(
                () => _gitHubService.CloseIssueAsync(_validRepoInfo, issueId)
            );
        }

        [Fact]
        public async Task CloseIssueAsync_MissingToken_ThrowsInvalidOperationException()
        {
            // Arrange
            var issueId = "1358";
            SetupMockConfiguration(null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _gitHubService.CloseIssueAsync(_validRepoInfo, issueId)
            );
        }

        [Fact]
        public async Task CloseIssueAsync_NullIssueId_ThrowsArgumentNullException()
        {
            // Arrange
            string? invalidIssueId = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _gitHubService.CloseIssueAsync(_validRepoInfo, invalidIssueId!)
            );
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task CloseIssueAsync_EmptyOrWhitespaceIssueId_ThrowsArgumentException(string invalidIssueId)
        {
            // Arrange
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _gitHubService.CloseIssueAsync(_validRepoInfo, invalidIssueId)
            );
        }

        [Fact]
        public async Task CloseIssueAsync_NetworkError_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "1359";
            SetupMockConfiguration(ValidGitHubToken);
            SetupMockHttpException(new HttpRequestException("Simulated network error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                 () => _gitHubService.CloseIssueAsync(_validRepoInfo, issueId)
            );
        }

        #endregion
    }
}