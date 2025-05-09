using System.ComponentModel.DataAnnotations;

namespace TDFShared.DTOs.Auth
{
    public class RegisterRequestDto
    {

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [Compare("Password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required]
        public string FullName { get; set; } = string.Empty;

        [Required]
        public string Department { get; set; } = string.Empty; // Or DepartmentId if using IDs

        [Required]
        public string Title { get; set; } = string.Empty; // Or TitleId if using IDs

        public string Username { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool IsManager { get; set; }
        public bool isHR { get; set; }
    }
}