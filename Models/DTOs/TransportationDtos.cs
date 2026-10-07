using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace FaceStone.Web.Models.DTOs;

public class CreateTransportationDto
{
    /// <summary>
    /// رقم الحركة - Automatically generated if 0 or omitted
    /// </summary>
    public int TransactionNumber { get; set; }

    [Required(ErrorMessage = "تاريخ الشحنة مطلوب.")]
    public DateTime? TransactionDate { get; set; }

    [Required(ErrorMessage = "رقم السيارة مطلوب.")]
    [MaxLength(50, ErrorMessage = "رقم السيارة لا يتجاوز 50 حرف.")]
    public string CarNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "اسم السائق مطلوب.")]
    [MaxLength(150, ErrorMessage = "اسم السائق لا يتجاوز 150 حرف.")]
    public string DriverName { get; set; } = string.Empty;

    /// <summary>
    /// المصدر - يتم تعيينه آلياً ومحصوراً في: Face stone (Full Concrete Solutions)
    /// </summary>
    public string? Source { get; set; }

    [Required(ErrorMessage = "رقم أمر الشراء مطلوب.")]
    [MaxLength(100, ErrorMessage = "رقم أمر الشراء لا يتجاوز 100 حرف.")]
    public string PONumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "نوع الخامة مطلوب.")]
    [RegularExpression("^(powder|slurry)$", ErrorMessage = "نوع الخامة يجب أن يكون إما powder أو slurry فقط.")]
    public string MaterialType { get; set; } = string.Empty;

    [Required(ErrorMessage = "الوزن الداخلي فارغ مطلوب.")]
    [Range(0.001, 1000000.0, ErrorMessage = "الوزن الداخلي فارغ يجب أن يكون رقم موجب.")]
    public decimal WeightBeforeLoading { get; set; }
    public decimal InternalEmptyWeight
    {
        get => WeightBeforeLoading;
        set => WeightBeforeLoading = value;
    }

    [Required(ErrorMessage = "الوزن الداخلي قائم مطلوب.")]
    [Range(0.001, 1000000.0, ErrorMessage = "الوزن الداخلي قائم يجب أن يكون رقم موجب.")]
    public decimal WeightWhenExitingFactory { get; set; }
    public decimal InternalLoadedWeight
    {
        get => WeightWhenExitingFactory;
        set => WeightWhenExitingFactory = value;
    }

    public decimal WeightWhenEnteringFactory { get; set; }

    [Required(ErrorMessage = "الوزن الخارجي قائم مطلوب.")]
    [Range(0.001, 1000000.0, ErrorMessage = "الوزن الخارجي قائم يجب أن يكون رقم موجب.")]
    public decimal ExternalLoadedWeight { get; set; }

    [Required(ErrorMessage = "الوزن الخارجي فارغ مطلوب.")]
    [Range(0.001, 1000000.0, ErrorMessage = "الوزن الخارجي فارغ يجب أن يكون رقم موجب.")]
    public decimal ExternalEmptyWeight { get; set; }

    [MaxLength(30, ErrorMessage = "رقم هاتف السائق لا يتجاوز 30 حرف.")]
    public string? DriverPhone { get; set; }

    public DateTime? DepartureTime { get; set; }

    public DateTime? ArrivalTime { get; set; }

    public string ShippingSupervisor { get; set; } = "محمد ناجح";

    public string DepartmentSupervisor { get; set; } = "ازهر محمد";

    [MaxLength(150, ErrorMessage = "اسم مستلم الشحنة لا يتجاوز 150 حرف.")]
    public string? RecipientName { get; set; }

    [MaxLength(100, ErrorMessage = "مسمى مستلم الشحنة لا يتجاوز 100 حرف.")]
    public string? RecipientTitle { get; set; }

    [Required(ErrorMessage = "حالة الاستلام مطلوبة (يرجى تحديد خيار واحد على الأقل).")]
    public string ReceivingStatus { get; set; } = "safe";

    public DateTime? ReceivedDate { get; set; }

    [Required(ErrorMessage = "رقم مرجع الميزان الداخلي مطلوب.")]
    [MaxLength(100, ErrorMessage = "رقم مرجع الميزان الداخلي لا يتجاوز 100 حرف.")]
    public string InternalWeighbridgeCode { get; set; } = string.Empty;

    public DateTime? InternalEmptyWeightTime { get; set; }

    [Required(ErrorMessage = "تاريخ ووقت الوزن القائم الداخلي مطلوب.")]
    public DateTime? InternalLoadedWeightTime { get; set; }

    [Required(ErrorMessage = "رقم مرجع الميزان الخارجي مطلوب.")]
    [MaxLength(100, ErrorMessage = "رقم مرجع الميزان الخارجي لا يتجاوز 100 حرف.")]
    public string ExternalWeighbridgeCode { get; set; } = string.Empty;

    public DateTime? ExternalEmptyWeightTime { get; set; }

    [Required(ErrorMessage = "تاريخ ووقت الوزن القائم الخارجي مطلوب.")]
    public DateTime? ExternalLoadedWeightTime { get; set; }

    [MaxLength(200, ErrorMessage = "الوجهة لا تتجاوز 200 حرف.")]
    public string? Destination { get; set; }

    [MaxLength(50, ErrorMessage = "حالة النقلة لا تتجاوز 50 حرف.")]
    public string TransactionStatus { get; set; } = "Completed";

    [MaxLength(1000, ErrorMessage = "الملاحظات لا تتجاوز 1000 حرف.")]
    public string? Notes { get; set; }

    public decimal Ikramiya { get; set; }

    [Required(ErrorMessage = "العهدة مطلوبة.")]
    [Range(0, 100000000.0, ErrorMessage = "قيمة العهدة يجب أن تكون رقماً موجباً أو صفراً.")]
    public decimal CustodyAmount { get; set; }

    public IFormFile? InvoiceFile { get; set; }

    /// <summary>
    /// مرفق الميزان الإلزامي - Weighbridge section mandatory attachment (PDF/JPG/JPEG)
    /// </summary>
    public IFormFile? WeighbridgeAttachment { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }

    /// <summary>
    /// مرفق يومية التوريد الإلزامي - Daily supply section mandatory attachment (PDF/JPG/JPEG)
    /// </summary>
    public IFormFile? DailySupplyAttachment { get; set; }
    public string? DailySupplyAttachmentPath { get; set; }

    /// <summary>
    /// مرفق بوليصة الشحن الإلزامي - Bill of lading section mandatory attachment (PDF/JPG/JPEG)
    /// </summary>
    public IFormFile? BillOfLadingAttachment { get; set; }
    public string? BillOfLadingAttachmentPath { get; set; }
    public string? InvoiceAttachmentPath { get; set; }
}

public class TransportationDto
{
    public int Id { get; set; }
    public int TransactionNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string CarNumber { get; set; } = string.Empty;
    public string DriverName { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string PONumber { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string MaterialType { get; set; } = string.Empty;
    public decimal WeightBeforeLoading { get; set; }
    public decimal WeightWhenEnteringFactory { get; set; }
    public decimal WeightWhenExitingFactory { get; set; }
    public decimal ExpectedWeight { get; set; }
    public decimal InternalWeight { get; set; }
    public decimal ExternalLoadedWeight { get; set; }
    public decimal ExternalEmptyWeight { get; set; }
    public decimal ExternalNetWeight { get; set; }
    public decimal WasteQuantity { get; set; }
    public decimal NetWeight { get; set; }
    public decimal Quantity => NetWeight;
    public decimal ActualWeight => NetWeight;
    public string? DriverPhone { get; set; }
    public DateTime? DepartureTime { get; set; }
    public DateTime? ArrivalTime { get; set; }
    public string ShippingSupervisor { get; set; } = string.Empty;
    public string DepartmentSupervisor { get; set; } = string.Empty;
    public string? RecipientName { get; set; }
    public string? RecipientTitle { get; set; }
    public DateTime? ReceivedDate { get; set; }
    public string? InternalWeighbridgeCode { get; set; }
    public DateTime? InternalEmptyWeightTime { get; set; }
    public DateTime? InternalLoadedWeightTime { get; set; }
    public string? ExternalWeighbridgeCode { get; set; }
    public DateTime? ExternalEmptyWeightTime { get; set; }
    public DateTime? ExternalLoadedWeightTime { get; set; }
    public int? ShippingDurationMinutes { get; set; }
    public string Destination { get; set; } = string.Empty;
    public string TransactionStatus { get; set; } = string.Empty;
    public string ReceivingStatus { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public decimal Ikramiya { get; set; }
    public decimal CustodyAmount { get; set; }
    public string? InvoiceAttachmentPath { get; set; }
    public string? InvoiceFileName { get; set; }
    public string? InvoiceFileType { get; set; }
    public long InvoiceFileSize { get; set; }
    public string? WeighbridgeAttachmentPath { get; set; }
    public string? WeighbridgeFileName { get; set; }
    public string? WeighbridgeFileType { get; set; }
    public long WeighbridgeFileSize { get; set; }
    public string? DailySupplyAttachmentPath { get; set; }
    public string? DailySupplyFileName { get; set; }
    public string? DailySupplyFileType { get; set; }
    public long DailySupplyFileSize { get; set; }
    public string? BillOfLadingAttachmentPath { get; set; }
    public string? BillOfLadingFileName { get; set; }
    public string? BillOfLadingFileType { get; set; }
    public long BillOfLadingFileSize { get; set; }
    public string? SubmittedBy { get; set; }
    public decimal PoRemainingQuantityAfter { get; set; }
    public DateTime DateOfSubmit { get; set; }
    public string ApprovalStatus { get; set; } = "PENDING";
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectionReason { get; set; }
}

public class TransportationConfigDto
{
    public List<string> MaterialTypes { get; set; } = new();
    public List<string> Destinations { get; set; } = new();
    public List<string> TransactionStatuses { get; set; } = new();
    public List<string> ReceivingStatuses { get; set; } = new();
    public List<string> ShippingSupervisors { get; set; } = new();
    public List<string> DepartmentSupervisors { get; set; } = new();
    public List<string> RecipientTitles { get; set; } = new();
    public List<string> Drivers { get; set; } = new();
    public int NextTransactionNumber { get; set; }
    public string NextInternalWeighbridgeCode { get; set; } = string.Empty;
    public string NextExternalWeighbridgeCode { get; set; } = string.Empty;
    public string FixedSource { get; set; } = "Face stone (Full Concrete Solutions)";
}

public class PoLookupDto
{
    public int Id { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Supplier { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public decimal RemainingQuantity { get; set; }
    public decimal PricePerTon { get; set; }
    public decimal TotalValue { get; set; }
    public DateTime OrderDate { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Notes { get; set; }
    public int NextTransactionNumber { get; set; } = 1;
}
