using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Data;
using FaceStone.Web.Models.DTOs;
using FaceStone.Web.Services;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Accountant,CEO,IT")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly FaceStoneDbContext _context;
    private readonly IAuthService _authService;

    public UsersController(FaceStoneDbContext context, IAuthService authService)
    {
        _context = context;
        _authService = authService;
    }

    /// <summary>
    /// Retrieve list of all users, with optional filtering by role or search term.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<UserDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] string? role, [FromQuery] string? search)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(s) ||
                u.Username.ToLower().Contains(s) ||
                u.Email.ToLower().Contains(s) ||
                u.PhoneNumber.Contains(s));
        }

        var users = await query
            .OrderBy(u => u.FullName)
            .ToListAsync();

        var dtos = users.Select(u => _authService.MapToUserDto(u)).ToList();
        return Ok(ApiResponse<List<UserDto>>.Ok(dtos));
    }

    /// <summary>
    /// Retrieve single user details by ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound(ApiResponse.Fail("User not found."));
        }

        return Ok(ApiResponse<UserDto>.Ok(_authService.MapToUserDto(user)));
    }
}
