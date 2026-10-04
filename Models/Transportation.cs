namespace FaceStone.Web.Models;

public class Transportation
{
    public int Id { get; set; }

    public int TransactionNumber { get; set; }

    public DateTime TransactionDate { get; set; }

    public string CarNumber { get; set; } = string.Empty;

    public string DriverName { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string PONumber { get; set; } = string.Empty;

    public string MaterialType { get; set; } = string.Empty;

    public decimal WeightBeforeLoading { get; set; }

    public decimal WeightWhenEnteringFactory { get; set; }

    public decimal WeightWhenExitingFactory { get; set; }

    public decimal ExpectedWeight { get; set; }

    public decimal NetWeight { get; set; }

    /// <summary>
    /// اسم الشركة - Company name retrieved automatically from PO
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// الوزن الداخلي = الوزن الداخلي قائم - الوزن الداخلي فارغ
    /// </summary>
    public decimal InternalWeight { get; set; }

    /// <summary>
    /// الوزن الخارجي قائم - External loaded weighbridge measurement
    /// </summary>
    public decimal ExternalLoadedWeight { get; set; }

    /// <summary>
    /// الوزن الخارجي فارغ - External empty weighbridge measurement
    /// </summary>
    public decimal ExternalEmptyWeight { get; set; }

    /// <summary>
    /// الوزن الخارجي = الوزن الخارجي قائم - الوزن الخارجي فارغ
    /// </summary>
    public decimal ExternalNetWeight { get; set; }

    /// <summary>
    /// كمية هالك = ABS(الوزن الداخلي - الوزن الخارجي)
    /// </summary>
    public decimal WasteQuantity { get; set; }

    /// <summary>
    /// رقم هاتف السائق - Optional driver contact number
    /// </summary>
    public string? DriverPhone { get; set; }

    /// <summary>
    /// تاريخ المغادرة - Date and time truck left factory
    /// </summary>
    public DateTime? DepartureTime { get; set; }

    /// <summary>
    /// تاريخ الوصول - Date and time truck arrived at destination
    /// </summary>
    public DateTime? ArrivalTime { get; set; }

    /// <summary>
    /// المسؤول عن الشحن - Fixed enum option: محمد ناجح
    /// </summary>
    public string ShippingSupervisor { get; set; } = "محمد ناجح";

    /// <summary>
    /// مسؤول القسم - Fixed enum option: ازهر محمد
    /// </summary>
    public string DepartmentSupervisor { get; set; } = "ازهر محمد";

    /// <summary>
    /// اسم مستلم الشحنة - Free-text recipient name
    /// </summary>
    public string? RecipientName { get; set; }

    /// <summary>
    /// مسمى مستلم الشحنة - Controlled enum recipient title
    /// </summary>
    public string? RecipientTitle { get; set; }

    /// <summary>
    /// تاريخ الاستلام - Shipment receipt date and time
    /// </summary>
    public DateTime? ReceivedDate { get; set; }

    /// <summary>
    /// كود المرجع للميزان الداخلي - Unique auto-generated internal weighbridge code
    /// </summary>
    public string? InternalWeighbridgeCode { get; set; }

    /// <summary>
    /// تاريخ ووقت الوزن الفارغ للميزان الداخلي
    /// </summary>
    public DateTime? InternalEmptyWeightTime { get; set; }

    /// <summary>
    /// تاريخ ووقت الوزن القائم للميزان الداخلي (بداية وقت الشحن)
    /// </summary>
    public DateTime? InternalLoadedWeightTime { get; set; }

    /// <summary>
    /// كود المرجع للميزان الخارجي - Unique auto-generated external weighbridge code
    /// </summary>
    public string? ExternalWeighbridgeCode { get; set; }

    /// <summary>
    /// تاريخ ووقت الوزن الفارغ للميزان الخارجي
    /// </summary>
    public DateTime? ExternalEmptyWeightTime { get; set; }

    /// <summary>
    /// تاريخ ووقت الوزن القائم للميزان الخارجي (نهاية وقت الشحن)
    /// </summary>
    public DateTime? ExternalLoadedWeightTime { get; set; }

    /// <summary>
    /// مدة الشحن المحسوبة بالدقائق = تاريخ ووقت الوزن القائم الخارجي - تاريخ ووقت الوزن القائم الداخلي
    /// </summary>
    public int? ShippingDurationMinutes { get; set; }

    public string Destination { get; set; } = string.Empty;

    public string TransactionStatus { get; set; } = string.Empty;

    public string ReceivingStatus { get; set; } = string.Empty;

    public string? Notes { get; set; }
 
    /// <summary>
    /// اكرامية - Tip/Gratuity field collected for Transportation transactions (decimal)
    /// </summary>
    public decimal Ikramiya { get; set; }

    /// <summary>
    /// العهدة - Custody / Petty Cash amount collected for Transportation transactions (decimal, required)
    /// </summary>
    public decimal CustodyAmount { get; set; }

    /// <summary>
    /// Path to mandatory invoice document (PDF/JPG) stored in uploads/transportation
    /// </summary>
    public string? InvoiceAttachmentPath { get; set; }

    public string? InvoiceFileName { get; set; }

    public string? InvoiceFileType { get; set; }

    public long InvoiceFileSize { get; set; }

    /// <summary>
    /// مرفق الميزان - Weighbridge section mandatory attachment (PDF/JPG)
    /// </summary>
    public string? WeighbridgeAttachmentPath { get; set; }

    public string? WeighbridgeFileName { get; set; }

    public string? WeighbridgeFileType { get; set; }

    public long WeighbridgeFileSize { get; set; }

    /// <summary>
    /// مرفق يومية التوريد - Daily Supply section mandatory attachment (PDF/JPG)
    /// </summary>
    public string? DailySupplyAttachmentPath { get; set; }

    public string? DailySupplyFileName { get; set; }

    public string? DailySupplyFileType { get; set; }

    public long DailySupplyFileSize { get; set; }

    /// <summary>
    /// مرفق بوليصة الشحن - Bill of Lading section mandatory attachment (PDF/JPG)
    /// </summary>
    public string? BillOfLadingAttachmentPath { get; set; }

    public string? BillOfLadingFileName { get; set; }

    public string? BillOfLadingFileType { get; set; }

    public long BillOfLadingFileSize { get; set; }

    public string? SubmittedBy { get; set; }

    public Guid? SubmittedByUserId { get; set; }

    public int? PurchaseOrderId { get; set; }

    public PurchaseOrder? PurchaseOrder { get; set; }

    public DateTime DateOfSubmit { get; set; }
}
