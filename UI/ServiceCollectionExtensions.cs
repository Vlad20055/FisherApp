using Microsoft.Extensions.DependencyInjection;
using Domain.Repositories;
using Infrastructure.Repositories;

namespace UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // repositories
        services.AddSingleton<IUserRepository>(_ => new UserRepository(connectionString));
        services.AddSingleton<IRoleRepository>(_ => new RoleRepository(connectionString));
        services.AddSingleton<IProductRepository>(_ => new ProductRepository(connectionString));
        services.AddSingleton<ICategoryRepository>(_ => new CategoryRepository(connectionString));
        services.AddSingleton<IStoreRepository>(_ => new StoreRepository(connectionString));
        services.AddSingleton<IOrderRepository>(_ => new OrderRepository(connectionString));
        services.AddSingleton<IOrderItemRepository>(_ => new OrderItemRepository(connectionString));
        services.AddSingleton<IStoreAccountRepository>(_ => new StoreAccountRepository(connectionString));
        services.AddSingleton<ICompanyAccountRepository>(_ => new CompanyAccountRepository(connectionString));
        services.AddSingleton<ITransactionRepository>(_ => new TransactionRepository(connectionString));

        return services;
    }
}
