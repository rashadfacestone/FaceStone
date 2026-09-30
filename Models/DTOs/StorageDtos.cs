using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models.DTOs;

public class CreateStorageTransactionDto
{
    [Required(ErrorMessage = "رقم الحركة مطلوب.")]
    [Range(1, int.MaxValue, ErrorMessage = "يرجى إدخال رقم حركة صحيح.")]
    public int MovementNumber { get; set; }

    [Required(ErrorMessage = "التاريخ مطلوب.")]
    public DateTime? TransactionDate { get; set; }

    [Required(ErrorMessage = "يرجى اختيار نوع الخدمة.")]
    [RegularExpression("^(إضافة|صرف|تحويل|تسوية)$", ErrorMessage = "نوع الخدمة يجب أن يكون: إضافة، صرف، تحويل، أو تسوية.")]
    public string ServiceType { get; set; } = string.Empty;

    [Required(ErrorMessage = "رقم المرجع مطلوب.")]
    [Range(1, int.MaxValue, ErrorMessage = "يرجى إدخال رقم مرجع صحيح.")]
    public int ReferenceNumber { get; set; }

    [Required(ErrorMessage = "رقم أمر الشراء (PO) مطلوب.")]
    [MaxLength(100, ErrorMessage = "رقم أمر الشراء لا يمكن أن يتجاوز 100 حرف.")]
    public string PONumber { get; set; } = string.Empty;

    /// <summary>
    /// قائمة أرقام النقلات المرتبطة بالحركة (رقم النقلة)
    /// </summary>
    [Required(ErrorMessage = "يجب تحديد رقم نقلة واحد على الأقل.")]
    public List<int> TransportationMovementNumbers { get; set; } = new();

    [Range(0, 1000000.0, ErrorMessage = "الكمية الواردة يجب أن تكون قيمة موجبة أو صفر.")]
    public decimal IncomingQuantity { get; set; }

    [Range(0, 1000000.0, ErrorMessage = "الكمية المنصرفة يجب أن تكون قيمة موجبة أو صفر.")]
    public decimal OutgoingQuantity { get; set; }

    public decimal OpeningBalance { get; set; }

    public decimal ClosingBalance { get; set; }

    [Required(ErrorMessage = "يرجى تحديد مكان التخزين.")]
    [MaxLength(200, ErrorMessage = "مكان التخزين لا يمكن أن يتجاوز 200 حرف.")]
    public string StorageLocation { get; set; } = string.Empty;

    [MaxLength(200, ErrorMessage = "مكان التخزين المحول إليه لا يمكن أن يتجاوز 200 حرف.")]
    public string? DestinationStorageLocation { get; set; }

    [Required(ErrorMessage = "اسم الموظف المستلم مطلوب.")]
    [MaxLength(150, ErrorMessage = "اسم الموظف المستلم لا يمكن أن يتجاوز 150 حرف.")]
    public string ReceivingEmployee { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "الملاحظات لا يمكن أن تتجاوز 1000 حرف.")]
    public string? Notes { get; set; }
}

public class StorageTransactionDto
{
    public int Id { get; set; }
    public int MovementNumber { get; set; }
    public DateTime TransactionDate { get; set; }
    public string ServiceType { get; set; } = string.Empty;
    public int ReferenceNumber { get; set; }
    public string PONumber { get; set; } = string.Empty;
    public List<int> TransportationMovementNumbers { get; set; } = new();
    public decimal IncomingQuantity { get; set; }
    public decimal OutgoingQuantity { get; set; }
    public decimal OpeningBalance { get; set; }
    public decimal ClosingBalance { get; set; }
    public string StorageLocation { get; set; } = string.Empty;
    public string? DestinationStorageLocation { get; set; }
    public string ReceivingEmployee { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StorageConfigDto
{
    public List<string> ServiceTypes { get; set; } = new();
    public List<string> StorageLocations { get; set; } = new();
    public List<EmployeeSummaryDto> Employees { get; set; } = new();
    public List<TransportationMovementSummaryDto> AvailableTransportationMovements { get; set; } = new();
    public decimal CurrentOverallBalance { get; set; }
}

public class EmployeeSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
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

public class CheckReferenceResultDto
{
    public int ReferenceNumber { get; set; }
    public bool Exists { get; set; }
    public string? Message { get; set; }
}

public class CurrentBalanceResultDto
{
    public string StorageLocation { get; set; } = string.Empty;
    public decimal CurrentBalance { get; set; }
    public DateTime? LastUpdated { get; set; }
}
