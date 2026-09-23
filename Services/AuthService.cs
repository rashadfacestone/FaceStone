using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using FaceStone.Web.Data;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Services;

public class AuthService : IAuthService
{
    private readonly FaceStoneDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        FaceStoneDbContext context, 
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    public IdentifierType ResolveIdentifierType(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier))
            return IdentifierType.Unknown;

        var trimmed = identifier.Trim();

        // 1. Email format check
        if (trimmed.Contains('@') && Regex.IsMatch(trimmed, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            return IdentifierType.Email;
        }

        // 2. Phone format check
        var digitsOnly = Regex.Replace(trimmed, @"\D", "");
        if ((trimmed.StartsWith('+') || digitsOnly.Length >= 7) && !Regex.IsMatch(trimmed, @"[a-zA-Z]"))
        {
            return IdentifierType.Phone;
        }

        // 3. Username fallback
        if (Regex.IsMatch(trimmed, @"^[a-zA-Z0-9._-]+$"))
        {
            return IdentifierType.Username;
        }

        return IdentifierType.Unknown;
    }

    public async Task<(bool Success, string? ErrorMessage, UserAccount? User)> AuthenticateAsync(string identifier, string password)
    {
        if (string.IsNullOrWhiteSpace(identifier) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "Please enter both your identifier and password.", null);
        }

        var trimmedId = identifier.Trim();
        var idType = ResolveIdentifierType(trimmedId);
        var inputHash = HashPassword(password);

        UserAccount? foundUser = null;

        try
        {
            if (idType == IdentifierType.Email)
            {
                foundUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == trimmedId.ToLower());
            }
            else if (idType == IdentifierType.Phone)
            {
                var inputDigits = Regex.Replace(trimmedId, @"\D", "");
                foundUser = await _context.Users.FirstOrDefaultAsync(u => u.PhoneNumber.Contains(inputDigits) || inputDigits.Contains(u.PhoneNumber));
            }
            else if (idType == IdentifierType.Username)
            {
                foundUser = await _context.Users
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == trimmedId.ToLower());
            }

            // Fallback: search across Email, Username, or Phone
            if (foundUser == null)
            {
                foundUser = await _context.Users
                    .FirstOrDefaultAsync(u =>
                        u.Email.ToLower() == trimmedId.ToLower() ||
                        u.Username.ToLower() == trimmedId.ToLower() ||
                        u.PhoneNumber == trimmedId);
            }

            if (foundUser == null)
            {
                return (false, "No account found matching this email, phone, or username in the database.", null);
            }

            // Strict password check - must match the database hash
            if (foundUser.PasswordHash != inputHash)
            {
                return (false, "Incorrect password. Please verify your credentials and try again.", null);
            }

            return (true, null, foundUser);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database authentication error for identifier: {Identifier}", identifier);
            return (false, $"Database error: {ex.Message}", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, AuthResponseDto? AuthResponse)> AuthenticateWithJwtAsync(LoginRequestDto model)
    {
        var (success, error, user) = await AuthenticateAsync(model.Identifier, model.Password);
        if (!success || user == null)
        {
            return (false, error, null);
        }

        var token = GenerateJwtToken(user, out var expiresAt);
        var response = new AuthResponseDto
        {
            Token = token,
            TokenType = "Bearer",
            ExpiresAt = expiresAt,
            User = MapToUserDto(user)
        };

        return (true, null, response);
    }

    public async Task<(bool Success, string? ErrorMessage, UserAccount? User)> RegisterAsync(SignUpViewModel model)
    {
        var regDto = new RegisterRequestDto
        {
            FullName = model.FullName,
            Username = model.Username,
            Email = model.Email,
            CountryCode = model.CountryCode,
            PhoneNumber = model.PhoneNumber,
            Password = model.Password,
            Role = model.Role
        };

        var (success, error, authResp) = await RegisterWithJwtAsync(regDto);
        if (!success || authResp == null)
        {
            return (false, error, null);
        }

        var user = await _context.Users.FindAsync(authResp.User.Id);
        return (true, null, user);
    }

    public async Task<(bool Success, string? ErrorMessage, AuthResponseDto? AuthResponse)> RegisterWithJwtAsync(RegisterRequestDto model)
    {
        try
        {
            // 1. Check if Email already exists in SQL Server
            var emailClean = model.Email.Trim().ToLower();
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == emailClean);

            if (emailExists)
            {
                return (false, "An account with this email address already exists in the database.", null);
            }

            // 2. Check if Username already exists in SQL Server
            var usernameClean = model.Username.Trim().ToLower();
            var usernameExists = await _context.Users
                .AnyAsync(u => u.Username.ToLower() == usernameClean);

            if (usernameExists)
            {
                return (false, "This username is already taken in the database. Please choose another.", null);
            }

            // 3. Check if Phone Number already exists in SQL Server
            var cleanPhone = Regex.Replace(model.PhoneNumber.Trim(), @"\D", "");
            var fullPhone = $"{model.CountryCode}{model.PhoneNumber.Trim()}";

            var phoneExists = await _context.Users
                .AnyAsync(u => u.PhoneNumber == fullPhone || u.PhoneNumber == model.PhoneNumber.Trim());

            if (!phoneExists && cleanPhone.Length >= 7)
            {
                var allPhones = await _context.Users.Select(u => u.PhoneNumber).ToListAsync();
                phoneExists = allPhones.Any(p =>
                {
                    var cp = Regex.Replace(p, @"\D", "");
                    return cp.EndsWith(cleanPhone) || cleanPhone.EndsWith(cp);
                });
            }

            if (phoneExists)
            {
                return (false, "An account with this phone number already exists in the database.", null);
            }

            var validRole = model.Role switch
            {
                "CEO" => "CEO",
                "IT" => "IT",
                "Accountant" => "Accountant",
                "Employee" => "Employee",
                _ => "Employee"
            };

            // 4. Create and persist user to SQL Server
            var newUser = new UserAccount
            {
                FullName = model.FullName.Trim(),
                Username = model.Username.Trim(),
                Email = model.Email.Trim(),
                CountryCode = model.CountryCode,
                PhoneNumber = fullPhone,
                PasswordHash = HashPassword(model.Password),
                CreatedAt = DateTime.UtcNow,
                Role = validRole
            };

            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();

            _logger.LogInformation("New user {Username} saved successfully to SQL Server database.", newUser.Username);

            var token = GenerateJwtToken(newUser, out var expiresAt);
            var response = new AuthResponseDto
            {
                Token = token,
                TokenType = "Bearer",
                ExpiresAt = expiresAt,
                User = MapToUserDto(newUser)
            };

            return (true, null, response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering user to SQL Server database");
            return (false, $"Database registration error: {ex.Message}", null);
        }
    }

    public async Task<bool> RequestPasswordResetAsync(string recoveryIdentifier)
    {
        if (string.IsNullOrWhiteSpace(recoveryIdentifier))
            return false;

        var trimmed = recoveryIdentifier.Trim().ToLower();
        var exists = await _context.Users.AnyAsync(u =>
            u.Email.ToLower() == trimmed ||
            u.PhoneNumber.Contains(trimmed) ||
            u.Username.ToLower() == trimmed);

        _logger.LogInformation("Password reset requested for {Identifier}. Account exists in DB: {Exists}", recoveryIdentifier, exists);
        return true;
    }

    public string GenerateJwtToken(UserAccount user, out DateTime expiresAt)
    {
        var secretKey = _configuration["Jwt:Key"] 
            ?? "FaceStone_Secret_Key_For_Jwt_Authentication_2026_Concrete_Solutions_SuperSecureKey!";
        var issuer = _configuration["Jwt:Issuer"] ?? "FaceStoneApi";
        var audience = _configuration["Jwt:Audience"] ?? "FaceStoneClients";
        var expireHours = int.TryParse(_configuration["Jwt:ExpireHours"], out var hours) ? hours : 24;

        expiresAt = DateTime.UtcNow.AddHours(expireHours);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.FullName),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("username", user.Username),
            new Claim("phone", user.PhoneNumber)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = issuer,
            Audience = audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public UserDto MapToUserDto(UserAccount user)
    {
        return new UserDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Username = user.Username,
            Email = user.Email,
            CountryCode = user.CountryCode,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "_FaceStoneSalt_2026"));
        return Convert.ToBase64String(bytes);
    }
}
