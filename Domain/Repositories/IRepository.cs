namespace Domain.Repositories;

public interface IRepository<T>
{
    public Task<Guid?> CreateAsync(T entity, CancellationToken cancellationToken);
    public Task<T?> ReadAsync(Guid id, CancellationToken cancellationToken);
    public Task<Guid?> UpdateAsync(T entity, CancellationToken cancellationToken);
    public Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
