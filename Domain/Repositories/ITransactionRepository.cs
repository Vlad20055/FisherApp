using Domain.Entities;

namespace Domain.Repositories;

public interface ITransactionRepository : IRepository<Transaction>
{
    public Task<Guid?> TransferStoreToCompany(Guid compAccId, Guid storeAccId, decimal amount, CancellationToken cancellationToken);
    public Task<Guid?> TransferCompanyToStore(Guid companyAccountId, Guid storeAccountId, decimal amount, CancellationToken cancellationToken);
}
