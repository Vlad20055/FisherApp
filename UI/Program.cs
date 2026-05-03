using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using UI;
using Domain.Repositories;
using UseCases;
using Infrastructure.NoSql;

class Program
{
    static async Task<int> Main(string[] args)
    {
        try
        {
            return await RunAppAsync(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine($"Фатальная ошибка: {ex.Message}");
            if (ex.InnerException != null)
                Console.WriteLine($"  {ex.InnerException.Message}");
            Console.WriteLine("Убедитесь, что запущены PostgreSQL, Redis и MongoDB; проверьте строки в Infrastructure/Options/DbOptions.cs и переменные FISHER_REDIS, FISHER_MONGO, JWT_SECRET.");
            return 2;
        }
    }

    private static async Task<int> RunAppAsync(string[] _)
    {
        var dbOptions = new DbOptions();

        var services = new ServiceCollection();
        services.AddInfrastructure(dbOptions);
        var provider = services.BuildServiceProvider();

        var cacheSubscriber = provider.GetRequiredService<CacheInvalidationSubscriber>();
        await cacheSubscriber.StartAsync();

        var roleRepo = provider.GetRequiredService<IRoleRepository>();
        var storeRepo = provider.GetRequiredService<IStoreRepository>();

        var adminUseCase = provider.GetRequiredService<Admin>();
        var companyManagerUseCase = provider.GetRequiredService<CompanyManager>();
        var storeManagerUseCase = provider.GetRequiredService<StoreManager>();
        var authService = provider.GetRequiredService<AuthServiceWithRedis>();

        Console.WriteLine("=== Fisher Console App ===");

        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.Write("Password: ");
        var password = ReadPassword();

        var (success, accessToken, refreshToken, user, message) = await authService.AuthenticateAsync(username, password, CancellationToken.None);
        if (!success)
        {
            Console.WriteLine(message ?? "Invalid credentials or inactive user.");
            return 1;
        }

        Console.WriteLine($"Authentication successful. Access token: {accessToken}");

        // interactive options: continue, refresh, logout
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("1) Continue to application");
            Console.WriteLine("2) Refresh access token");
            Console.WriteLine("3) Logout and exit");
            Console.Write("Choose an option: ");
            var opt = Console.ReadLine()?.Trim() ?? string.Empty;
            if (opt == "1") break;
            if (opt == "2")
            {
                var (rsuccess, newAccess, newRefresh, rmsg) = await authService.RefreshAsync(refreshToken!);
                if (!rsuccess)
                {
                    Console.WriteLine(rmsg ?? "Refresh failed");
                }
                else
                {
                    accessToken = newAccess;
                    refreshToken = newRefresh;
                    Console.WriteLine($"Token refreshed. New access token: {accessToken}");
                }
                continue;
            }
            if (opt == "3")
            {
                await authService.LogoutAsync(accessToken!, refreshToken);
                Console.WriteLine("Logged out. Exiting.");
                return 0;
            }
            Console.WriteLine("Unknown option");
        }

        var role = await roleRepo.ReadAsync(user!.RoleId, CancellationToken.None);
        Console.WriteLine($"Welcome, {user.FullName} (role: {role?.Name ?? "unknown"})");

        if (role?.Name == "company_manager")
        {
            await companyManagerUseCase.RunAsync(CancellationToken.None);
        }
        else if (role?.Name == "store_manager")
        {
            var store = await storeRepo.GetByManagerIdAsync(user.Id, CancellationToken.None);
            if (store == null)
            {
                Console.WriteLine("You are not assigned to any store.");
                return 1;
            }

            await storeManagerUseCase.RunAsync(store, CancellationToken.None);
        }
        else if (role?.Name == "admin" || role?.Name == "company_admin" || role?.Name == "administrator")
        {
            await adminUseCase.RunAsync(CancellationToken.None);
        }
        else
        {
            Console.WriteLine("This role is not supported yet.");
        }

        return 0;
    }

    public static string ReadPassword()
    {
        var pwd = string.Empty;
        ConsoleKey key;
        do
        {
            var keyInfo = Console.ReadKey(intercept: true);
            key = keyInfo.Key;
            if (key == ConsoleKey.Backspace && pwd.Length > 0)
            {
                pwd = pwd[0..^1];
                Console.Write("\b \b");
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                pwd += keyInfo.KeyChar;
                Console.Write("*");
            }
        } while (key != ConsoleKey.Enter);
        Console.WriteLine();
        return pwd;
    }
}
