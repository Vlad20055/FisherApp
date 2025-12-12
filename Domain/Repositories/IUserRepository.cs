using Domain.Entities;

namespace Domain.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken);
    Task<Guid?> CreateWithPasswordAsync(User entity, string password, CancellationToken cancellationToken);
    Task<List<User>> GetByRoleIdAsync(Guid roleId, CancellationToken cancellationToken);
}
