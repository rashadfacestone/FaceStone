using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models;

public class SignUpViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full Name must be between 2 and 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Username is required.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]{3,30}$", ErrorMessage = "Username must be 3-30 characters (alphanumeric, dot, underscore, or hyphen).")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email Address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Country Code")]
    public string CountryCode { get; set; } = "+1";

    [Required(ErrorMessage = "Phone number is required.")]
    [RegularExpression(@"^[\d\s\-\(\)]{6,20}$", ErrorMessage = "Please enter a valid phone number.")]
    [Display(Name = "Mobile Phone")]
    public string PhoneNumber { get; set; } = string.Empty;
    [Display(Name = "Company Role")]
    public string Role { get; set; } = "Employee";

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Range(typeof(bool), "true", "true", ErrorMessage = "You must agree to the Terms of Service & Privacy Policy.")]
    [Display(Name = "Accept Terms")]
    public bool AgreeTerms { get; set; }
}
