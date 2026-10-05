using System;

namespace FaceStone.Web.Models;

/// <summary>
/// Financial record and payroll parameters for an employee (linked to UserAccount).
/// </summary>
public class EmployeeFinancialRecord
{
    public int Id { get; set; }

    /// <summary>
    /// المعرف الفريد للموظف في جدول Users
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// الراتب الأساسي (ج.م)
    /// </summary>
    public decimal BasicSalary { get; set; }

    /// <summary>
    /// البدلات والحوافز (ج.م)
    /// </summary>
    public decimal Allowances { get; set; }

    /// <summary>
    /// الاستقطاعات والتأمينات (ج.م)
    /// </summary>
    public decimal InsuranceDeductions { get; set; }

    /// <summary>
    /// صافي الراتب المستحق = الأساسي + البدلات − الاستقطاعات
    /// </summary>
    public decimal NetSalary { get; set; }

    /// <summary>
    /// تاريخ سريان الراتب
    /// </summary>
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// طريقة السداد: تحويل بنكي / نقدي
    /// </summary>
    public string PaymentMethod { get; set; } = "Bank Transfer";

    /// <summary>
    /// رقم الحساب البنكي / IBAN
    /// </summary>
    public string? BankAccountNumber { get; set; }

    /// <summary>
    /// ملاحظات
    /// </summary>
    public string? Notes { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public UserAccount? User { get; set; }
}
