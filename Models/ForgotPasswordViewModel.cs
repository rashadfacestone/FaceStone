using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "Please provide your email address or mobile phone number.")]
    [Display(Name = "Email or Phone")]
    public string RecoveryIdentifier { get; set; } = string.Empty;
}
