namespace Application.Models
{
    /// <summary>
    /// Represents the data required to create a new issue.
    /// </summary>
    public class CreateIssueRequest
    {
        /// <summary>
        /// The title of the issue. Required.
        /// </summary>
        public required string Title { get; set; }

        /// <summary>
        /// The description or body of the issue. Optional.
        /// </summary>
        public string? Description { get; set; }
    }
}
