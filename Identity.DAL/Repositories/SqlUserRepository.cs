using Identity.BLL.Authentication;
using Identity.Models.Authentication;
using Identity.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace Identity.DAL.Repositories;

public sealed class SqlUserRepository(IDbContextFactory<IdentityDbContext> contexts) : IUserRepository
{
    public SqlUserRepository(string connectionString) : this(new IdentityDbContextFactory(connectionString)) { }

    public async Task<AuthUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await AuthUsers(db.Users.Where(x => x.Username == username)).SingleOrDefaultAsync(cancellationToken);
    }
    public async Task<AuthUser?> FindByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        return await AuthUsers(db.Users.Where(x => x.UserId == id)).SingleOrDefaultAsync(cancellationToken);
    }
    public async Task<UserDisplay[]> ListDisplayNamesAsync(CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        // Project only display fields; password hashes remain inside Identity.
        return await db.Users.AsNoTracking().OrderBy(x => x.FullName)
            .Select(x => new UserDisplay(x.UserId, x.Username, x.FullName)).ToArrayAsync(cancellationToken);
    }
    private static IQueryable<AuthUser> AuthUsers(IQueryable<User> users) => users.AsNoTracking()
        .Select(x => new AuthUser(x.UserId, x.Username, x.FullName, x.PasswordHash,
            x.Role.RoleName, x.WarehouseId, x.Status, x.RowVersion));

    public async Task UpdatePasswordHashAsync(AuthUser user, string hash, CancellationToken cancellationToken)
    {
        await using var db = await contexts.CreateDbContextAsync(cancellationToken);
        var entity = new User { UserId = user.UserId, RowVersion = user.RowVersion, PasswordHash = hash };
        db.Attach(entity);
        db.Entry(entity).Property(x => x.PasswordHash).IsModified = true;
        try { await db.SaveChangesAsync(cancellationToken); }
        // Another request changed the account. Do not overwrite its new password/version.
        catch (DbUpdateConcurrencyException) { }
    }
}
