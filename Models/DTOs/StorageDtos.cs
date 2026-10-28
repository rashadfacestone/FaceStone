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

public class RawMaterialSummaryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal InitialQuantity { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public decimal CurrentQuantity { get; set; }
    public decimal CurrentValue { get; set; }
    public decimal AverageUnitPrice { get; set; }
    public decimal TotalIncoming { get; set; }
    public decimal TotalOutgoing { get; set; }
    public int TransactionCount { get; set; }
    public string MaterialType { get; set; } = "MECHANICAL";
    public string MaterialTypeAr => MaterialProcessType.ToArabic(MaterialType);
}

public class CreateRawMaterialDto
{
    [Required(ErrorMessage = "اسم الصنف مطلوب.")]
    [MaxLength(200, ErrorMessage = "اسم الصنف لا يتجاوز 200 حرف.")]
    public string Name { get; set; } = string.Empty;

    public string MaterialType { get; set; } = "MECHANICAL";

    [Range(0, 10000000.0, ErrorMessage = "الكمية يجب أن تكون صفر أو أكبر.")]
    public decimal InitialQuantity { get; set; } = 0m;
}

public class CreateItemTransactionDto
{
    [Required(ErrorMessage = "نوع الحركة مطلوب.")]
    [RegularExpression("^(Incoming|Outgoing|وارد|منصرف)$", ErrorMessage = "نوع الحركة يجب أن يكون وارد أو منصرف.")]
    public string TransactionType { get; set; } = "Incoming";

    [Required(ErrorMessage = "تاريخ الحركة مطلوب.")]
    public DateTime TransactionDate { get; set; }

    [Required(ErrorMessage = "البيان مطلوب.")]
    [RegularExpression("^(علاء|اسلام|محمود)$", ErrorMessage = "البيان يجب أن يكون أحد الخيارات التالية: علاء، اسلام، محمود.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "الكمية مطلوبة.")]
    [Range(0.0001, 10000000.0, ErrorMessage = "الكمية يجب أن تكون أكبر من الصفر.")]
    public decimal Quantity { get; set; }

    [Range(0.0, 10000000.0, ErrorMessage = "سعر الوحدة يجب أن يكون صفر أو أكبر.")]
    public decimal UnitPrice { get; set; } = 0m;

    public string? DestinationType { get; set; }

    public string? Destination { get; set; }

    public int? SourceTransportationId { get; set; }

    public string? Notes { get; set; }
}

public class ItemTransactionLedgerItemDto
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? IncomingQuantity { get; set; }
    public decimal? IncomingUnitPrice { get; set; }
    public decimal? IncomingTotalValue { get; set; }
    public decimal? OutgoingQuantity { get; set; }
    public decimal? OutgoingUnitPrice { get; set; }
    public decimal? OutgoingTotalValue { get; set; }
    public string? DestinationType { get; set; }
    public string? Destination { get; set; }
    public decimal RunningQuantityBalance { get; set; }
    public decimal RunningValueBalance { get; set; }
    public int? SourceTransportationId { get; set; }
    public int? SourceTransportationNumber { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}

public class ItemCardLedgerResponseDto
{
    public RawMaterialSummaryDto Material { get; set; } = null!;
    public decimal OpeningBalance { get; set; }
    public decimal CurrentQuantityBalance { get; set; }
    public decimal CurrentValueBalance { get; set; }
    public decimal AverageUnitCost { get; set; }
    public decimal TotalIncomingQuantity { get; set; }
    public decimal TotalOutgoingQuantity { get; set; }
    public List<ItemTransactionLedgerItemDto> Transactions { get; set; } = new();
}

public class TransportationCandidateDto
{
    public int TransportationId { get; set; }
    public int TransactionNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public decimal? TonPrice { get; set; }
    public string DriverName { get; set; } = string.Empty;
    public string CarNumber { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public bool IsAlreadyPosted { get; set; }
    public string? PostedMaterialName { get; set; }
}

// =========================================================================
// STORAGE REDESIGN DTOs (3 MAIN CARDS: IMPORT, EXPORT, TRANSFER)
// =========================================================================

public class StorageLocationDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class StorageClientDestinationDto
{
    public int Id { get; set; }
    public int StorageClientId { get; set; }
    public string DestinationName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class StorageClientDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<StorageClientDestinationDto> Destinations { get; set; } = new();
}

public class StorageMaterialStockDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MaterialType { get; set; } = "MECHANICAL";
    public string MaterialTypeAr => MaterialProcessType.ToArabic(MaterialType);
    public decimal AvailableQuantity { get; set; }
    public int? StorageLocationId { get; set; }
    public string? StorageLocationName { get; set; }
}

public class StorageOperationLineInputDto
{
    public int? RawMaterialId { get; set; }
    public string? NewMaterialName { get; set; }

    [Range(0.0001, 10000000.0, ErrorMessage = "الكمية يجب أن تكون أكبر من الصفر.")]
    public decimal Quantity { get; set; }

    public string? TrackingNumber { get; set; }
    public string? Notes { get; set; }
}

public class StorageAttachmentItemDto
{
    public string Category { get; set; } = string.Empty; // "Weighbridge" | "QOC" | "Document"
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ViewUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
}

public class CreateStorageImportRequestDto
{
    [Required(ErrorMessage = "نوع الخامة (ميكانيكية أو كيميائية) مطلوب.")]
    [RegularExpression("^(MECHANICAL|CHEMICAL|خامات ميكانيكية|خامات كيميائية)$", ErrorMessage = "نوع الخامة يجب أن يكون خامات ميكانيكية (MECHANICAL) أو خامات كيميائية (CHEMICAL).")]
    public string MaterialType { get; set; } = "MECHANICAL";

    [Required(ErrorMessage = "مكان التخزين مطلوب.")]
    public int StorageLocationId { get; set; }

    [Required(ErrorMessage = "المورد / المصدر مطلوب.")]
    public string Source { get; set; } = string.Empty;

    public string? SourcePhone { get; set; }

    public DateTime? TransactionDate { get; set; }
    public string? Notes { get; set; }

    // Internal & External Weighing (الميزان الداخلي والخارجي)
    public decimal? ExternalReportedWeight { get; set; }
    public decimal? InternalLoadedWeight { get; set; }
    public decimal? InternalEmptyWeight { get; set; }
    public decimal? InternalNetWeight { get; set; }
    public DateTime? InternalWeighingTime { get; set; }
    public string? WeighbridgeReference { get; set; }
    public string? WeighingResponsibleEmployee { get; set; }
    public string? DiscrepancyExplanation { get; set; }
    public int? TransportationId { get; set; }

    /// <summary>
    /// الرقم المرجعي التتبعي للإنتاج المشترك الصادر من قسم المعمل
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    public string? ItemsJson { get; set; }
    [Required(ErrorMessage = "يجب تحديد صنف واحد على الأقل.")]
    [MinLength(1, ErrorMessage = "يجب تحديد صنف واحد على الأقل.")]
    public List<StorageOperationLineInputDto> Items { get; set; } = new();

    // Attachments (Optional for submission, saved if provided)
    public IFormFile? WeighbridgeAttachment { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }

    public IFormFile? QocAttachment { get; set; }
    public string? QocAttachmentPath { get; set; }

    public IFormFile? DocumentAttachment { get; set; }
    public string? DocumentAttachmentPath { get; set; }
}

public class CreateStorageExportRequestDto
{
    [Required(ErrorMessage = "مكان التخزين مطلوب.")]
    public int StorageLocationId { get; set; }

    [Required(ErrorMessage = "العميل مطلوب.")]
    public int ClientId { get; set; }

    [Required(ErrorMessage = "الوجهة مطلوبة.")]
    public int DestinationId { get; set; }

    public string? ClientPhone { get; set; }

    public DateTime? TransactionDate { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// نوع وتصنيف الخامة: MECHANICAL (خامات ميكانيكية) | CHEMICAL (خامات كيميائية)
    /// </summary>
    public string? MaterialType { get; set; }

    // Internal & External Weighing (الميزان الداخلي والخارجي)
    public decimal? ExternalReportedWeight { get; set; }
    public decimal? InternalLoadedWeight { get; set; }
    public decimal? InternalEmptyWeight { get; set; }
    public decimal? InternalNetWeight { get; set; }
    public DateTime? InternalWeighingTime { get; set; }
    public string? WeighbridgeReference { get; set; }
    public string? WeighingResponsibleEmployee { get; set; }
    public string? DiscrepancyExplanation { get; set; }
    public int? TransportationId { get; set; }

    public string? ItemsJson { get; set; }
    [Required(ErrorMessage = "يجب تحديد صنف واحد على الأقل.")]
    [MinLength(1, ErrorMessage = "يجب تحديد صنف واحد على الأقل.")]
    public List<StorageOperationLineInputDto> Items { get; set; } = new();

    // Attachments (Optional for submission, saved if provided)
    public IFormFile? WeighbridgeAttachment { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }

    public IFormFile? DocumentAttachment { get; set; }
    public string? DocumentAttachmentPath { get; set; }
}

public class CreateStorageTransferRequestDto
{
    [Required(ErrorMessage = "مكان التخزين مطلوب.")]
    public int StorageLocationId { get; set; }

    public DateTime? TransactionDate { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// الرقم المرجعي التتبعي للإنتاج المشترك الصادر من قسم المعمل
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    public string? ItemsJson { get; set; }
    [Required(ErrorMessage = "يجب تحديد مادة واحدة على الأقل.")]
    [MinLength(1, ErrorMessage = "يجب تحديد مادة واحدة على الأقل.")]
    public List<StorageOperationLineInputDto> Items { get; set; } = new();

    // 2 Required Attachments for Transfer
    public IFormFile? DocumentAttachment { get; set; }
    public string? DocumentAttachmentPath { get; set; }

    public IFormFile? WeighbridgeAttachment { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }
}

public class StorageOperationItemDto
{
    public int Id { get; set; }
    public int StorageOperationId { get; set; }
    public int RawMaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class StorageOperationDto
{
    public int Id { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public int TransactionNumber { get; set; }
    public string TransactionFormattedNumber => $"#{TransactionNumber}";
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// الرقم المرجعي التتبعي للإنتاج المشترك الصادر من قسم المعمل
    /// </summary>
    public int? ProductionReferenceNumber { get; set; }

    public int StorageLocationId { get; set; }
    public string StorageLocationName { get; set; } = string.Empty;

    public string? MaterialType { get; set; }
    public string? MaterialTypeAr => MaterialProcessType.ToArabic(MaterialType);

    // Import
    public string? Source { get; set; }
    public string? SourcePhone { get; set; }

    // Export
    public int? ClientId { get; set; }
    public string? ClientName { get; set; }
    public int? DestinationId { get; set; }
    public string? DestinationName { get; set; }
    public string? ClientPhone { get; set; }

    // Transfer
    public string? DestinationDepartment { get; set; }

    // Weighing & Discrepancy
    public decimal? ExternalReportedWeight { get; set; }
    public decimal? InternalLoadedWeight { get; set; }
    public decimal? InternalEmptyWeight { get; set; }
    public decimal? InternalNetWeight { get; set; }
    public DateTime? InternalWeighingTime { get; set; }
    public string? WeighbridgeReference { get; set; }
    public string? WeighingResponsibleEmployee { get; set; }
    public decimal? WeightDifference { get; set; }
    public decimal? WeightDifferencePercentage { get; set; }
    public bool IsDiscrepancyFlagged { get; set; }
    public string? DiscrepancyExplanation { get; set; }
    public int? TransportationId { get; set; }

    public string ResponsibleEmployee { get; set; } = string.Empty;
    public Guid? ResponsibleUserId { get; set; }
    public string? Notes { get; set; }

    public string ApprovalStatus { get; set; } = "PENDING";
    public string? ReviewedBy { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }

    public bool IsInventoryPosted { get; set; }
    public DateTime? InventoryPostedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }

    public decimal TotalQuantity { get; set; }
    public List<StorageOperationItemDto> Items { get; set; } = new();

    // Attachments
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? WeighbridgeFileType { get; set; }
    public long WeighbridgeFileSize { get; set; }

    public string? QocAttachmentPath { get; set; }
    public string? QocFileName { get; set; }
    public string? QocFileType { get; set; }
    public long QocFileSize { get; set; }

    public string? DocumentAttachmentPath { get; set; }
    public string? DocumentFileName { get; set; }
    public string? DocumentFileType { get; set; }
    public long DocumentFileSize { get; set; }

    public List<StorageAttachmentItemDto> Attachments { get; set; } = new();

    /// <summary>
    /// دفعات التشغيل المرتبطة بعمليات التحويل للإنتاج ومخرجات الهالك والمنتج النهائي
    /// </summary>
    public List<ProductionWorkInProcessDto> ProductionBatches { get; set; } = new();
}

public class ProductionWorkInProcessDto
{
    public int Id { get; set; }
    public int StorageOperationId { get; set; }
    public int StorageOperationItemId { get; set; }
    public int RawMaterialId { get; set; }
    public string MaterialName { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public decimal MaterialQuantityIssued { get; set; }
    public string SourceStorageLocation { get; set; } = string.Empty;
    public DateTime TransferDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? FinalProductName { get; set; }
    public decimal? FinalProductQuantity { get; set; }
    public decimal? WasteQuantity { get; set; }
    public decimal? WastePercentage { get; set; }

    // Waste classification and recoverable final product
    public string WasteClassification { get; set; } = "ORDINARY_WASTE";
    public string WasteClassificationAr => WasteClassification switch
    {
        "FINAL_PRODUCT" => "منتج نهائي",
        "RECOVERABLE_MATERIAL" => "مادة مستردة / إعادة تدوير",
        _ => "هالك عادي"
    };
    public string? RecoverableProductName { get; set; }
    public int? RecoverableMaterialId { get; set; }
    public decimal? RecoverableQuantity { get; set; }
    public string RecoverableUnit { get; set; } = "طن";
    public int? DestinationStorageLocationId { get; set; }
    public string? DestinationStorageLocationName { get; set; }
    public bool IsRecoveredStockPosted { get; set; }
    public DateTime? RecoveredStockPostedAt { get; set; }
    public string? OutputResponsibleEmployee { get; set; }
    public string? ClassificationHistory { get; set; }

    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RecordProductionOutputDto
{
    public string? FinalProductName { get; set; }

    [Range(0.0, 10000000.0, ErrorMessage = "كمية المنتج النهائي يجب أن تكون صفر أو أكبر.")]
    public decimal? FinalProductQuantity { get; set; }

    /// <summary>
    /// ORDINARY_WASTE | RECOVERABLE_MATERIAL | FINAL_PRODUCT
    /// </summary>
    public string WasteClassification { get; set; } = "ORDINARY_WASTE";

    public string? RecoverableProductName { get; set; }
    public int? RecoverableMaterialId { get; set; }
    public decimal? RecoverableQuantity { get; set; }
    public string? RecoverableUnit { get; set; } = "طن";
    public int? DestinationStorageLocationId { get; set; }
    public string? ResponsibleEmployee { get; set; }

    public string? Notes { get; set; }
}

public class RejectStorageOperationDto
{
    [Required(ErrorMessage = "سبب الرفض مطلوب.")]
    public string Reason { get; set; } = string.Empty;
}


