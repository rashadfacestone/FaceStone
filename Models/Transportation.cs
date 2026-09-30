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

    public string Destination { get; set; } = string.Empty;

    public string TransactionStatus { get; set; } = string.Empty;

    public string ReceivingStatus { get; set; } = string.Empty;

    public string? Notes { get; set; }

    public DateTime DateOfSubmit { get; set; }
}
