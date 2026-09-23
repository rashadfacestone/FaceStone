using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using FaceStone.Web.Models;

namespace FaceStone.Web.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FaceStoneDbContext>();

        try
        {
            await db.Database.EnsureCreatedAsync();

            if (!await db.Users.AnyAsync())
            {
                logger.LogInformation("Database is empty. Seeding initial accounts...");

                var defaultAccounts = new List<UserAccount>
                {
                    new UserAccount
                    {
                        Id = Guid.NewGuid(),
                        FullName = "Sarah Mitchell",
                        Username = "sarah_acc",
                        Email = "sarah@facestone.com",
                        PhoneNumber = "+20 10 1122 3344",
                        CountryCode = "+20",
                        Role = "Accountant",
                        PasswordHash = HashPassword("SecretPass123!"),
                        CreatedAt = DateTime.UtcNow
                    },
                    new UserAccount
                    {
                        Id = Guid.NewGuid(),
                        FullName = "Alexander Wright",
                        Username = "alex_stone",
                        Email = "alex@facestone.com",
                        PhoneNumber = "+20 10 5566 7788",
                        CountryCode = "+20",
                        Role = "Senior Structural Engineer",
                        PasswordHash = HashPassword("Password123!"),
                        CreatedAt = DateTime.UtcNow
                    }
                };

                await db.Users.AddRangeAsync(defaultAccounts);
                await db.SaveChangesAsync();
                logger.LogInformation("Database seeded successfully with initial accounts.");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "DbInitializer completed with notice.");
        }
    }

    private static string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password + "_FaceStoneSalt_2026"));
        return Convert.ToBase64String(bytes);
    }
}
