using System;
using System.Collections.Generic;

namespace FaceStone.Web.Models;

public class StorageTransaction
{
    public int Id { get; set; }

    /// <summary>
    /// رقم الحركة - Storage movement / transaction number (User enters manually)
    /// </summary>
    public int MovementNumber { get; set; }

    /// <summary>
    /// التاريخ - Date and time of storage movement
    /// </summary>
    public DateTime TransactionDate { get; set; }

    /// <summary>
    /// نوع الخدمة - Exactly: إضافة | صرف | تحويل | تسوية
    /// </summary>
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>
    /// رقم المرجع - Unique reference number
    /// </summary>
    public int ReferenceNumber { get; set; }

    /// <summary>
    /// رقم أمر الشراء (PO)
    /// </summary>
    public string PONumber { get; set; } = string.Empty;

    /// <summary>
    /// الكمية الواردة - Incoming material quantity
    /// </summary>
    public decimal IncomingQuantity { get; set; }

    /// <summary>
    /// الكمية المنصرفة - Outgoing material quantity
    /// </summary>
    public decimal OutgoingQuantity { get; set; }

    /// <summary>
    /// الرصيد الافتتاحي - Opening inventory balance
    /// </summary>
    public decimal OpeningBalance { get; set; }

    /// <summary>
    /// الرصيد الختامي - Closing inventory balance (Opening + Incoming - Outgoing)
    /// </summary>
    public decimal ClosingBalance { get; set; }

    /// <summary>
    /// مكان التخزين - Storage location
    /// </summary>
    public string StorageLocation { get; set; } = string.Empty;

    /// <summary>
    /// مكان التخزين المحول إليه - Destination storage location (Used when ServiceType is تحويل)
    /// </summary>
    public string? DestinationStorageLocation { get; set; }

    /// <summary>
    /// اسم الموظف المستلم - Receiving employee name
    /// </summary>
    public string ReceivingEmployee { get; set; } = string.Empty;

    /// <summary>
    /// ملاحظات - Optional notes
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Relational navigation: One storage movement can be associated with multiple transportation movements
    /// </summary>
    public ICollection<StorageTransactionMovement> TransportationMovements { get; set; } = new List<StorageTransactionMovement>();
}
