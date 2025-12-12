using Domain.Entities;

namespace Domain.Repositories;

public interface IOrderItemRepository : IRepository<OrderItem>
{
    Task<List<OrderItem>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken);
}
