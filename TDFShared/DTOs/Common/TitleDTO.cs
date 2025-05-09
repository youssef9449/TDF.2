namespace TDFShared.DTOs.Common
{
    // Represents a Job Title, often used in lookups or user profiles.
    public class TitleDTO
    {
        // Assuming an ID and Name structure, adjust if different
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}