using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TDFShared.Models.Department
{
    /// <summary>
    /// Represents a department and job title combination in the Departments table
    /// </summary>
    [Table("Departments")]
    public class DepartmentEntity
    {
        // Note: This table doesn't have a primary key in the database
        // We'll use a composite key of Department and Title for EF Core

        /// <summary>
        /// The department name
        /// </summary>
        [Column("Department")]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// The job title within the department
        /// </summary>
        [StringLength(100)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Optional sort order for display purposes (not stored in database)
        /// </summary>
        [NotMapped]
        public int? SortOrder { get; set; }
    }
}
