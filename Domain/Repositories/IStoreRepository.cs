using Domain.Entities;

namespace Domain.Repositories;

public interface IStoreRepository : IRepository<Store>
{
    Task<Store?> GetByManagerIdAsync(Guid managerId, CancellationToken cancellationToken);
    Task<Store?> GetByNameAsync(string name, CancellationToken cancellationToken);
    Task<List<Store>> GetAllAsync(CancellationToken cancellationToken);
}
