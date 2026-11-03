using Identity.DAL;
using Identity.DAL.Repositories;
using Identity.Models.Entities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Data.Common;

namespace Identity.Authentication.Tests;

internal static partial class SqlIdentityTests
{
    public static async Task Run()
    {
        // Stop before opening SQL: both real repository queries must translate first.
        var queryOptions = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseSqlServer("Server=unused;Database=IdentityModelOnly;Integrated Security=true;TrustServerCertificate=true")
            .AddInterceptors(new TranslationProbe()).Options;
        var queryRepository = new UserRepository(new ProbeContextFactory(queryOptions));
        await CheckTranslation(() => queryRepository.FindByUsernameAsync("translation-probe", default));
        await CheckTranslation(() => queryRepository.FindByIdAsync(1, default));
        using (var model = IdentityDbContext.Create("Server=unused;Database=IdentityModelOnly;Integrated Security=true;TrustServerCertificate=true"))
        {
            var user = model.Model.FindEntityType(typeof(User))!;
            Check(user.GetTableName() == "User" && user.FindProperty(nameof(User.RowVersion))!.IsConcurrencyToken, "user mapping and version");
            Check(user.FindProperty(nameof(User.Email))!.GetMaxLength() == 254, "email schema length");
            var query = model.Users.Select(x => new { x.UserId, x.Role.RoleName }).ToQueryString();
            Check(query.Contains("JOIN") && query.Contains("[Role]"), "role navigation translates");
        }
        var server = Environment.GetEnvironmentVariable("EWMS_IDENTITY_TEST_SERVER");
        if (string.IsNullOrWhiteSpace(server)) return;
        // This test owns only its uniquely named temporary database.
        var connection = new SqlConnectionStringBuilder { DataSource = server,
            InitialCatalog = "EWMS_EfIdentityTest_" + Guid.NewGuid().ToString("N"),
            IntegratedSecurity = true, TrustServerCertificate = true }.ConnectionString;
        await using var db = IdentityDbContext.Create(connection);
        try
        {
            await db.Database.EnsureCreatedAsync();
            var role = new Role { RoleName = "ADMIN" };
            var entity = new User { Username = "ef-test", FullName = "EF Test", PasswordHash = "first", Role = role };
            db.Users.Add(entity);
            await db.SaveChangesAsync();
            var repository = new UserRepository(connection);
            var initial = await repository.FindByUsernameAsync(entity.Username, default);
            Check(initial is { Role: "ADMIN", PasswordHash: "first" } && initial.RowVersion.Length == 8, "user and role DTO projection");
            await repository.UpdatePasswordHashAsync(initial!, "second", default);
            // A stale rehash must never overwrite a newer password.
            await repository.UpdatePasswordHashAsync(initial!, "stale", default);
            var updated = await repository.FindByIdAsync(entity.UserId, default);
            Check(updated!.PasswordHash == "second" && !updated.RowVersion.SequenceEqual(initial!.RowVersion), "rowversion protects password update");
            var displays = await repository.ListDisplayNamesAsync(default);
            Check(displays.Length == 1 && displays[0].FullName == entity.FullName, "display-only projection");
            Check(await repository.FindByUsernameAsync("missing", default) is null, "missing user");
            Console.WriteLine("PASS Identity EF SQL user/role reads and password concurrency.");
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("FAIL: " + message); }

    private static async Task CheckTranslation(Func<Task<Identity.Models.Authentication.AuthUser?>> query)
    {
        try { await query(); }
        catch (TranslationCompleteException) { return; }
        throw new Exception("FAIL: expected SQL connection probe.");
    }
}
