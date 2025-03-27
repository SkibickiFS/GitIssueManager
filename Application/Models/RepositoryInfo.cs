using Application.Enums;

namespace Application.Models
{
    /// <summary>
    /// Contains information identifying a specific repository on a Git hosting service.
    /// </summary>
    public class RepositoryInfo
    {
        /// <summary>
        /// The type of the Git hosting service provider.
        /// </summary>
        public ProviderType Provider { get; init; }

        /// <summary>
        /// The owner of the repository (organization, user or workspace).
        /// </summary>
        public required string Owner { get; init; }

        /// <summary>
        /// The name of the repository (repository name or slug).
        /// </summary>
        public required string RepositoryName { get; init; }
    }
}
