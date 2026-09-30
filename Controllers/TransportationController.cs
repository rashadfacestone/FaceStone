using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Data;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "TransportationAccess")]
[Produces("application/json")]
public class TransportationController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<TransportationController> _logger;

    public TransportationController(FaceStoneDbContext db, ILogger<TransportationController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Configuration options for Material Types, Destinations, and Statuses.
    /// Can be centrally configured without altering the UI.
    /// </summary>
    public static readonly List<string> MaterialTypes = new()
    {
        "Ready-Mix Concrete (C25/30)",
        "Reinforced Concrete Mix (C35/45)",
        "High-Performance Concrete (HPC)",
        "Portland Cement (CEM I 42.5N)",
        "Aggregate & Gravel (Coarse 20mm)",
        "Fine Sand (Silica / Concrete Sand)",
        "Fly Ash / Silica Fume Slurry",
        "Precast Concrete Elements"
    };

    public static readonly List<string> Destinations = new()
    {
        "Factory Sector A - Batching Plant 1",
        "Factory Sector B - Precast Yard",
        "Factory Sector C - Silos & Aggregate Storage",
        "New Cairo Construction Site - Zone 4",
        "New Administrative Capital - Tower Site A",
        "6th of October Industrial Complex",
        "Ain Sokhna Port Logistics Hub"
    };

    public static readonly List<string> TransactionStatuses = new()
    {
        "Pending",
        "In Transit",
        "Entered Factory",
        "Under Inspection",
        "Completed",
        "Cancelled"
    };

    public static readonly List<string> ReceivingStatuses = new()
    {
        "Pending",
        "Approved / Accepted",
        "Partially Accepted",
        "Rejected",
        "Under Quality Test"
    };

    /// <summary>
    /// Retrieve configurable dropdown options for Transportation Transaction Form.
    /// </summary>
    [HttpGet("config")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TransportationConfigDto>), StatusCodes.Status200OK)]
    public IActionResult GetConfig()
    {
        var config = new TransportationConfigDto
        {
            MaterialTypes = MaterialTypes,
            Destinations = Destinations,
            TransactionStatuses = TransactionStatuses,
            ReceivingStatuses = ReceivingStatuses
        };

        return Ok(ApiResponse<TransportationConfigDto>.Ok(config));
    }

    /// <summary>
    /// Check if a Transaction Number already exists.
    /// </summary>
    [HttpGet("check-number/{transactionNumber:int}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckTransactionNumber(int transactionNumber)
    {
        var exists = await _db.Transportations.AnyAsync(t => t.TransactionNumber == transactionNumber);
        return Ok(ApiResponse<object>.Ok(new { transactionNumber, exists }));
    }

    /// <summary>
    /// Retrieve recent transportation transactions.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponse<List<TransportationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] int limit = 10)
    {
        limit = Math.Clamp(limit, 1, 50);
        var transactions = await _db.Transportations
            .AsNoTracking()
            .OrderByDescending(t => t.DateOfSubmit)
            .Take(limit)
            .Select(t => MapToDto(t))
            .ToListAsync();

        return Ok(ApiResponse<List<TransportationDto>>.Ok(transactions));
    }

    /// <summary>
    /// Retrieve single transportation transaction by ID.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var transaction = await _db.Transportations.FindAsync(id);
        if (transaction == null)
        {
            return NotFound(ApiResponse.Fail($"Transportation transaction #{id} was not found."));
        }

        return Ok(ApiResponse<TransportationDto>.Ok(MapToDto(transaction)));
    }

    /// <summary>
    /// Submit a new Transportation / Factory Transaction record.
    /// Validates required fields, data types, duplicate transaction numbers,
    /// performs weight calculations, and timestamps on the server.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<TransportationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateTransportationDto dto)
    {
        // 1. Explicit Role Authorization Defense in Depth
        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
        if (!AppRoles.HasTransportationAccess(role))
        {
            _logger.LogWarning("Unauthorized user with role '{Role}' attempted to submit transportation transaction.", role);
            return StatusCode(StatusCodes.Status403Forbidden, 
                ApiResponse.Fail("Access Denied: Transportation department is restricted to CEO, IT, and Transportation Manager."));
        }

        // 2. ModelState Model Validation
        if (!ModelState.IsValid)
        {
            var errors = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .ToList();
            return BadRequest(ApiResponse.Fail("Validation failed. Please verify the entered data.", errors));
        }

        // 3. Integer Transaction Number Validation
        if (dto.TransactionNumber <= 0)
        {
            return BadRequest(ApiResponse.Fail("Transaction Number must be a valid positive integer greater than 0."));
        }

        // 4. Duplicate Transaction Number Prevention
        var exists = await _db.Transportations.AnyAsync(t => t.TransactionNumber == dto.TransactionNumber);
        if (exists)
        {
            _logger.LogWarning("Duplicate transaction number '{TxNum}' submitted by role '{Role}'.", dto.TransactionNumber, role);
            return BadRequest(ApiResponse.Fail("Transaction Number already exists."));
        }

        // 5. Positive Numeric Weights Validation
        if (dto.WeightBeforeLoading <= 0)
        {
            return BadRequest(ApiResponse.Fail("Weight Before Loading must be a positive number greater than 0."));
        }
        if (dto.WeightWhenEnteringFactory <= 0)
        {
            return BadRequest(ApiResponse.Fail("Weight When Entering Factory must be a positive number greater than 0."));
        }
        if (dto.WeightWhenExitingFactory <= 0)
        {
            return BadRequest(ApiResponse.Fail("Weight When Exiting Factory must be a positive number greater than 0."));
        }

        // 6. Logical Weight Validations according to business rules
        if (dto.WeightWhenExitingFactory > dto.WeightWhenEnteringFactory)
        {
            return BadRequest(ApiResponse.Fail("Weight When Exiting Factory cannot exceed Weight When Entering Factory."));
        }
        if (dto.WeightWhenExitingFactory > dto.WeightBeforeLoading)
        {
            return BadRequest(ApiResponse.Fail("Weight When Exiting Factory cannot exceed Weight Before Loading."));
        }

        // 7. Automatic Weight Calculations (Formula: Expected = Before Loading - Exit Factory; Net = Enter Factory - Exit Factory)
        var expectedWeight = Math.Round(dto.WeightBeforeLoading - dto.WeightWhenExitingFactory, 3);
        var netWeight = Math.Round(dto.WeightWhenEnteringFactory - dto.WeightWhenExitingFactory, 3);

        // 8. Official Server-Generated Submission Date
        var submissionDate = DateTime.UtcNow;

        // 9. Construct Entity
        var entity = new Transportation
        {
            TransactionNumber = dto.TransactionNumber,
            TransactionDate = dto.TransactionDate!.Value,
            CarNumber = dto.CarNumber.Trim(),
            DriverName = dto.DriverName.Trim(),
            Source = dto.Source.Trim(),
            PONumber = dto.PONumber.Trim(),
            MaterialType = dto.MaterialType.Trim(),
            WeightBeforeLoading = Math.Round(dto.WeightBeforeLoading, 3),
            WeightWhenEnteringFactory = Math.Round(dto.WeightWhenEnteringFactory, 3),
            WeightWhenExitingFactory = Math.Round(dto.WeightWhenExitingFactory, 3),
            ExpectedWeight = expectedWeight,
            NetWeight = netWeight,
            Destination = dto.Destination.Trim(),
            TransactionStatus = dto.TransactionStatus.Trim(),
            ReceivingStatus = dto.ReceivingStatus.Trim(),
            Notes = string.IsNullOrWhiteSpace(dto.Notes) ? null : dto.Notes.Trim(),
            DateOfSubmit = submissionDate
        };

        try
        {
            await _db.Transportations.AddAsync(entity);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Transportation transaction #{TxNum} created successfully by user {User} (Role: {Role}). Id: {Id}",
                entity.TransactionNumber, User.Identity?.Name, role, entity.Id);

            var resultDto = MapToDto(entity);
            return Ok(ApiResponse<TransportationDto>.Ok(resultDto, "Transaction submitted successfully."));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update error while saving transportation transaction #{TxNum}", dto.TransactionNumber);
            // Catch unique constraint violation race conditions
            if (ex.InnerException?.Message.Contains("IX_Transportation_TransactionNumber") == true ||
                ex.InnerException?.Message.Contains("duplicate") == true)
            {
                return BadRequest(ApiResponse.Fail("Transaction Number already exists."));
            }
            return StatusCode(StatusCodes.Status500InternalServerError, 
                ApiResponse.Fail("A database error occurred while saving the transaction. Please try again."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error saving transportation transaction #{TxNum}", dto.TransactionNumber);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                ApiResponse.Fail("An unexpected error occurred while processing the transaction."));
        }
    }

    private static TransportationDto MapToDto(Transportation entity)
    {
        return new TransportationDto
        {
            Id = entity.Id,
            TransactionNumber = entity.TransactionNumber,
            TransactionDate = entity.TransactionDate,
            CarNumber = entity.CarNumber,
            DriverName = entity.DriverName,
            Source = entity.Source,
            PONumber = entity.PONumber,
            MaterialType = entity.MaterialType,
            WeightBeforeLoading = entity.WeightBeforeLoading,
            WeightWhenEnteringFactory = entity.WeightWhenEnteringFactory,
            WeightWhenExitingFactory = entity.WeightWhenExitingFactory,
            ExpectedWeight = entity.ExpectedWeight,
            NetWeight = entity.NetWeight,
            Destination = entity.Destination,
            TransactionStatus = entity.TransactionStatus,
            ReceivingStatus = entity.ReceivingStatus,
            Notes = entity.Notes,
            DateOfSubmit = entity.DateOfSubmit
        };
    }
}
