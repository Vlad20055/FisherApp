using Microsoft.Extensions.DependencyInjection;
using Domain.Repositories;
using Domain.Services;
using Infrastructure.Repositories;
using Infrastructure.NoSql;
using Infrastructure.Security;
using Infrastructure.Options;
using UseCases;
using Infrastructure.Caching;

namespace UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, DbOptions options)
    {
        var connectionString = options.ConnectionString;

        services.AddSingleton<UserRepository>(_ => new UserRepository(connectionString));
        services.AddSingleton<RoleRepository>(_ => new RoleRepository(connectionString));
        services.AddSingleton<ProductRepository>(_ => new ProductRepository(connectionString));
        services.AddSingleton<CategoryRepository>(_ => new CategoryRepository(connectionString));
        services.AddSingleton<StoreRepository>(_ => new StoreRepository(connectionString));
        services.AddSingleton<OrderRepository>(_ => new OrderRepository(connectionString));
        services.AddSingleton<OrderItemRepository>(_ => new OrderItemRepository(connectionString));
        services.AddSingleton<StoreAccountRepository>(_ => new StoreAccountRepository(connectionString));
        services.AddSingleton<CompanyAccountRepository>(_ => new CompanyAccountRepository(connectionString));
        services.AddSingleton<TransactionRepository>(_ => new TransactionRepository(connectionString));

        services.AddSingleton(_ => new RedisCache(options.RedisConnection));
        services.AddSingleton(_ => new MongoLogger(options.MongoConnection));

        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET") ?? "change_this_secret_to_env_var";
        services.AddSingleton(_ => new JwtService(jwtSecret));

        services.AddSingleton<ILogReportingService, MongoLogReportingService>();

        services.AddSingleton<IUserRepository>(sp => new CachedUserRepository(sp.GetRequiredService<UserRepository>(), sp.GetRequiredService<RedisCache>(), sp.GetRequiredService<MongoLogger>()));
        services.AddSingleton<IRoleRepository>(sp => new CachedRoleRepository(sp.GetRequiredService<RoleRepository>(), sp.GetRequiredService<RedisCache>(), sp.GetRequiredService<MongoLogger>()));
        services.AddSingleton<IProductRepository>(sp => new CachedProductRepository(sp.GetRequiredService<ProductRepository>(), sp.GetRequiredService<RedisCache>(), sp.GetRequiredService<MongoLogger>()));
        services.AddSingleton<ICategoryRepository>(sp => new CachedCategoryRepository(sp.GetRequiredService<CategoryRepository>(), sp.GetRequiredService<RedisCache>(), sp.GetRequiredService<MongoLogger>()));
        services.AddSingleton<IStoreRepository>(sp => sp.GetRequiredService<StoreRepository>());
        services.AddSingleton<IOrderRepository>(sp => new CachedOrderRepository(sp.GetRequiredService<OrderRepository>(), sp.GetRequiredService<RedisCache>(), sp.GetRequiredService<MongoLogger>()));
        services.AddSingleton<IOrderItemRepository>(sp => sp.GetRequiredService<OrderItemRepository>());
        services.AddSingleton<IStoreAccountRepository>(sp => sp.GetRequiredService<StoreAccountRepository>());
        services.AddSingleton<ICompanyAccountRepository>(sp => sp.GetRequiredService<CompanyAccountRepository>());
        services.AddSingleton<ITransactionRepository>(sp => sp.GetRequiredService<TransactionRepository>());

        services.AddSingleton<AuthServiceWithRedis>(sp => new AuthServiceWithRedis(sp.GetRequiredService<IUserRepository>(), sp.GetRequiredService<RedisCache>(), sp.GetRequiredService<JwtService>(), sp.GetRequiredService<MongoLogger>()));

        services.AddSingleton<CacheInvalidationSubscriber>(sp => new CacheInvalidationSubscriber(sp.GetRequiredService<RedisCache>(), sp.GetRequiredService<MongoLogger>()));

        services.AddSingleton<CompanyManager>();
        services.AddSingleton<Admin>();
        services.AddSingleton<StoreManager>();

        return services;
    }
}
