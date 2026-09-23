using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models;

public enum IdentifierType
{
    Unknown,
    Email,
    Phone,
    Username
}

public class LoginViewModel
{
    [Required(ErrorMessage = "Please enter your email, phone, or username.")]
    [Display(Name = "Account Identifier")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember this device")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }

    public IdentifierType DetectedType { get; set; } = IdentifierType.Unknown;
}
