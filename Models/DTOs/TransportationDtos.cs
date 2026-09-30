using System.ComponentModel.DataAnnotations;

namespace FaceStone.Web.Models.DTOs;

public class CreateTransportationDto
{
    [Required(ErrorMessage = "Transaction Number is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Please enter a valid positive integer for Transaction Number.")]
    public int TransactionNumber { get; set; }

    [Required(ErrorMessage = "Transaction Date is required.")]
    public DateTime? TransactionDate { get; set; }

    [Required(ErrorMessage = "Car Number is required.")]
    [MaxLength(50, ErrorMessage = "Car Number cannot exceed 50 characters.")]
    public string CarNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Driver's Name is required.")]
    [MaxLength(150, ErrorMessage = "Driver's Name cannot exceed 150 characters.")]
    public string DriverName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Source is required.")]
    [MaxLength(200, ErrorMessage = "Source cannot exceed 200 characters.")]
    public string Source { get; set; } = string.Empty;

    [Required(ErrorMessage = "PO Number is required.")]
    [MaxLength(100, ErrorMessage = "PO Number cannot exceed 100 characters.")]
    public string PONumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select Material Type.")]
    [MaxLength(100, ErrorMessage = "Material Type cannot exceed 100 characters.")]
    public string MaterialType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Weight Before Loading is required.")]
    [Range(0.001, 1000000.0, ErrorMessage = "Weight Before Loading must be a positive number.")]
    public decimal WeightBeforeLoading { get; set; }

    [Required(ErrorMessage = "Weight When Entering Factory is required.")]
    [Range(0.001, 1000000.0, ErrorMessage = "Weight When Entering Factory must be a positive number.")]
    public decimal WeightWhenEnteringFactory { get; set; }

    [Required(ErrorMessage = "Weight When Exiting Factory is required.")]
    [Range(0.001, 1000000.0, ErrorMessage = "Weight When Exiting Factory must be a positive number.")]
    public decimal WeightWhenExitingFactory { get; set; }

    [Required(ErrorMessage = "Please select Destination.")]
    [MaxLength(200, ErrorMessage = "Destination cannot exceed 200 characters.")]
    public string Destination { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select Transaction Status.")]
    [MaxLength(50, ErrorMessage = "Transaction Status cannot exceed 50 characters.")]
    public string TransactionStatus { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please select Receiving Status.")]
    [MaxLength(50, ErrorMessage = "Receiving Status cannot exceed 50 characters.")]
    public string ReceivingStatus { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }
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

public class TransportationConfigDto
{
    public List<string> MaterialTypes { get; set; } = new();
    public List<string> Destinations { get; set; } = new();
    public List<string> TransactionStatuses { get; set; } = new();
    public List<string> ReceivingStatuses { get; set; } = new();
}
