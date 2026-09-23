using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Services;

public interface IAuthService
{
    IdentifierType ResolveIdentifierType(string identifier);
    Task<(bool Success, string? ErrorMessage, UserAccount? User)> AuthenticateAsync(string identifier, string password);
    Task<(bool Success, string? ErrorMessage, AuthResponseDto? AuthResponse)> AuthenticateWithJwtAsync(LoginRequestDto model);
    Task<(bool Success, string? ErrorMessage, UserAccount? User)> RegisterAsync(SignUpViewModel model);
    Task<(bool Success, string? ErrorMessage, AuthResponseDto? AuthResponse)> RegisterWithJwtAsync(RegisterRequestDto model);
    Task<bool> RequestPasswordResetAsync(string recoveryIdentifier);
    string GenerateJwtToken(UserAccount user, out DateTime expiresAt);
    UserDto MapToUserDto(UserAccount user);
}
