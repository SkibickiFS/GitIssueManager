using Application.Models;

namespace Application.Interfaces
{
    /// <summary>
    /// Defines the contract for managing issues on Git hosting services.
    /// Implementations will handle communication with specific provider APIs.
    /// </summary>
    public interface IGitIssueService
    {
        /// <summary>
        /// Creates a new issue in the specified repository.
        /// </summary>
        /// <param name="repoInfo">Information identifying the target repository.</param>
        /// <param name="request">Data for the new issue (title, description).</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>Details of the newly created issue.</returns>
        /// <exception cref="System.Exception">Throws exceptions on communication errors or API failures.</exception>
        Task<IssueDetailsResponse> AddIssueAsync(
            RepositoryInfo repoInfo,
            CreateIssueRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates an existing issue in the specified repository.
        /// </summary>
        /// <param name="repoInfo">Information identifying the target repository.</param>
        /// <param name="issueId">The unique identifier of the issue to update.</param>
        /// <param name="request">Data containing the fields to update (title, description). Null fields are ignored.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>Details of the updated issue.</returns>
        /// <exception cref="System.Exception">Throws exceptions on communication errors, API failures, or if the issue is not found.</exception>
        Task<IssueDetailsResponse> UpdateIssueAsync(
            RepositoryInfo repoInfo,
            string issueId,
            UpdateIssueRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Closes an existing issue in the specified repository.
        /// </summary>
        /// <param name="repoInfo">Information identifying the target repository.</param>
        /// <param name="issueId">The unique identifier of the issue to close.</param>
        /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
        /// <returns>Details of the closed issue.</returns> 
        /// <exception cref="System.Exception">Throws exceptions on communication errors, API failures, or if the issue is not found.</exception>
        Task<IssueDetailsResponse> CloseIssueAsync(
            RepositoryInfo repoInfo,
            string issueId,
            CancellationToken cancellationToken = default);
    }
}
