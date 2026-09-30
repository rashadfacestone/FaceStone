using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models.DTOs;

public class ManagementCarMovementDto
{
    public string CarNumber { get; set; } = string.Empty;
    public int TransactionNumber { get; set; }
    public decimal NetWeight { get; set; }
    public DateTime Date { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FormattedMovement => $"{CarNumber} - حركة {TransactionNumber}";
}

public class ManagementRecordDto
{
    public string TransactionNumber { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string? PONumber { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public int NumberOfTransactions { get; set; }
    public decimal TotalReceivedAmount { get; set; }
    public string CarPlatesNumber { get; set; } = string.Empty;
    public List<ManagementCarMovementDto> CarMovements { get; set; } = new();
    public decimal AcceptedQuantity { get; set; }
    public decimal UnacceptedPendingQuantity { get; set; }
    public string DeliveryStatus { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ManagementSummaryDto
{
    public int TotalMovements { get; set; }
    public decimal TotalReceivedAmount { get; set; }
    public decimal TotalAcceptedQuantity { get; set; }
    public decimal TotalUnacceptedPendingQuantity { get; set; }
}

public class ManagementFilterOptionsDto
{
    public List<string> MaterialTypes { get; set; } = new();
    public List<string> ReceivingStatuses { get; set; } = new();
    public List<string> Destinations { get; set; } = new();
}

public class ManagementPageResponseDto
{
    public List<ManagementRecordDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public ManagementSummaryDto Summary { get; set; } = new();
    public ManagementFilterOptionsDto FilterOptions { get; set; } = new();
}

public class ManagementQueryParameters
{
    public string? Search { get; set; }
    public string? MaterialType { get; set; }
    public string? ReceivingStatus { get; set; }
    public string? Destination { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SortBy { get; set; } = "date";
    public string? SortOrder { get; set; } = "desc";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
