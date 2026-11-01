using Identity.Models.Authentication;

namespace Identity.BLL.Authentication;

public interface IUserRepository
{
    Task<AuthUser?> FindByUsernameAsync(string username, CancellationToken cancellationToken);
    Task<AuthUser?> FindByIdAsync(int id, CancellationToken cancellationToken);
    Task<UserDisplay[]> ListDisplayNamesAsync(CancellationToken cancellationToken);
    Task UpdatePasswordHashAsync(AuthUser user, string hash, CancellationToken cancellationToken);
}
