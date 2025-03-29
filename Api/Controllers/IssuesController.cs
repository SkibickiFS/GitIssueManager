using Microsoft.AspNetCore.Mvc;
using Application.Interfaces; 
using Application.Models;
using Application.Enums;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Api.Controllers
{
    /// <summary>
    /// API Controller for managing issues on different Git hosting services.
    /// </summary>
    [ApiController]
    [Route("api/{provider}/{owner}/{repo}/[controller]")]
    public class IssuesController : ControllerBase
    {
        private readonly IGitIssueServiceFactory _serviceFactory;
        private readonly ILogger<IssuesController> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="IssuesController"/> class.
        /// </summary>
        /// <param name="serviceFactory">The factory to get Git service implementations.</param>
        /// <param name="logger">The logger instance.</param>
        public IssuesController(IGitIssueServiceFactory serviceFactory, ILogger<IssuesController> logger)
        {
            _serviceFactory = serviceFactory ?? throw new ArgumentNullException(nameof(serviceFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Creates a new issue.
        /// </summary>
        /// <param name="provider">Git provider (e.g., 'github', 'bitbucket').</param>
        /// <param name="owner">Repository owner or workspace.</param>
        /// <param name="repo">Repository name or slug.</param>
        /// <param name="request">Issue details (title, description).</param>
        /// <returns>Action result with details of the created issue.</returns>
        [HttpPost] // Map to POST /api/{provider}/{owner}/{repo}/issues
        [ProducesResponseType(typeof(IssueDetailsResponse), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> AddIssue(
            [FromRoute] string provider,
            [FromRoute] string owner,
            [FromRoute] string repo,
            [FromBody] CreateIssueRequest request)
        {
            _logger.LogInformation("Attempting to add issue for {Provider}/{Owner}/{Repo}", provider, owner, repo);

            if (!TryParseProviderType(provider, out var providerType))
            {
                return BadRequest($"Unsupported provider: {provider}. Supported providers are 'github', 'bitbucket'.");
            }


            await Task.Delay(10);
                                  //Temp result TODO: #11
            return Ok(new { Message = "AddIssue endpoint hit - Not Implemented Yet", provider, owner, repo, request });
        }

        /// <summary>
        /// Updates an existing issue (title and/or description).
        /// </summary>
        /// <param name="provider">Git provider.</param>
        /// <param name="owner">Repository owner or workspace.</param>
        /// <param name="repo">Repository name or slug.</param>
        /// <param name="issueId">The ID or number of the issue to update.</param>
        /// <param name="request">Fields to update (title, description).</param>
        /// <returns>Action result with details of the updated issue.</returns>
        [HttpPatch("{issueId}")] // Map to PATCH /api/{provider}/{owner}/{repo}/issues/{issueId}
        [ProducesResponseType(typeof(IssueDetailsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UpdateIssue(
            [FromRoute] string provider,
            [FromRoute] string owner,
            [FromRoute] string repo,
            [FromRoute] string issueId,
            [FromBody] UpdateIssueRequest request)
        {
            _logger.LogInformation("Attempting to update issue {IssueId} for {Provider}/{Owner}/{Repo}", issueId, provider, owner, repo);

            if (!TryParseProviderType(provider, out var providerType))
            {
                return BadRequest($"Unsupported provider: {provider}. Supported providers are 'github', 'bitbucket'.");
            }

            // TODO #12

            await Task.Delay(10);
            return Ok(new { Message = "UpdateIssue endpoint hit - Not Implemented Yet", provider, owner, repo, issueId, request });
        }

        /// <summary>
        /// Closes an existing issue.
        /// </summary>
        /// <param name="provider">Git provider.</param>
        /// <param name="owner">Repository owner or workspace.</param>
        /// <param name="repo">Repository name or slug.</param>
        /// <param name="issueId">The ID or number of the issue to close.</param>
        /// <returns>Action result with details of the closed issue.</returns>
        [HttpPost("{issueId}/close")] // Map to POST /api/{provider}/{owner}/{repo}/issues/{issueId}/close
        [ProducesResponseType(typeof(IssueDetailsResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> CloseIssue(
            [FromRoute] string provider,
            [FromRoute] string owner,
            [FromRoute] string repo,
            [FromRoute] string issueId)
        {
            _logger.LogInformation("Attempting to close issue {IssueId} for {Provider}/{Owner}/{Repo}", issueId, provider, owner, repo);

            if (!TryParseProviderType(provider, out var providerType))
            {
                return BadRequest($"Unsupported provider: {provider}. Supported providers are 'github', 'bitbucket'.");
            }

            // TODO #12

            await Task.Delay(10);
            return Ok(new { Message = "CloseIssue endpoint hit - Not Implemented Yet", provider, owner, repo, issueId });
        }

        #region private helper methods

        /// <summary>
        /// Tries to parse the provider string into a ProviderType enum.
        /// </summary>
        private bool TryParseProviderType(string provider, out ProviderType providerType)
        {
            if (Enum.TryParse<ProviderType>(provider, ignoreCase: true, out var type) &&
                Enum.IsDefined(typeof(ProviderType), type))
            {
                providerType = type;
                return true;
            }

            providerType = default;
            return false;
        }

        #endregion
    }
}
