namespace Application.Models
{
    /// <summary>
    /// Represents the data required to update an existing issue.
    /// Null values indicate that the corresponding field should not be updated.
    /// </summary>
    public class UpdateIssueRequest
    {
        /// <summary>
        /// The new title for the issue. If null, the title is not updated.
        /// </summary>
        public string? Title { get; set; }

        /// <summary>
        /// The new description for the issue. If null, the description is not updated.
        /// </summary>
        public string? Description { get; set; }
    }
}
