using Domain.Entities;

namespace Domain.Repositories;

public interface IStoreAccountRepository : IRepository<StoreAccount>
{
    Task<StoreAccount?> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken);
    Task<List<StoreAccount>> GetAllAsync(CancellationToken cancellationToken);
}
