using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Data;
using FaceStone.Web.Models.DTOs;
using FaceStone.Web.Services;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly FaceStoneDbContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        FaceStoneDbContext context,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Authenticate a user with email, phone, or username and receive a JWT Bearer token.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var (success, error, authResponse) = await _authService.AuthenticateWithJwtAsync(request);
        if (!success || authResponse == null)
        {
            return Unauthorized(ApiResponse.Fail(error ?? "Invalid credentials. Please verify your identifier and password."));
        }

        _logger.LogInformation("User {Username} ({Role}) authenticated successfully.", authResponse.User.Username, authResponse.User.Role);
        return Ok(ApiResponse<AuthResponseDto>.Ok(authResponse, $"Welcome back, {authResponse.User.FullName}!"));
    }

    /// <summary>
    /// Register a new user account in SQL Server and immediately receive a JWT Bearer token.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse.Fail("Validation failed.", errors));
        }

        var (success, error, authResponse) = await _authService.RegisterWithJwtAsync(request);
        if (!success || authResponse == null)
        {
            return BadRequest(ApiResponse.Fail(error ?? "Registration failed. Please review your input."));
        }

        _logger.LogInformation("New user {Username} ({Role}) registered successfully.", authResponse.User.Username, authResponse.User.Role);
        return StatusCode(StatusCodes.Status201Created, 
            ApiResponse<AuthResponseDto>.Ok(authResponse, $"Account created successfully! Welcome, {authResponse.User.FullName}."));
    }

    /// <summary>
    /// Request password reset instructions by identifier (email, phone, or username).
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse.Fail("Invalid recovery identifier."));
        }

        await _authService.RequestPasswordResetAsync(request.RecoveryIdentifier);
        return Ok(ApiResponse.Ok($"Password recovery instructions dispatched to {request.RecoveryIdentifier}."));
    }

    /// <summary>
    /// Smart detection of identifier type (email, phone, or username).
    /// </summary>
    [HttpPost("detect-identifier")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<DetectIdentifierResponseDto>), StatusCodes.Status200OK)]
    public IActionResult DetectIdentifier([FromBody] DetectIdentifierRequestDto request)
    {
        var type = _authService.ResolveIdentifierType(request?.Identifier ?? string.Empty);
        return Ok(ApiResponse<DetectIdentifierResponseDto>.Ok(new DetectIdentifierResponseDto
        {
            Type = type.ToString().ToLowerInvariant()
        }));
    }

    /// <summary>
    /// Get authenticated caller's profile.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(ApiResponse.Fail("Unauthorized caller token."));
        }

        var user = await _context.Users.FindAsync(userId);
        if (user == null)
        {
            return NotFound(ApiResponse.Fail("User account not found."));
        }

        return Ok(ApiResponse<UserDto>.Ok(_authService.MapToUserDto(user)));
    }
}
