using Domain.Entities;

namespace Domain.Repositories;

public interface IOrderRepository : IRepository<Order>
{
    Task<List<Order>> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken);
    Task<List<Order>> GetAllAsync(CancellationToken cancellationToken);
}
