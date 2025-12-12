using Domain.Entities;

namespace Domain.Repositories;

public interface ICompanyAccountRepository : IRepository<CompanyAccount>
{
    Task<List<CompanyAccount>> GetAllAsync(CancellationToken cancellationToken);
}
