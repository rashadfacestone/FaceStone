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
[Authorize(Policy = "ManagementAccess")]
public class ManagementController : ControllerBase
{
    private readonly FaceStoneDbContext _context;
    private readonly ILogger<ManagementController> _logger;

    public ManagementController(FaceStoneDbContext context, ILogger<ManagementController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Gets aggregated management data, KPI summary, and filter options with server-side pagination and sorting.
    /// Accessible to CEO, IT, and Factory Manager.
    /// </summary>
    [HttpGet("data")]
    [ProducesResponseType(typeof(ApiResponse<ManagementPageResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetManagementData([FromQuery] ManagementQueryParameters query)
    {
        try
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value ?? "Unknown";
            _logger.LogInformation("Management data requested by role '{Role}'. Search: '{Search}', Page: {Page}, PageSize: {PageSize}",
                userRole, query.Search, query.Page, query.PageSize);

            // 1. Fetch raw transportation records
            var transRecords = await _context.Transportations
                .AsNoTracking()
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();

            // 2. Fetch any standalone records from Management table
            var standaloneMgmt = await _context.Management
                .AsNoTracking()
                .ToListAsync();

            // 3. Aggregate Transportation records by PO Number (or by TxNumber if PO is empty)
            var aggregatedRecords = new List<ManagementRecordDto>();

            var groupedByPo = transRecords
                .GroupBy(t => string.IsNullOrWhiteSpace(t.PONumber) ? $"TX_{t.TransactionNumber}" : t.PONumber.Trim())
                .ToList();

            foreach (var group in groupedByPo)
            {
                var sortedItems = group.OrderByDescending(x => x.TransactionDate).ToList();
                var latest = sortedItems.First();

                var carMovements = sortedItems.Select(x => new ManagementCarMovementDto
                {
                    CarNumber = x.CarNumber,
                    TransactionNumber = x.TransactionNumber,
                    NetWeight = x.NetWeight,
                    Date = x.TransactionDate,
                    Status = x.ReceivingStatus
                }).ToList();

                var carPlatesFormatted = string.Join(" \n ", sortedItems.Select(x => $"{x.CarNumber} - حركة {x.TransactionNumber}"));

                var totalReceived = sortedItems.Sum(x => x.NetWeight);
                var accepted = sortedItems
                    .Where(x => x.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.NetWeight);
                var unaccepted = sortedItems
                    .Where(x => !x.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.NetWeight);

                // Determine representative DeliveryStatus
                string overallStatus;
                if (sortedItems.All(x => x.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase)))
                {
                    overallStatus = "Approved / Accepted";
                }
                else if (sortedItems.Any(x => x.ReceivingStatus.Equals("Rejected", StringComparison.OrdinalIgnoreCase)))
                {
                    overallStatus = "Rejected";
                }
                else if (sortedItems.Any(x => x.ReceivingStatus.Equals("Under Quality Test", StringComparison.OrdinalIgnoreCase)))
                {
                    overallStatus = "Under Quality Test";
                }
                else if (sortedItems.Any(x => x.ReceivingStatus.Equals("Partially Accepted", StringComparison.OrdinalIgnoreCase)))
                {
                    overallStatus = "Partially Accepted";
                }
                else
                {
                    overallStatus = latest.ReceivingStatus;
                }

                // Combine non-empty notes
                var notesList = sortedItems
                    .Select(x => x.Notes?.Trim())
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .ToList();
                var combinedNotes = notesList.Count > 0 ? string.Join("; ", notesList) : null;

                aggregatedRecords.Add(new ManagementRecordDto
                {
                    TransactionNumber = latest.TransactionNumber.ToString(),
                    Date = latest.TransactionDate,
                    PONumber = string.IsNullOrWhiteSpace(latest.PONumber) ? "-" : latest.PONumber.Trim(),
                    MaterialType = latest.MaterialType,
                    NumberOfTransactions = sortedItems.Count,
                    TotalReceivedAmount = totalReceived,
                    CarPlatesNumber = carPlatesFormatted,
                    CarMovements = carMovements,
                    AcceptedQuantity = accepted,
                    UnacceptedPendingQuantity = unaccepted,
                    DeliveryStatus = overallStatus,
                    Destination = latest.Destination,
                    Notes = combinedNotes
                });
            }

            // Also incorporate standalone Management table rows if their PO/TxNumber is not already present
            var existingPos = new HashSet<string>(aggregatedRecords.Select(r => r.PONumber ?? "").Where(s => s != "-"), StringComparer.OrdinalIgnoreCase);
            var existingTxNums = new HashSet<string>(aggregatedRecords.Select(r => r.TransactionNumber), StringComparer.OrdinalIgnoreCase);

            foreach (var m in standaloneMgmt)
            {
                if (!existingTxNums.Contains(m.TransactionNumber) && (string.IsNullOrWhiteSpace(m.PO) || !existingPos.Contains(m.PO)))
                {
                    aggregatedRecords.Add(new ManagementRecordDto
                    {
                        TransactionNumber = m.TransactionNumber,
                        Date = m.Date,
                        PONumber = string.IsNullOrWhiteSpace(m.PO) ? "-" : m.PO.Trim(),
                        MaterialType = m.MaterialType,
                        NumberOfTransactions = m.NumberOfTransactions > 0 ? m.NumberOfTransactions : 1,
                        TotalReceivedAmount = m.TotalReceivedAmount,
                        CarPlatesNumber = m.CarPlatesNumber,
                        CarMovements = new List<ManagementCarMovementDto>(),
                        AcceptedQuantity = m.AcceptedQuantity,
                        UnacceptedPendingQuantity = m.UnacceptedPendingQuantity,
                        DeliveryStatus = m.DeliveryStatus,
                        Destination = m.Destination,
                        Notes = m.Notes
                    });
                }
            }

            // 4. Compute dynamic filter options from all records
            var filterOptions = new ManagementFilterOptionsDto
            {
                MaterialTypes = aggregatedRecords.Select(r => r.MaterialType).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                ReceivingStatuses = aggregatedRecords.Select(r => r.DeliveryStatus).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList(),
                Destinations = aggregatedRecords.Select(r => r.Destination).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToList()
            };

            // 5. Apply Search Filter
            var filtered = aggregatedRecords.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim();
                filtered = filtered.Where(r =>
                    r.TransactionNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (r.PONumber != null && r.PONumber.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    r.MaterialType.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    r.CarPlatesNumber.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    r.Destination.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    r.CarMovements.Any(cm => cm.CarNumber.Contains(s, StringComparison.OrdinalIgnoreCase) || cm.TransactionNumber.ToString().Contains(s))
                );
            }

            // Apply Material Type Dropdown Filter
            if (!string.IsNullOrWhiteSpace(query.MaterialType) && !query.MaterialType.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.MaterialType.Equals(query.MaterialType.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            // Apply Receiving Status Dropdown Filter
            if (!string.IsNullOrWhiteSpace(query.ReceivingStatus) && !query.ReceivingStatus.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.DeliveryStatus.Equals(query.ReceivingStatus.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            // Apply Destination Dropdown Filter
            if (!string.IsNullOrWhiteSpace(query.Destination) && !query.Destination.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(r => r.Destination.Equals(query.Destination.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            // Apply Date Range Filter
            if (query.FromDate.HasValue)
            {
                var fromDateUtc = query.FromDate.Value.Date;
                filtered = filtered.Where(r => r.Date.Date >= fromDateUtc);
            }

            if (query.ToDate.HasValue)
            {
                var toDateUtc = query.ToDate.Value.Date;
                filtered = filtered.Where(r => r.Date.Date <= toDateUtc);
            }

            var filteredList = filtered.ToList();

            // 6. Calculate Summary KPIs from actual database records
            var summary = new ManagementSummaryDto
            {
                TotalMovements = filteredList.Sum(r => r.NumberOfTransactions),
                TotalReceivedAmount = filteredList.Sum(r => r.TotalReceivedAmount),
                TotalAcceptedQuantity = filteredList.Sum(r => r.AcceptedQuantity),
                TotalUnacceptedPendingQuantity = filteredList.Sum(r => r.UnacceptedPendingQuantity)
            };

            // 7. Apply Sorting
            var sortCol = (query.SortBy ?? "date").ToLowerInvariant();
            var isAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);

            filteredList = sortCol switch
            {
                "txnumber" => isAsc ? filteredList.OrderBy(r => r.TransactionNumber).ToList() : filteredList.OrderByDescending(r => r.TransactionNumber).ToList(),
                "po" => isAsc ? filteredList.OrderBy(r => r.PONumber).ToList() : filteredList.OrderByDescending(r => r.PONumber).ToList(),
                "material" => isAsc ? filteredList.OrderBy(r => r.MaterialType).ToList() : filteredList.OrderByDescending(r => r.MaterialType).ToList(),
                "trips" => isAsc ? filteredList.OrderBy(r => r.NumberOfTransactions).ToList() : filteredList.OrderByDescending(r => r.NumberOfTransactions).ToList(),
                "received" => isAsc ? filteredList.OrderBy(r => r.TotalReceivedAmount).ToList() : filteredList.OrderByDescending(r => r.TotalReceivedAmount).ToList(),
                "accepted" => isAsc ? filteredList.OrderBy(r => r.AcceptedQuantity).ToList() : filteredList.OrderByDescending(r => r.AcceptedQuantity).ToList(),
                "pending" => isAsc ? filteredList.OrderBy(r => r.UnacceptedPendingQuantity).ToList() : filteredList.OrderByDescending(r => r.UnacceptedPendingQuantity).ToList(),
                "status" => isAsc ? filteredList.OrderBy(r => r.DeliveryStatus).ToList() : filteredList.OrderByDescending(r => r.DeliveryStatus).ToList(),
                "destination" => isAsc ? filteredList.OrderBy(r => r.Destination).ToList() : filteredList.OrderByDescending(r => r.Destination).ToList(),
                _ => isAsc ? filteredList.OrderBy(r => r.Date).ToList() : filteredList.OrderByDescending(r => r.Date).ToList(),
            };

            // 8. Apply Pagination
            var totalCount = filteredList.Count;
            var pageSize = query.PageSize > 0 ? Math.Min(query.PageSize, 100) : 10;
            var page = Math.Max(query.Page, 1);
            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var pagedItems = filteredList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var response = new ManagementPageResponseDto
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Summary = summary,
                FilterOptions = filterOptions
            };

            return Ok(ApiResponse<ManagementPageResponseDto>.Ok(response, "Management records retrieved successfully."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving management data.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("An error occurred while loading management records."));
        }
    }

    /// <summary>
    /// Gets only the dashboard summary KPIs calculated from live records.
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<ManagementSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetManagementSummary()
    {
        try
        {
            var transRecords = await _context.Transportations
                .AsNoTracking()
                .ToListAsync();

            var summary = new ManagementSummaryDto
            {
                TotalMovements = transRecords.Count,
                TotalReceivedAmount = transRecords.Sum(t => t.NetWeight),
                TotalAcceptedQuantity = transRecords
                    .Where(t => t.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.NetWeight),
                TotalUnacceptedPendingQuantity = transRecords
                    .Where(t => !t.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase))
                    .Sum(t => t.NetWeight)
            };

            return Ok(ApiResponse<ManagementSummaryDto>.Ok(summary));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating management summary.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse.Fail("Failed to calculate management summary."));
        }
    }

    /// <summary>
    /// Synchronizes current aggregated transportation data into the SQL Management table.
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> SyncManagementTable()
    {
        try
        {
            var transRecords = await _context.Transportations.AsNoTracking().ToListAsync();
            var grouped = transRecords
                .GroupBy(t => string.IsNullOrWhiteSpace(t.PONumber) ? $"TX_{t.TransactionNumber}" : t.PONumber.Trim())
                .ToList();

            int syncedCount = 0;
            foreach (var g in grouped)
            {
                var latest = g.OrderByDescending(x => x.TransactionDate).First();
                var txKey = $"MGMT-{latest.TransactionNumber}";

                var existing = await _context.Management.FirstOrDefaultAsync(m => m.TransactionNumber == txKey);
                if (existing == null)
                {
                    var mRec = new Management
                    {
                        TransactionNumber = txKey,
                        Date = latest.TransactionDate,
                        PO = string.IsNullOrWhiteSpace(latest.PONumber) ? null : latest.PONumber.Trim(),
                        MaterialType = latest.MaterialType,
                        NumberOfTransactions = g.Count(),
                        TotalReceivedAmount = g.Sum(x => x.NetWeight),
                        CarPlatesNumber = latest.CarNumber,
                        AcceptedQuantity = g.Where(x => x.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase)).Sum(x => x.NetWeight),
                        UnacceptedPendingQuantity = g.Where(x => !x.ReceivingStatus.Equals("Approved / Accepted", StringComparison.OrdinalIgnoreCase)).Sum(x => x.NetWeight),
                        DeliveryStatus = latest.ReceivingStatus,
                        Destination = latest.Destination,
                        Notes = latest.Notes
                    };
                    await _context.Management.AddAsync(mRec);
                    syncedCount++;
                }
            }

            if (syncedCount > 0)
            {
                await _context.SaveChangesAsync();
            }

            return Ok(ApiResponse.Ok($"Management table synchronized successfully. Synced {syncedCount} new records."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error syncing management table.");
            return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Fail("Sync failed: " + ex.Message));
        }
    }
}
