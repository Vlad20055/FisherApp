using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Domain.Services;

namespace UseCases;

public class Admin
{
    private readonly IUserRepository _userRepo;
    private readonly IRoleRepository _roleRepo;
    private readonly IStoreRepository _storeRepo;
    private readonly ICompanyAccountRepository _companyAccountRepo;
    private readonly IStoreAccountRepository _storeAccountRepo;
    private readonly ITransactionRepository _transactionRepo;
    private readonly CompanyManager _companyManager;
    private readonly ILogReportingService _logReporting;

    public Admin(IUserRepository userRepo, IRoleRepository roleRepo, IStoreRepository storeRepo, ICompanyAccountRepository companyAccountRepo, IStoreAccountRepository storeAccountRepo, ITransactionRepository transactionRepo, CompanyManager companyManager, ILogReportingService logReporting)
    {
        _userRepo = userRepo;
        _roleRepo = roleRepo;
        _storeRepo = storeRepo;
        _companyAccountRepo = companyAccountRepo;
        _storeAccountRepo = storeAccountRepo;
        _transactionRepo = transactionRepo;
        _companyManager = companyManager;
        _logReporting = logReporting;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Administrator - Available commands:");
            Console.WriteLine("1. Add company manager");
            Console.WriteLine("2. Activate/Deactivate company manager");
            Console.WriteLine("3. Add store manager (and create new store)");
            Console.WriteLine("4. Activate/Deactivate store manager");
            Console.WriteLine("5. Delete company manager");
            Console.WriteLine("6. Delete store manager");
            Console.WriteLine("7. Transfer money from company to store");
            Console.WriteLine("8. Company manager menu (all company manager functions)");
            Console.WriteLine("9. Logs & MongoDB reports (search, aggregations, export)");
            Console.WriteLine("0. Logout / Exit");
            Console.Write("Enter command number: ");

            var cmd = Console.ReadLine()?.Trim() ?? string.Empty;
            if (cmd == "0")
            {
                Console.WriteLine("Logging out...");
                break;
            }

            try
            {
                switch (cmd)
                {
                    case "1":
                        await AddCompanyManager(ct);
                        break;
                    case "2":
                        await ToggleCompanyManagerActive(ct);
                        break;
                    case "3":
                        await AddStoreManager(ct);
                        break;
                    case "4":
                        await ToggleStoreManagerActive(ct);
                        break;
                    case "5":
                        await DeleteCompanyManager(ct);
                        break;
                    case "6":
                        await DeleteStoreManager(ct);
                        break;
                    case "7":
                        await TransferCompanyToStoreInteractive(ct);
                        break;
                    case "8":
                        await _companyManager.RunAsync(ct);
                        break;
                    case "9":
                        await MongoLogsMenuAsync(ct);
                        break;
                    default:
                        Console.WriteLine("Unknown command. Please enter a number from the list.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            await Task.Delay(10, ct);
        }
    }

    private static string ReadPasswordStatic()
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

    private async Task AddCompanyManager(CancellationToken ct)
    {
        var role = await _roleRepo.GetByNameAsync("company_manager", ct);
        if (role == null) { Console.WriteLine("Role 'company_manager' not found."); return; }

        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.Write("Full name: ");
        var fullName = Console.ReadLine() ?? string.Empty;
        Console.Write("Password: ");
        var password = ReadPasswordStatic();

        var user = new User { Id = Guid.NewGuid(), Username = username, FullName = fullName, RoleId = role.Id, IsActive = true };
        var created = await _userRepo.CreateWithPasswordAsync(user, password, ct);
        Console.WriteLine(created != null ? $"Company manager created with id {created}" : "Failed to create user.");
    }

    private async Task ToggleCompanyManagerActive(CancellationToken ct)
    {
        var role = await _roleRepo.GetByNameAsync("company_manager", ct);
        if (role == null) { Console.WriteLine("Role 'company_manager' not found."); return; }
        var users = await _userRepo.GetByRoleIdAsync(role.Id, ct);
        if (users.Count == 0) { Console.WriteLine("No company managers found."); return; }
        for (int i = 0; i < users.Count; i++) Console.WriteLine($"{i + 1}. {users[i].Username} | {users[i].FullName} | Active: {users[i].IsActive}");
        Console.Write("Select number to toggle active: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > users.Count) { Console.WriteLine("Invalid selection"); return; }
        var u = users[idx - 1];
        u.IsActive = !u.IsActive;
        var updated = await _userRepo.UpdateAsync(u, ct);
        Console.WriteLine(updated != null ? $"User {(u.IsActive ? "activated" : "deactivated")}." : "Failed to update user.");
    }

    private async Task AddStoreManager(CancellationToken ct)
    {
        var role = await _roleRepo.GetByNameAsync("store_manager", ct);
        if (role == null) { Console.WriteLine("Role 'store_manager' not found."); return; }

        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.Write("Full name: ");
        var fullName = Console.ReadLine() ?? string.Empty;
        Console.Write("Password: ");
        var password = ReadPasswordStatic();

        // create user first
        var user = new User { Id = Guid.NewGuid(), Username = username, FullName = fullName, RoleId = role.Id, IsActive = true };
        var created = await _userRepo.CreateWithPasswordAsync(user, password, ct);
        if (created == null) { Console.WriteLine("Failed to create user."); return; }

        // create store and assign manager
        Console.Write("Store name: ");
        var storeName = Console.ReadLine() ?? string.Empty;
        Console.Write("Store address: ");
        var storeAddress = Console.ReadLine() ?? string.Empty;
        Console.Write("Store tax id: ");
        var taxId = Console.ReadLine() ?? string.Empty;

        var store = new Store { Id = Guid.NewGuid(), Name = storeName, Address = storeAddress, TaxId = taxId, ManagerId = created.Value };
        var storeCreated = await _storeRepo.CreateAsync(store, ct);
        Console.WriteLine(storeCreated != null ? "Store manager created and new store assigned." : "User created but failed to create store.");
    }

    private async Task ToggleStoreManagerActive(CancellationToken ct)
    {
        var role = await _roleRepo.GetByNameAsync("store_manager", ct);
        if (role == null) { Console.WriteLine("Role 'store_manager' not found."); return; }
        var users = await _userRepo.GetByRoleIdAsync(role.Id, ct);
        if (users.Count == 0) { Console.WriteLine("No store managers found."); return; }
        for (int i = 0; i < users.Count; i++) Console.WriteLine($"{i + 1}. {users[i].Username} | {users[i].FullName} | Active: {users[i].IsActive}");
        Console.Write("Select number to toggle active: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > users.Count) { Console.WriteLine("Invalid selection"); return; }
        var u = users[idx - 1];
        u.IsActive = !u.IsActive;
        var updated = await _userRepo.UpdateAsync(u, ct);
        Console.WriteLine(updated != null ? $"User {(u.IsActive ? "activated" : "deactivated")}." : "Failed to update user.");
    }

    private async Task DeleteCompanyManager(CancellationToken ct)
    {
        var role = await _roleRepo.GetByNameAsync("company_manager", ct);
        if (role == null) { Console.WriteLine("Role 'company_manager' not found."); return; }
        var users = await _userRepo.GetByRoleIdAsync(role.Id, ct);
        if (users.Count == 0) { Console.WriteLine("No company managers found."); return; }
        for (int i = 0; i < users.Count; i++) Console.WriteLine($"{i + 1}. {users[i].Username} | {users[i].FullName}");
        Console.Write("Select number to delete: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > users.Count) { Console.WriteLine("Invalid selection"); return; }
        var u = users[idx - 1];
        var deleted = await _userRepo.DeleteAsync(u.Id, ct);
        Console.WriteLine(deleted != null ? "User deleted." : "Failed to delete user (check references). ");
    }

    private async Task DeleteStoreManager(CancellationToken ct)
    {
        var role = await _roleRepo.GetByNameAsync("store_manager", ct);
        if (role == null) { Console.WriteLine("Role 'store_manager' not found."); return; }
        var users = await _userRepo.GetByRoleIdAsync(role.Id, ct);
        if (users.Count == 0) { Console.WriteLine("No store managers found."); return; }
        for (int i = 0; i < users.Count; i++) Console.WriteLine($"{i + 1}. {users[i].Username} | {users[i].FullName}");
        Console.Write("Select number to delete: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > users.Count) { Console.WriteLine("Invalid selection"); return; }
        var u = users[idx - 1];
        var deleted = await _userRepo.DeleteAsync(u.Id, ct);
        Console.WriteLine(deleted != null ? "User deleted." : "Failed to delete user (check references). ");
    }

    private async Task TransferCompanyToStoreInteractive(CancellationToken ct)
    {
        var companies = await _companyAccountRepo.GetAllAsync(ct);
        var stores = await _storeRepo.GetAllAsync(ct);
        if (companies.Count == 0 || stores.Count == 0) { Console.WriteLine("No company accounts or stores found."); return; }

        Console.WriteLine("Available company accounts:");
        for (int i = 0; i < companies.Count; i++) Console.WriteLine($"{i + 1}. {companies[i].Id} — balance: {companies[i].Balance}");
        Console.Write("Select company account by number: ");
        if (!int.TryParse(Console.ReadLine(), out var companyIdx) || companyIdx < 1 || companyIdx > companies.Count) { Console.WriteLine("Invalid selection"); return; }
        var selectedCompanyAccount = companies[companyIdx - 1];

        Console.WriteLine("Available stores:");
        for (int i = 0; i < stores.Count; i++) Console.WriteLine($"{i + 1}. {stores[i].Id} - {stores[i].Name}");
        Console.Write("Select store by number: ");
        if (!int.TryParse(Console.ReadLine(), out var storeIdx) || storeIdx < 1 || storeIdx > stores.Count) { Console.WriteLine("Invalid selection"); return; }
        var selectedStore = stores[storeIdx - 1];

        var storeAccount = await _storeAccountRepo.GetByStoreIdAsync(selectedStore.Id, ct);
        if (storeAccount == null) { Console.WriteLine("No account found for this store."); return; }

        Console.Write("Amount to transfer: ");
        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0) { Console.WriteLine("Invalid amount"); return; }

        var txId = await _transactionRepo.TransferCompanyToStore(selectedCompanyAccount.Id, storeAccount.Id, amount, ct);
        Console.WriteLine(txId != null ? "Transfer completed." : "Transfer failed.");
    }

    private async Task MongoLogsMenuAsync(CancellationToken ct)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("--- MongoDB logs & reports ---");
            Console.WriteLine("1. Search logs (user, level, eventType, period, limit)");
            Console.WriteLine("2. Activity by calendar day (period: day/week/month)");
            Console.WriteLine("3. TOP active users");
            Console.WriteLine("4. Operations by eventType (CRUD / auth / …)");
            Console.WriteLine("5. Hourly trend (last N hours)");
            Console.WriteLine("6. Anomalies (high volume users, 24h)");
            Console.WriteLine("7. Export filtered logs to JSON file");
            Console.WriteLine("8. Export filtered logs to CSV file");
            Console.WriteLine("0. Back");
            Console.Write("Choice: ");
            var c = Console.ReadLine()?.Trim() ?? string.Empty;
            if (c == "0") return;
            try
            {
                switch (c)
                {
                    case "1":
                    {
                        Console.Write("user (empty = any): ");
                        var u = Console.ReadLine()?.Trim();
                        Console.Write("level (empty = any, e.g. info/warn): ");
                        var lv = Console.ReadLine()?.Trim();
                        Console.Write("eventType (empty = any, e.g. AUTH, READ, CREATE): ");
                        var et = Console.ReadLine()?.Trim();
                        Console.Write("from UTC (yyyy-MM-dd or empty): ");
                        DateTime? from = null;
                        if (DateTime.TryParse(Console.ReadLine(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var df))
                            from = df;
                        Console.Write("to UTC (yyyy-MM-dd or empty): ");
                        DateTime? to = null;
                        if (DateTime.TryParse(Console.ReadLine(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                            to = dt;
                        Console.Write("limit (default 100): ");
                        var lim = int.TryParse(Console.ReadLine(), out var l) ? l : 100;
                        var json = await _logReporting.SearchLogsAsync(from, to, string.IsNullOrEmpty(u) ? null : u, string.IsNullOrEmpty(lv) ? null : lv, string.IsNullOrEmpty(et) ? null : et, lim, ct);
                        Console.WriteLine(json);
                        break;
                    }
                    case "2":
                    {
                        Console.Write("period day / week / month [week]: ");
                        var p = Console.ReadLine()?.Trim();
                        if (string.IsNullOrEmpty(p)) p = "week";
                        Console.WriteLine(await _logReporting.ReportActivityByPeriodJsonAsync(p, ct));
                        break;
                    }
                    case "3":
                    {
                        Console.Write("TOP count [10]: ");
                        var n = int.TryParse(Console.ReadLine(), out var t) ? t : 10;
                        Console.WriteLine(await _logReporting.ReportTopUsersJsonAsync(n, ct));
                        break;
                    }
                    case "4":
                        Console.WriteLine(await _logReporting.ReportEventTypeDistributionJsonAsync(ct));
                        break;
                    case "5":
                    {
                        Console.Write("hours [24]: ");
                        var h = int.TryParse(Console.ReadLine(), out var hh) ? hh : 24;
                        Console.WriteLine(await _logReporting.ReportHourlyTrendJsonAsync(h, ct));
                        break;
                    }
                    case "6":
                        Console.WriteLine(await _logReporting.ReportAnomaliesJsonAsync(ct));
                        break;
                    case "7":
                    case "8":
                    {
                        Console.Write("output file path: ");
                        var path = Console.ReadLine()?.Trim() ?? string.Empty;
                        if (string.IsNullOrEmpty(path)) { Console.WriteLine("Path required."); break; }
                        Console.Write("filter user (empty = any): ");
                        var u = Console.ReadLine()?.Trim();
                        Console.Write("from UTC (empty = any): ");
                        DateTime? from = null;
                        if (DateTime.TryParse(Console.ReadLine(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var df))
                            from = df;
                        Console.Write("to UTC (empty = any): ");
                        DateTime? to = null;
                        if (DateTime.TryParse(Console.ReadLine(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dt))
                            to = dt;
                        var msg = c == "7"
                            ? await _logReporting.ExportLogsToJsonFileAsync(path, from, to, string.IsNullOrEmpty(u) ? null : u, ct)
                            : await _logReporting.ExportLogsToCsvFileAsync(path, from, to, string.IsNullOrEmpty(u) ? null : u, ct);
                        Console.WriteLine(msg);
                        break;
                    }
                    default:
                        Console.WriteLine("Unknown choice.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            await Task.Delay(10, ct);
        }
    }
}
