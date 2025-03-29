using Application.Enums;

namespace Application.Interfaces
{
    /// <summary>
    /// Defines a factory for obtaining instances of IGitIssueService
    /// based on the specified provider type.
    /// </summary>
    public interface IGitIssueServiceFactory
    {
        /// <summary>
        /// Gets the appropriate IGitIssueService implementation for the given provider.
        /// </summary>
        /// <param name="providerType">The type of the Git hosting service provider.</param>
        /// <returns>An instance of IGitIssueService specific to the provider.</returns>
        /// <exception cref="System.NotSupportedException">Thrown if the provider type is not supported.</exception>
        IGitIssueService GetService(ProviderType providerType);
    }
}
