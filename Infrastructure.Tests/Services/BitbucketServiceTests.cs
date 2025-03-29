using Application.Enums;
using Application.Models;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Infrastructure.Tests.Services
{
    public class BitbucketServiceTests
    {
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<IConfiguration> _mockConfiguration;
        private readonly HttpClient _httpClient;
        private readonly BitbucketService _bitbucketService;
        private readonly RepositoryInfo _validRepoInfo;
        private const string ValidBitbucketUsername = "test@example.com";
        private const string ValidBitbucketAppPassword = "fake-app-password";
        private const string BitbucketUsernameConfigKey = "GitProviders:Bitbucket:Username";
        private const string BitbucketAppPasswordConfigKey = "GitProviders:Bitbucket:AppPassword";

        public BitbucketServiceTests()
        {
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockConfiguration = new Mock<IConfiguration>();

            _httpClient = new HttpClient(_mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("https://api.bitbucket.org/2.0/")
            };

            _bitbucketService = new BitbucketService(_httpClient, _mockConfiguration.Object);

            _validRepoInfo = new RepositoryInfo
            {
                Provider = ProviderType.Bitbucket,
                Owner = "test-workspace",
                RepositoryName = "test-slug"
            };
        }

        #region Helper

        private void SetupMockBitbucketCredentials(string? username, string? appPassword)
        {
            _mockConfiguration.Setup(c => c[BitbucketUsernameConfigKey]).Returns(username);
            _mockConfiguration.Setup(c => c[BitbucketAppPasswordConfigKey]).Returns(appPassword);
        }

        // HttpMessageHandler
        private void SetupMockHttpResponseForBitbucket(HttpStatusCode statusCode, string jsonResponseContent, out string? capturedContent)
        {
            string? content = null;
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
                        content = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                    }
                })
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = statusCode,
                    Content = new StringContent(jsonResponseContent)
                });
            capturedContent = content;
        }

        // HttpMessageHandler web errors
        private void SetupMockHttpExceptionForBitbucket(Exception exception)
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
        public async Task AddIssueAsync_Success_SendsCorrectPayloadAndAuth_ReturnsMappedIssue()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Bitbucket Test", Description = "BB Desc" };
            var expectedBitbucketId = 54321;
            var expectedState = "new";
            var expectedUrl = "https://bitbucket.org/test-workspace/test-slug/issues/54321";

            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);

            var fakeJsonResponse = $@"{{
                ""id"": {expectedBitbucketId},
                ""title"": ""{request.Title}"",
                ""content"": {{ ""raw"": ""{request.Description}"", ""markup"": ""markdown"", ""html"": ""<p>BB Desc</p>"" }},
                ""state"": ""{expectedState}"",
                ""links"": {{ ""html"": {{ ""href"": ""{expectedUrl}"" }} }}
            }}";

            var expectedAuthParam = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidBitbucketUsername}:{ValidBitbucketAppPassword}"));

            string? capturedContent = null;
            _mockHttpMessageHandler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
               .Callback<HttpRequestMessage, CancellationToken>((req, ct) => { if (req.Content != null) { capturedContent = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult(); } })
               .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.Created, Content = new StringContent(fakeJsonResponse) });


            // Act
            var result = await _bitbucketService.AddIssueAsync(_validRepoInfo, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedBitbucketId.ToString(), result.Id);
            Assert.Equal(expectedBitbucketId.ToString(), result.DisplayId);
            Assert.Equal(request.Title, result.Title);
            Assert.Equal(request.Description, result.Description);
            Assert.Equal(expectedState, result.State);
            Assert.Equal(expectedUrl, result.Url);
            Assert.Equal(ProviderType.Bitbucket, result.Provider);

            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                && req.RequestUri.AbsoluteUri == $"https://api.bitbucket.org/2.0/repositories/{_validRepoInfo.Owner}/{_validRepoInfo.RepositoryName}/issues"
                && req.Headers.Authorization != null
                && req.Headers.Authorization.Scheme == "Basic"
                && req.Headers.Authorization.Parameter == expectedAuthParam
               ),
               ItExpr.IsAny<CancellationToken>()
            );

            Assert.NotNull(capturedContent);
            using (var doc = JsonDocument.Parse(capturedContent))
            {
                Assert.True(doc.RootElement.TryGetProperty("title", out var titleProp) && titleProp.GetString() == request.Title);
                Assert.True(doc.RootElement.TryGetProperty("content", out var contentProp) && contentProp.TryGetProperty("raw", out var rawProp) && rawProp.GetString() == request.Description);
                Assert.Equal(2, doc.RootElement.EnumerateObject().Count());
            }
        }

        [Fact]
        public async Task AddIssueAsync_Success_NoDescription_SendsCorrectPayload()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "BB No Desc", Description = null };
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);
            var fakeJsonResponse = $@"{{ ""id"": 54322, ""title"": ""{request.Title}"", ""content"": null, ""state"": ""new"", ""links"": {{ ""html"": {{ ""href"": ""url"" }} }} }}";
            var expectedAuthParam = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidBitbucketUsername}:{ValidBitbucketAppPassword}"));
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
                    StatusCode = HttpStatusCode.Created,
                    Content = new StringContent(fakeJsonResponse)
                });


            // Act
            await _bitbucketService.AddIssueAsync(_validRepoInfo, request);

            // Assert
            _mockHttpMessageHandler.Protected().Verify(
                "SendAsync",
                Times.Exactly(1),
                 ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post
                    && req.RequestUri.AbsoluteUri == $"https://api.bitbucket.org/2.0/repositories/{_validRepoInfo.Owner}/{_validRepoInfo.RepositoryName}/issues"
                    && req.Headers.Authorization != null && req.Headers.Authorization.Parameter == expectedAuthParam
                 ),
                ItExpr.IsAny<CancellationToken>());

            Assert.NotNull(capturedContent);
            using (var doc = JsonDocument.Parse(capturedContent))
            {
                Assert.True(doc.RootElement.TryGetProperty("title", out var titleProp) && titleProp.GetString() == request.Title);
                Assert.False(doc.RootElement.TryGetProperty("content", out _));
                Assert.Equal(1, doc.RootElement.EnumerateObject().Count());
            }
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.NotFound)]
        [InlineData(HttpStatusCode.InternalServerError)]
        public async Task AddIssueAsync_ApiReturnsError_ThrowsHttpRequestException(HttpStatusCode statusCode)
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Error" };
            var errorJson = @"{""type"": ""error"", ""error"": {""message"": ""Something went wrong""}}";
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);
            string? capturedContent;
            SetupMockHttpResponseForBitbucket(statusCode, errorJson, out capturedContent);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _bitbucketService.AddIssueAsync(_validRepoInfo, request)
            );
            Assert.Equal(statusCode, exception.StatusCode);
            if (statusCode == HttpStatusCode.Unauthorized)
                Assert.Contains("authentication failed", exception.Message);
            else if (statusCode == HttpStatusCode.NotFound)
                Assert.Contains("repository", exception.Message);
            Assert.Contains(errorJson, exception.Message);
        }

        [Fact]
        public async Task AddIssueAsync_InvalidJsonResponse_ThrowsJsonException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Invalid Json" };
            var invalidJsonResponse = @"{""id"": 123";
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);
            string? capturedContent;
            SetupMockHttpResponseForBitbucket(HttpStatusCode.Created, invalidJsonResponse, out capturedContent);

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(
                () => _bitbucketService.AddIssueAsync(_validRepoInfo, request)
            );
        }

        [Fact]
        public async Task AddIssueAsync_MissingCredentials_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Missing Creds" };
            SetupMockBitbucketCredentials(null, ValidBitbucketAppPassword);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bitbucketService.AddIssueAsync(_validRepoInfo, request)
            );
            Assert.Contains("username or App Password is missing", exception.Message);
        }

        [Fact]
        public async Task AddIssueAsync_NullTitle_ThrowsArgumentNullException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = null };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _bitbucketService.AddIssueAsync(_validRepoInfo, request)
            );
        }

        [Fact]
        public async Task AddIssueAsync_NetworkError_ThrowsHttpRequestException()
        {
            // Arrange
            var request = new CreateIssueRequest { Title = "Test Network Error" };
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);
            SetupMockHttpExceptionForBitbucket(new HttpRequestException("Simulated network error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                 () => _bitbucketService.AddIssueAsync(_validRepoInfo, request)
            );
        }

        #endregion

        #region Tests UpdateIssueAsync

        [Fact]
        public async Task UpdateIssueAsync_Success_UpdatesTitleAndDesc_SendsCorrectPayload_ReturnsMappedIssue()
        {
            // Arrange
            var issueId = "54321";
            var request = new UpdateIssueRequest { Title = "Updated BB Title", Description = "Updated BB Desc" };
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);
            var fakeJsonResponse = $@"{{
            ""id"": {issueId},
            ""title"": ""{request.Title}"",
            ""content"": {{""raw"": ""{request.Description}""}},
            ""state"": ""open"",
            ""links"": {{""html"": {{""href"": ""https://bitbucket.org/test-workspace/test-slug/issues/{issueId}""}}}}
        }}";
            var expectedAuthParam = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidBitbucketUsername}:{ValidBitbucketAppPassword}"));
            string? capturedContent = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => { if (req.Content != null) { capturedContent = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult(); } })
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(fakeJsonResponse) });

            // Act
            var result = await _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(issueId, result.Id);
            Assert.Equal(issueId, result.DisplayId);
            Assert.Equal(request.Title, result.Title);
            Assert.Equal(request.Description, result.Description);
            Assert.Equal("open", result.State);
            Assert.Equal($"https://bitbucket.org/test-workspace/test-slug/issues/{issueId}", result.Url);
            Assert.Equal(ProviderType.Bitbucket, result.Provider);
            Assert.Equal(_validRepoInfo.RepositoryName, result.RepositoryName);
            Assert.Equal(_validRepoInfo.Owner, result.Owner);

            _mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put
                    && req.RequestUri.AbsoluteUri == $"https://api.bitbucket.org/2.0/repositories/{_validRepoInfo.Owner}/{_validRepoInfo.RepositoryName}/issues/{issueId}"
                    && req.Headers.Authorization != null
                    && req.Headers.Authorization.Scheme == "Basic"
                    && req.Headers.Authorization.Parameter == expectedAuthParam
               ),
               ItExpr.IsAny<CancellationToken>());

            Assert.NotNull(capturedContent);
            using (var doc = JsonDocument.Parse(capturedContent))
            {
                Assert.True(doc.RootElement.TryGetProperty("title", out var titleProp) && titleProp.GetString() == request.Title);
                Assert.True(doc.RootElement.TryGetProperty("content", out var contentProp) && contentProp.TryGetProperty("raw", out var rawProp) && rawProp.GetString() == request.Description);
                Assert.Equal(2, doc.RootElement.EnumerateObject().Count());
            }
        }

        [Fact]
        public async Task UpdateIssueAsync_Success_UpdatesOnlyTitle_SendsCorrectPayload()
        {
            // Arrange
            var issueId = "54322";
            var request = new UpdateIssueRequest { Title = "Updated BB Title Only", Description = null };
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);
            var fakeJsonResponse = $@"{{ ""id"": {issueId}, ""title"": ""{request.Title}"", ""content"": {{""raw"": ""Old Desc""}}, ""state"": ""open"", ""links"": {{""html"": {{""href"": ""url""}}}} }}";
            var expectedAuthParam = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidBitbucketUsername}:{ValidBitbucketAppPassword}"));
            string? capturedContent = null;

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .Callback<HttpRequestMessage, CancellationToken>((req, ct) => { if (req.Content != null) { capturedContent = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult(); } })
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(fakeJsonResponse) });

            // Act
            var result = await _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(request.Title, result.Title);
            Assert.Equal("Old Desc", result.Description);

            _mockHttpMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put
                    && req.RequestUri.AbsoluteUri == $"https://api.bitbucket.org/2.0/repositories/{_validRepoInfo.Owner}/{_validRepoInfo.RepositoryName}/issues/{issueId}"
                    && req.Headers.Authorization != null && req.Headers.Authorization.Parameter == expectedAuthParam
               ),
               ItExpr.IsAny<CancellationToken>());

            Assert.NotNull(capturedContent);
            using (var doc = JsonDocument.Parse(capturedContent))
            {
                Assert.True(doc.RootElement.TryGetProperty("title", out var titleProp) && titleProp.GetString() == request.Title);
                Assert.False(doc.RootElement.TryGetProperty("content", out _));
                Assert.Equal(1, doc.RootElement.EnumerateObject().Count());
            }
        }

        [Fact]
        public async Task UpdateIssueAsync_Success_UpdatesOnlyDescription_SendsCorrectPayload()
        {
            // Arrange
            var issueId = "54323";
            var request = new UpdateIssueRequest { Title = null, Description = "Updated BB Desc Only" };
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);
            var fakeJsonResponse = $@"{{ ""id"": {issueId}, ""title"": ""Old Title"", ""content"": {{""raw"": ""{request.Description}""}}, ""state"": ""open"", ""links"": {{""html"": {{""href"": ""url""}}}} }}";
            var expectedAuthParam = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ValidBitbucketUsername}:{ValidBitbucketAppPassword}"));
            string? capturedContent = null;

            _mockHttpMessageHandler.Protected()
               .Setup<Task<HttpResponseMessage>>(
                   "SendAsync",
                   ItExpr.IsAny<HttpRequestMessage>(),
                   ItExpr.IsAny<CancellationToken>()
               )
               .Callback<HttpRequestMessage, CancellationToken>((req, ct) => { if (req.Content != null) { capturedContent = req.Content.ReadAsStringAsync(ct).GetAwaiter().GetResult(); } })
               .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(fakeJsonResponse) });

            // Act
            var result = await _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("Old Title", result.Title);
            Assert.Equal(request.Description, result.Description);

            _mockHttpMessageHandler.Protected().Verify("SendAsync", Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put
                    && req.RequestUri.AbsoluteUri == $"https://api.bitbucket.org/2.0/repositories/{_validRepoInfo.Owner}/{_validRepoInfo.RepositoryName}/issues/{issueId}"
                    && req.Headers.Authorization != null && req.Headers.Authorization.Parameter == expectedAuthParam
               ),
               ItExpr.IsAny<CancellationToken>());

            Assert.NotNull(capturedContent);
            using (var doc = JsonDocument.Parse(capturedContent))
            {
                Assert.False(doc.RootElement.TryGetProperty("title", out _));
                Assert.True(doc.RootElement.TryGetProperty("content", out var contentProp) && contentProp.TryGetProperty("raw", out var rawProp) && rawProp.GetString() == request.Description);
                Assert.Equal(1, doc.RootElement.EnumerateObject().Count());
            }
        }

        [Fact]
        public async Task UpdateIssueAsync_NotFound_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "non-existent";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            var expectedStatusCode = HttpStatusCode.NotFound;
            var errorJson = @"{""error"":{""message"":""Issue not found""}}";
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = expectedStatusCode, Content = new StringContent(errorJson) });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            Assert.Contains($"issue '{issueId}' or repository", exception.Message);
        }

        [Fact]
        public async Task UpdateIssueAsync_Unauthorized_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "54324";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            var expectedStatusCode = HttpStatusCode.Unauthorized;
            var errorJson = @"{""error"":{""message"":""Auth failed""}}";
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);

            _mockHttpMessageHandler.Protected()
               .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
               .ReturnsAsync(new HttpResponseMessage { StatusCode = expectedStatusCode, Content = new StringContent(errorJson) });

            // Act & Assert
            var exception = await Assert.ThrowsAsync<HttpRequestException>(
                () => _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
            Assert.Equal(expectedStatusCode, exception.StatusCode);
            Assert.Contains("authentication/authorization failed", exception.Message);
        }

        [Fact]
        public async Task UpdateIssueAsync_InvalidJsonResponse_ThrowsJsonException()
        {
            // Arrange
            var issueId = "54325";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            var invalidJsonResponse = @"{""id"": 123";
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);

            _mockHttpMessageHandler.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(invalidJsonResponse) });

            // Act & Assert
            await Assert.ThrowsAsync<JsonException>(
                () => _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
        }

        [Fact]
        public async Task UpdateIssueAsync_MissingCredentials_ThrowsInvalidOperationException()
        {
            // Arrange
            var issueId = "54326";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            SetupMockBitbucketCredentials(ValidBitbucketUsername, null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
        }

        [Fact]
        public async Task UpdateIssueAsync_NoFieldsToUpdate_ThrowsArgumentException()
        {
            // Arrange
            var issueId = "54327";
            var request = new UpdateIssueRequest { Title = null, Description = null };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
        }

        [Fact]
        public async Task UpdateIssueAsync_NullIssueId_ThrowsArgumentNullException()
        {
            // Arrange
            var request = new UpdateIssueRequest { Title = "Update Title" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                 () => _bitbucketService.UpdateIssueAsync(_validRepoInfo, null!, request));
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
                () => _bitbucketService.UpdateIssueAsync(_validRepoInfo, invalidIssueId, request));
        }

        [Fact]
        public async Task UpdateIssueAsync_NetworkError_ThrowsHttpRequestException()
        {
            // Arrange
            var issueId = "54328";
            var request = new UpdateIssueRequest { Title = "Update Title" };
            SetupMockBitbucketCredentials(ValidBitbucketUsername, ValidBitbucketAppPassword);
            SetupMockHttpExceptionForBitbucket(new HttpRequestException("Simulated network error"));

            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(
                 () => _bitbucketService.UpdateIssueAsync(_validRepoInfo, issueId, request)
            );
        }

        #endregion

    }
}
