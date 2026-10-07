using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FaceStone.Web.Models.DTOs;

public class CreateStorageTransactionDto
{
    // =========================================================================
    // SECTION 1 — TYPE (النوع)
    // =========================================================================

    /// <summary>
    /// نوع الخامة - Reused project standard: powder | slurry
    /// </summary>
    [Required(ErrorMessage = "نوع الخامة مطلوب.")]
    [RegularExpression("^(powder|slurry)$", ErrorMessage = "نوع الخامة يجب أن يكون powder أو slurry فقط.")]
    public string Material { get; set; } = string.Empty;

    /// <summary>
    /// نوع الحركة - Options ONLY: import (إيداع) | export (صرف)
    /// </summary>
    [Required(ErrorMessage = "نوع الحركة مطلوب.")]
    [RegularExpression("^(import|export|إيداع|صرف)$", ErrorMessage = "نوع الحركة يجب أن يكون إيداع (Import) أو صرف (Export).")]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// الكمية الواردة (TON) - Required when Action is import
    /// </summary>
    [Range(0.001, 1000000.0, ErrorMessage = "الكمية الواردة يجب أن تكون رقم عشري موجب.")]
    public decimal? IncomingQuantity { get; set; }

    /// <summary>
    /// الكمية الصادرة (TON) - Required when Action is export
    /// </summary>
    [Range(0.001, 1000000.0, ErrorMessage = "الكمية الصادرة يجب أن تكون رقم عشري موجب.")]
    public decimal? OutgoingQuantity { get; set; }

    /// <summary>
    /// هل الصادر مربوط بأمر شراء؟ (Checked only when Action is export)
    /// </summary>
    public bool IsExportRelatedToPO { get; set; }

    /// <summary>
    /// رقم أمر الشراء (PO) - Mandatory when IsExportRelatedToPO is true
    /// </summary>
    [MaxLength(100, ErrorMessage = "رقم أمر الشراء لا يمكن أن يتجاوز 100 حرف.")]
    public string? PONumber { get; set; }

    // =========================================================================
    // SECTION 2 — DATE (التاريخ)
    // =========================================================================

    /// <summary>
    /// تاريخ ووقت الحركة (Calendar/Date-time control, Required)
    /// </summary>
    [Required(ErrorMessage = "تاريخ ووقت الحركة مطلوب.")]
    public DateTime? TransactionDate { get; set; }

    // =========================================================================
    // SECTION 3 — WEIGHBRIDGE (الميزان)
    // =========================================================================

    // الميزان الداخلي (Internal Weighbridge)
    /// <summary>
    /// رقم المرجع للميزان الداخلي (Entered manually by employee)
    /// </summary>
    [Required(ErrorMessage = "رقم مرجع الميزان الداخلي مطلوب.")]
    [MaxLength(100, ErrorMessage = "رقم مرجع الميزان الداخلي لا يتجاوز 100 حرف.")]
    public string InternalWeighbridgeReference { get; set; } = string.Empty;

    [Required(ErrorMessage = "الوزن الداخلي قائم مطلوب.")]
    [Range(0.001, 1000000.0, ErrorMessage = "الوزن الداخلي قائم يجب أن يكون رقم موجب.")]
    public decimal InternalLoadedWeight { get; set; }

    [Required(ErrorMessage = "الوزن الداخلي فارغ مطلوب.")]
    [Range(0.001, 1000000.0, ErrorMessage = "الوزن الداخلي فارغ يجب أن يكون رقم موجب.")]
    public decimal InternalEmptyWeight { get; set; }

    public DateTime? InternalEmptyWeightTime { get; set; }

    [Required(ErrorMessage = "تاريخ ووقت الوزن القائم الداخلي مطلوب.")]
    public DateTime? InternalLoadedWeightTime { get; set; }

    // الميزان الخارجي (External Weighbridge)
    /// <summary>
    /// هل يوجد وزن خارجي؟
    /// </summary>
    public bool HasExternalWeighbridge { get; set; }

    [MaxLength(100, ErrorMessage = "رقم مرجع الميزان الخارجي لا يتجاوز 100 حرف.")]
    public string? ExternalWeighbridgeReference { get; set; }

    [Range(0.001, 1000000.0, ErrorMessage = "الوزن الخارجي قائم يجب أن يكون رقم موجب.")]
    public decimal? ExternalLoadedWeight { get; set; }

    [Range(0.001, 1000000.0, ErrorMessage = "الوزن الخارجي فارغ يجب أن يكون رقم موجب.")]
    public decimal? ExternalEmptyWeight { get; set; }

    public DateTime? ExternalEmptyWeightTime { get; set; }

    public DateTime? ExternalLoadedWeightTime { get; set; }

    // إيصال الميزان (Weighbridge Receipt Attachment)
    public IFormFile? WeighbridgeAttachment { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }

    // =========================================================================
    // SECTION 4 — RESPONSIBLE EMPLOYEE (الموظف المسؤول)
    // =========================================================================

    /// <summary>
    /// المسؤول (Controlled selection from existing Users database)
    /// </summary>
    [Required(ErrorMessage = "الموظف المسؤول مطلوب.")]
    [MaxLength(150, ErrorMessage = "اسم الموظف المسؤول لا يتجاوز 150 حرف.")]
    public string ResponsibleEmployee { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "الملاحظات لا يمكن أن تتجاوز 1000 حرف.")]
    public string? Notes { get; set; }

    // Optional legacy movement number if provided
    public int? MovementNumber { get; set; }
}

public class StorageTransactionDto
{
    public int Id { get; set; }
    public int MovementNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string Material { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ServiceType { get; set; } = string.Empty;
    public decimal? IncomingQuantity { get; set; }
    public decimal? OutgoingQuantity { get; set; }
    public bool IsExportRelatedToPO { get; set; }
    public string? PONumber { get; set; }
    public int? PurchaseOrderId { get; set; }

    // Internal Weighbridge
    public string InternalWeighbridgeReference { get; set; } = string.Empty;
    public decimal InternalLoadedWeight { get; set; }
    public decimal InternalEmptyWeight { get; set; }
    public decimal InternalNetWeight { get; set; }
    public DateTime? InternalEmptyWeightTime { get; set; }
    public DateTime? InternalLoadedWeightTime { get; set; }

    // External Weighbridge
    public bool HasExternalWeighbridge { get; set; }
    public string? ExternalWeighbridgeReference { get; set; }
    public decimal? ExternalLoadedWeight { get; set; }
    public decimal? ExternalEmptyWeight { get; set; }
    public decimal? ExternalNetWeight { get; set; }
    public DateTime? ExternalEmptyWeightTime { get; set; }
    public DateTime? ExternalLoadedWeightTime { get; set; }

    // Attachment
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? WeighbridgeFileType { get; set; }
    public long WeighbridgeFileSize { get; set; }

    // Responsible Employee
    public string ResponsibleEmployee { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public string ApprovalStatus { get; set; } = "PENDING";
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class StorageConfigDto
{
    public List<string> Materials { get; set; } = new();
    public List<string> Actions { get; set; } = new();
    public List<EmployeeSummaryDto> Employees { get; set; } = new();
    public int NextMovementNumber { get; set; }
}

public class EmployeeSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

public class StoragePoLookupDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public DateTime OrderDate { get; set; }
    public string? Notes { get; set; }
}

public class CheckReferenceResultDto
{
    public string ReferenceNumber { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public string? Message { get; set; }
}

public class CurrentBalanceResultDto
{
    public string StorageLocation { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public DateTime? LastUpdated { get; set; }
}

public class TransportationMovementSummaryDto
{
    public int TransactionNumber { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string CarNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public DateTime Date { get; set; }
    public string Destination { get; set; } = string.Empty;
}
