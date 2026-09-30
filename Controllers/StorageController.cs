using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using FaceStone.Web.Data;
using FaceStone.Web.Models;
using FaceStone.Web.Models.DTOs;

namespace FaceStone.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "StorageAccess")]
[Produces("application/json")]
public class StorageController : ControllerBase
{
    private readonly FaceStoneDbContext _db;
    private readonly ILogger<StorageController> _logger;

    public StorageController(FaceStoneDbContext db, ILogger<StorageController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Supported Service Types in Arabic
    /// </summary>
    public static readonly List<string> ServiceTypes = new()
    {
        "إضافة",
        "صرف",
        "تحويل",
        "تسوية"
    };

    /// <summary>
    /// Configured Storage Locations matching FaceStone factory layout and silos
    /// </summary>
    public static readonly List<string> StorageLocations = new()
    {
        "مستودع الخامات الرئيسي (صوامع وتخزين الركام)",
        "مستودع قطاع المصنع أ (محطة الخلط 1)",
        "مستودع قطاع المصنع ب (ساحة مسبق الصب)",
        "مستودع قطع الغيار والمهمات",
        "مستودع المواد الكيميائية والإضافات",
        "ساحة تشوين المواد الجافة",
        "صوامع الأسمنت الرئيسية"
    };

    /// <summary>
    /// Retrieve configuration options, storage locations, active employees, and recent transportation movements.
    /// </summary>
    [HttpGet("config")]
    [ProducesResponseType(typeof(ApiResponse<StorageConfigDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConfig()
    {
        // 1. Fetch available employees/users from Users table
        var employees = await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.FullName)
            .Select(u => new EmployeeSummaryDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Username = u.Username,
                Role = u.Role
            })
            .ToListAsync();

        // 2. Fetch recent transportation movements for quick linking
        var recentTrans = await _db.Transportations
            .AsNoTracking()
            .OrderByDescending(t => t.TransactionDate)
            .Take(30)
            .Select(t => new TransportationMovementSummaryDto
            {
                TransactionNumber = t.TransactionNumber,
                PONumber = t.PONumber,
                CarNumber = t.CarNumber,
                DriverName = t.DriverName,
                MaterialType = t.MaterialType,
                NetWeight = t.NetWeight,
                Date = t.TransactionDate,
                Destination = t.Destination
            })
            .ToListAsync();

        // 3. Compute latest overall balance
        var latestTx = await _db.StorageTransactions
            .AsNoTracking()
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync();

        var config = new StorageConfigDto
        {
            ServiceTypes = ServiceTypes,
            StorageLocations = StorageLocations,
            Employees = employees,
            AvailableTransportationMovements = recentTrans,
            CurrentOverallBalance = latestTx?.ClosingBalance ?? 0m
        };

        return Ok(ApiResponse<StorageConfigDto>.Ok(config));
    }

    /// <summary>
    /// Real-time uniqueness check for Reference Number (رقم المرجع).
    /// </summary>
    [HttpGet("check-reference/{referenceNumber:int}")]
    [ProducesResponseType(typeof(ApiResponse<CheckReferenceResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckReferenceNumber(int referenceNumber)
    {
        var exists = await _db.StorageTransactions
            .AsNoTracking()
            .AnyAsync(s => s.ReferenceNumber == referenceNumber);

        var result = new CheckReferenceResultDto
        {
            ReferenceNumber = referenceNumber,
            Exists = exists,
            Message = exists ? "رقم المرجع مستخدم بالفعل" : null
        };

        return Ok(ApiResponse<CheckReferenceResultDto>.Ok(result));
    }

    /// <summary>
    /// Retrieve latest balance for a specific storage location (or overall) to set opening balance.
    /// </summary>
    [HttpGet("balance")]
    [ProducesResponseType(typeof(ApiResponse<CurrentBalanceResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentBalance([FromQuery] string? storageLocation)
    {
        IQueryable<StorageTransaction> query = _db.StorageTransactions.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(storageLocation))
        {
            query = query.Where(s => s.StorageLocation == storageLocation || s.DestinationStorageLocation == storageLocation);
        }

        var latest = await query
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync();

        var result = new CurrentBalanceResultDto
        {
            StorageLocation = storageLocation ?? "All",
            CurrentBalance = latest?.ClosingBalance ?? 0m,
            LastUpdated = latest?.TransactionDate
        };

        return Ok(ApiResponse<CurrentBalanceResultDto>.Ok(result));
    }

    /// <summary>
    /// Retrieve recent storage transactions with linked transportation movement numbers.
    /// </summary>
    [HttpGet("recent")]
    [ProducesResponseType(typeof(ApiResponse<List<StorageTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRecentTransactions([FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 100);

        var transactions = await _db.StorageTransactions
            .AsNoTracking()
            .Include(s => s.TransportationMovements)
            .OrderByDescending(s => s.TransactionDate)
            .ThenByDescending(s => s.Id)
            .Take(limit)
            .ToListAsync();

        var dtos = transactions.Select(MapToDto).ToList();
        return Ok(ApiResponse<List<StorageTransactionDto>>.Ok(dtos));
    }

    /// <summary>
    /// Create and persist a new Storage Transaction with multiple transportation movements.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<StorageTransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateStorageTransaction([FromBody] CreateStorageTransactionDto dto)
    {
        if (dto == null)
        {
            return BadRequest(ApiResponse.Fail("بيانات الحركة غير صالحة."));
        }

        // 1. Validate required fields
        if (dto.MovementNumber <= 0)
        {
            return BadRequest(ApiResponse.Fail("يرجى إدخال رقم حركة صحيح."));
        }

        if (!dto.TransactionDate.HasValue)
        {
            return BadRequest(ApiResponse.Fail("التاريخ مطلوب."));
        }

        if (string.IsNullOrWhiteSpace(dto.ServiceType) || !ServiceTypes.Contains(dto.ServiceType.Trim()))
        {
            return BadRequest(ApiResponse.Fail("نوع الخدمة يجب أن يكون: إضافة، صرف، تحويل، أو تسوية."));
        }
        var serviceType = dto.ServiceType.Trim();

        if (dto.ReferenceNumber <= 0)
        {
            return BadRequest(ApiResponse.Fail("يرجى إدخال رقم مرجع صحيح."));
        }

        // 2. Strict uniqueness check for رقم المرجع
        var refExists = await _db.StorageTransactions
            .AsNoTracking()
            .AnyAsync(s => s.ReferenceNumber == dto.ReferenceNumber);

        if (refExists)
        {
            _logger.LogWarning("Duplicate Reference Number attempted: {RefNo}", dto.ReferenceNumber);
            return BadRequest(ApiResponse.Fail("رقم المرجع مستخدم بالفعل"));
        }

        if (string.IsNullOrWhiteSpace(dto.PONumber))
        {
            return BadRequest(ApiResponse.Fail("رقم أمر الشراء (PO) مطلوب."));
        }

        // 3. Validate multiple transportation movements (رقم النقلة)
        if (dto.TransportationMovementNumbers == null || !dto.TransportationMovementNumbers.Any(n => n > 0))
        {
            return BadRequest(ApiResponse.Fail("يجب إضافة رقم نقلة واحد على الأقل مرتبط بالحركة."));
        }

        // Filter and deduplicate movement numbers for this transaction
        var validMovements = dto.TransportationMovementNumbers.Where(n => n > 0).Distinct().ToList();

        // 4. Validate quantities according to Service Type
        decimal incomingQty = Math.Max(0m, dto.IncomingQuantity);
        decimal outgoingQty = Math.Max(0m, dto.OutgoingQuantity);

        switch (serviceType)
        {
            case "إضافة":
                if (incomingQty <= 0)
                {
                    return BadRequest(ApiResponse.Fail("في حالة الإضافة، يجب أن تكون الكمية الواردة أكبر من الصفر."));
                }
                outgoingQty = 0m;
                break;

            case "صرف":
                if (outgoingQty <= 0)
                {
                    return BadRequest(ApiResponse.Fail("في حالة الصرف، يجب أن تكون الكمية المنصرفة أكبر من الصفر."));
                }
                incomingQty = 0m;
                break;

            case "تحويل":
                if (string.IsNullOrWhiteSpace(dto.DestinationStorageLocation))
                {
                    return BadRequest(ApiResponse.Fail("في حالة التحويل، يجب تحديد مكان التخزين المحول إليه."));
                }
                if (string.Equals(dto.StorageLocation?.Trim(), dto.DestinationStorageLocation?.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(ApiResponse.Fail("مكان التخزين المحول منه يجب أن يختلف عن مكان التخزين المحول إليه."));
                }
                if (outgoingQty <= 0 && incomingQty <= 0)
                {
                    return BadRequest(ApiResponse.Fail("في حالة التحويل، يجب تحديد الكمية المحولة."));
                }
                if (outgoingQty <= 0 && incomingQty > 0)
                {
                    outgoingQty = incomingQty;
                }
                incomingQty = 0m;
                break;

            case "تسوية":
                if (incomingQty == 0 && outgoingQty == 0)
                {
                    return BadRequest(ApiResponse.Fail("في حالة التسوية، يجب إدخال كمية التسوية (إضافة رصيد فائض أو صرف عجز)."));
                }
                break;
        }

        // 5. Automatic Closing Balance Calculation: Closing = Opening + Incoming - Outgoing
        decimal openingBalance = dto.OpeningBalance;
        decimal closingBalance = openingBalance + incomingQty - outgoingQty;

        if (string.IsNullOrWhiteSpace(dto.StorageLocation))
        {
            return BadRequest(ApiResponse.Fail("مكان التخزين مطلوب."));
        }

        if (string.IsNullOrWhiteSpace(dto.ReceivingEmployee))
        {
            return BadRequest(ApiResponse.Fail("اسم الموظف المستلم مطلوب."));
        }

        // 6. Build Entity and link multiple Transportation Movements
        var transaction = new StorageTransaction
        {
            MovementNumber = dto.MovementNumber,
            TransactionDate = dto.TransactionDate.Value,
            ServiceType = serviceType,
            ReferenceNumber = dto.ReferenceNumber,
            PONumber = dto.PONumber.Trim(),
            IncomingQuantity = incomingQty,
            OutgoingQuantity = outgoingQty,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            StorageLocation = dto.StorageLocation.Trim(),
            DestinationStorageLocation = (serviceType == "تحويل" && !string.IsNullOrWhiteSpace(dto.DestinationStorageLocation)) 
                ? dto.DestinationStorageLocation.Trim() 
                : null,
            ReceivingEmployee = dto.ReceivingEmployee.Trim(),
            Notes = dto.Notes?.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        // Query existing Transportation records for these movement numbers
        var matchedTransList = await _db.Transportations
            .AsNoTracking()
            .Where(t => validMovements.Contains(t.TransactionNumber))
            .ToListAsync();

        var transLookup = matchedTransList.ToDictionary(t => t.TransactionNumber, t => t.Id);

        foreach (var movNum in validMovements)
        {
            var mov = new StorageTransactionMovement
            {
                TransportationMovementNumber = movNum,
                TransportationId = transLookup.TryGetValue(movNum, out var transId) ? transId : null
            };
            transaction.TransportationMovements.Add(mov);
        }

        try
        {
            await _db.StorageTransactions.AddAsync(transaction);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Storage Transaction {MovementNumber} saved successfully with Reference {RefNo}. Multiple transportation movements count: {MovCount}",
                transaction.MovementNumber, transaction.ReferenceNumber, validMovements.Count);

            var resultDto = MapToDto(transaction);
            return StatusCode(StatusCodes.Status201Created, 
                ApiResponse<StorageTransactionDto>.Ok(resultDto, "تم حفظ حركة المخزن بنجاح"));
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database update error saving storage transaction.");
            if (ex.InnerException?.Message.Contains("IX_StorageTransactions_ReferenceNumber") ?? false)
            {
                return BadRequest(ApiResponse.Fail("رقم المرجع مستخدم بالفعل"));
            }
            return StatusCode(StatusCodes.Status500InternalServerError, 
                ApiResponse.Fail("حدث خطأ أثناء حفظ حركة المخزن في قاعدة البيانات."));
        }
    }

    private static StorageTransactionDto MapToDto(StorageTransaction s)
    {
        return new StorageTransactionDto
        {
            Id = s.Id,
            MovementNumber = s.MovementNumber,
            TransactionDate = s.TransactionDate,
            ServiceType = s.ServiceType,
            ReferenceNumber = s.ReferenceNumber,
            PONumber = s.PONumber,
            TransportationMovementNumbers = s.TransportationMovements?
                .Select(m => m.TransportationMovementNumber)
                .ToList() ?? new List<int>(),
            IncomingQuantity = s.IncomingQuantity,
            OutgoingQuantity = s.OutgoingQuantity,
            OpeningBalance = s.OpeningBalance,
            ClosingBalance = s.ClosingBalance,
            StorageLocation = s.StorageLocation,
            DestinationStorageLocation = s.DestinationStorageLocation,
            ReceivingEmployee = s.ReceivingEmployee,
            Notes = s.Notes,
            CreatedAt = s.CreatedAt
        };
    }
}
