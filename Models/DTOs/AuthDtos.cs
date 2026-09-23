using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models.DTOs;

public class LoginRequestDto
{
    [Required(ErrorMessage = "يرجى إدخال البريد الإلكتروني أو رقم الهاتف أو اسم المستخدم.")]
    public string Identifier { get; set; } = string.Empty;

    [Required(ErrorMessage = "يرجى إدخال كلمة المرور.")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequestDto
{
    [Required(ErrorMessage = "الاسم الكامل مطلوب.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "يجب أن يتراوح الاسم بين 3 و 150 حرفاً.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم المستخدم مطلوب.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "يجب أن يتراوح اسم المستخدم بين 3 و 50 حرفاً.")]
    [RegularExpression(@"^[a-zA-Z0-9._-]+$", ErrorMessage = "اسم المستخدم يجب أن يحتوي على أحرف وأرقام ورموز (. _ -) فقط.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "البريد الإلكتروني مطلوب.")]
    [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة.")]
    [StringLength(150)]
    public string Email { get; set; } = string.Empty;

    public string CountryCode { get; set; } = "+20";

    [Required(ErrorMessage = "رقم الهاتف مطلوب.")]
    [Phone(ErrorMessage = "صيغة رقم الهاتف غير صحيحة.")]
    [StringLength(30)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "كلمة المرور مطلوبة.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "يجب ألا تقل كلمة المرور عن 6 أحرف.")]
    public string Password { get; set; } = string.Empty;

    public string Role { get; set; } = "Employee";
}

public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "يرجى إدخال البريد الإلكتروني أو رقم الهاتف لاستعادة كلمة المرور.")]
    public string RecoveryIdentifier { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class DetectIdentifierRequestDto
{
    [Required]
    public string Identifier { get; set; } = string.Empty;
}

public class DetectIdentifierResponseDto
{
    public string Type { get; set; } = string.Empty;
}
