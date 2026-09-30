namespace FaceStone.Web.Models;

public class Management
{
    public string TransactionNumber { get; set; } = string.Empty;

    public DateTime Date { get; set; }

    public string? PO { get; set; }

    public string MaterialType { get; set; } = string.Empty;

    public int NumberOfTransactions { get; set; }

    public decimal TotalReceivedAmount { get; set; }

    public string CarPlatesNumber { get; set; } = string.Empty;

    public decimal AcceptedQuantity { get; set; }

    public decimal UnacceptedPendingQuantity { get; set; }

    public string DeliveryStatus { get; set; } = string.Empty;

    public string Destination { get; set; } = string.Empty;

    public string? Notes { get; set; }
}
