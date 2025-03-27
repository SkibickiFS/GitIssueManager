using Application.Enums;

namespace Application.Models
{
    /// <summary>
    /// Represents the details of an issue retrieved from a Git hosting service.
    /// Provides a common structure for issues from different providers.
    /// </summary>
    public class IssueDetailsResponse
    {
        /// <summary>
        /// The unique identifier of the issue within the provider's system (node_id or issue UUID).
        /// </summary>
        public required string Id { get; set; }

        /// <summary>
        /// A user-friendly identifier, often sequential (issue number). May be the same as Id for some providers.
        /// </summary>
        public string? DisplayId { get; set; }

        /// <summary>
        /// The current title of the issue.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// The description or body of the issue.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// The current state of the issue (e.g., "open"). Provider-specific states may need mapping.
        /// </summary>
        public required string State { get; set; }

        /// <summary>
        /// The URL to view the issue on the provider's website.
        /// </summary>
        public string? Url { get; set; }

        /// <summary>
        /// The provider from which this issue originates.
        /// </summary>
        public ProviderType Provider { get; set; }

        /// <summary>
        /// The name of the repository containing the issue.
        /// </summary>
        public required string RepositoryName { get; set; }

        /// <summary>
        /// The owner or workspace of the repository containing the issue.
        /// </summary>
        public required string Owner { get; set; }
    }
}
