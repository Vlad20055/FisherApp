using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Domain.Repositories;
using UI;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var dbOptions = new DbOptions();

        var services = new ServiceCollection();
        services.AddInfrastructure(dbOptions.ConnectionString);
        var provider = services.BuildServiceProvider();

        var userRepo = provider.GetRequiredService<IUserRepository>();
        var roleRepo = provider.GetRequiredService<IRoleRepository>();
        var productRepo = provider.GetRequiredService<IProductRepository>();
        var categoryRepo = provider.GetRequiredService<ICategoryRepository>();
        var storeRepo = provider.GetRequiredService<IStoreRepository>();
        var companyAccountRepo = provider.GetRequiredService<ICompanyAccountRepository>();
        var orderRepo = provider.GetRequiredService<IOrderRepository>();
        var orderItemRepo = provider.GetRequiredService<IOrderItemRepository>();
        var storeAccountRepo = provider.GetRequiredService<IStoreAccountRepository>();
        var transactionRepo = provider.GetRequiredService<ITransactionRepository>();

        Console.WriteLine("=== Fisher Console App ===");

        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.Write("Password: ");
        var password = ReadPassword();

        var user = await userRepo.AuthenticateAsync(username, password, CancellationToken.None);
        if (user == null)
        {
            Console.WriteLine("Invalid credentials or inactive user.");
            return 1;
        }

        var role = await roleRepo.ReadAsync(user.RoleId, CancellationToken.None);
        Console.WriteLine($"Welcome, {user.FullName} (role: {role?.Name ?? "unknown"})");

        if (role?.Name == "company_manager")
        {
            await CompanyManagerMenuLoop(productRepo, categoryRepo, storeRepo, companyAccountRepo, orderRepo, orderItemRepo);
        }
        else if (role?.Name == "store_manager")
        {
            var store = await storeRepo.GetByManagerIdAsync(user.Id, CancellationToken.None);
            if (store == null)
            {
                Console.WriteLine("You are not assigned to any store.");
                return 1;
            }

            await StoreManagerMenuLoop(store, productRepo, orderRepo, orderItemRepo, storeAccountRepo, companyAccountRepo, transactionRepo);
        }
        else if (role?.Name == "admin" || role?.Name == "company_admin" || role?.Name == "administrator")
        {
            await AdminMenuLoop(userRepo, roleRepo, storeRepo, companyAccountRepo, storeAccountRepo, productRepo, categoryRepo, transactionRepo, orderRepo, orderItemRepo);
        }
        else
        {
            Console.WriteLine("This role is not supported yet.");
        }

        return 0;
    }

    static string ReadPassword()
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

    // Admin menu and handlers
    static async Task AdminMenuLoop(IUserRepository userRepo, IRoleRepository roleRepo, IStoreRepository storeRepo, ICompanyAccountRepository companyAccountRepo, IStoreAccountRepository storeAccountRepo, IProductRepository productRepo, ICategoryRepository categoryRepo, ITransactionRepository transactionRepo, IOrderRepository orderRepo, IOrderItemRepository orderItemRepo)
    {
        var ct = CancellationToken.None;
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
                        await AddCompanyManager(userRepo, roleRepo, ct);
                        break;
                    case "2":
                        await ToggleCompanyManagerActive(userRepo, roleRepo, ct);
                        break;
                    case "3":
                        await AddStoreManager(userRepo, roleRepo, storeRepo, ct);
                        break;
                    case "4":
                        await ToggleStoreManagerActive(userRepo, roleRepo, ct);
                        break;
                    case "5":
                        await DeleteCompanyManager(userRepo, roleRepo, ct);
                        break;
                    case "6":
                        await DeleteStoreManager(userRepo, roleRepo, storeRepo, ct);
                        break;
                    case "7":
                        await TransferCompanyToStoreInteractive(companyAccountRepo, storeAccountRepo, transactionRepo, ct);
                        break;
                    case "8":
                        await CompanyManagerMenuLoop(productRepo, categoryRepo, storeRepo, companyAccountRepo, orderRepo, orderItemRepo);
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

            await Task.Delay(10);
        }
    }

    static async Task AddCompanyManager(IUserRepository userRepo, IRoleRepository roleRepo, CancellationToken ct)
    {
        var role = await roleRepo.GetByNameAsync("company_manager", ct);
        if (role == null) { Console.WriteLine("Role 'company_manager' not found."); return; }

        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.Write("Full name: ");
        var fullName = Console.ReadLine() ?? string.Empty;
        Console.Write("Password: ");
        var password = ReadPassword();

        var user = new User { Id = Guid.NewGuid(), Username = username, FullName = fullName, RoleId = role.Id, IsActive = true };
        var created = await userRepo.CreateWithPasswordAsync(user, password, ct);
        Console.WriteLine(created != null ? $"Company manager created with id {created}" : "Failed to create user.");
    }

    static async Task ToggleCompanyManagerActive(IUserRepository userRepo, IRoleRepository roleRepo, CancellationToken ct)
    {
        var role = await roleRepo.GetByNameAsync("company_manager", ct);
        if (role == null) { Console.WriteLine("Role 'company_manager' not found."); return; }
        var users = await userRepo.GetByRoleIdAsync(role.Id, ct);
        if (users.Count == 0) { Console.WriteLine("No company managers found."); return; }
        for (int i = 0; i < users.Count; i++) Console.WriteLine($"{i + 1}. {users[i].Username} | {users[i].FullName} | Active: {users[i].IsActive}");
        Console.Write("Select number to toggle active: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > users.Count) { Console.WriteLine("Invalid selection"); return; }
        var u = users[idx - 1];
        u.IsActive = !u.IsActive;
        var updated = await userRepo.UpdateAsync(u, ct);
        Console.WriteLine(updated != null ? $"User {(u.IsActive ? "activated" : "deactivated")}." : "Failed to update user.");
    }

    static async Task AddStoreManager(IUserRepository userRepo, IRoleRepository roleRepo, IStoreRepository storeRepo, CancellationToken ct)
    {
        var role = await roleRepo.GetByNameAsync("store_manager", ct);
        if (role == null) { Console.WriteLine("Role 'store_manager' not found."); return; }

        Console.Write("Username: ");
        var username = Console.ReadLine()?.Trim() ?? string.Empty;
        Console.Write("Full name: ");
        var fullName = Console.ReadLine() ?? string.Empty;
        Console.Write("Password: ");
        var password = ReadPassword();

        // create user first
        var user = new User { Id = Guid.NewGuid(), Username = username, FullName = fullName, RoleId = role.Id, IsActive = true };
        var created = await userRepo.CreateWithPasswordAsync(user, password, ct);
        if (created == null) { Console.WriteLine("Failed to create user."); return; }

        // create store and assign manager
        Console.Write("Store name: ");
        var storeName = Console.ReadLine() ?? string.Empty;
        Console.Write("Store address: ");
        var storeAddress = Console.ReadLine() ?? string.Empty;
        Console.Write("Store tax id: ");
        var taxId = Console.ReadLine() ?? string.Empty;

        var store = new Store { Id = Guid.NewGuid(), Name = storeName, Address = storeAddress, TaxId = taxId, ManagerId = created.Value };
        var storeCreated = await storeRepo.CreateAsync(store, ct);
        Console.WriteLine(storeCreated != null ? "Store manager created and new store assigned." : "User created but failed to create store.");
    }

    static async Task ToggleStoreManagerActive(IUserRepository userRepo, IRoleRepository roleRepo, CancellationToken ct)
    {
        var role = await roleRepo.GetByNameAsync("store_manager", ct);
        if (role == null) { Console.WriteLine("Role 'store_manager' not found."); return; }
        var users = await userRepo.GetByRoleIdAsync(role.Id, ct);
        if (users.Count == 0) { Console.WriteLine("No store managers found."); return; }
        for (int i = 0; i < users.Count; i++) Console.WriteLine($"{i + 1}. {users[i].Username} | {users[i].FullName} | Active: {users[i].IsActive}");
        Console.Write("Select number to toggle active: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > users.Count) { Console.WriteLine("Invalid selection"); return; }
        var u = users[idx - 1];
        u.IsActive = !u.IsActive;
        var updated = await userRepo.UpdateAsync(u, ct);
        Console.WriteLine(updated != null ? $"User {(u.IsActive ? "activated" : "deactivated")}." : "Failed to update user.");
    }

    static async Task DeleteCompanyManager(IUserRepository userRepo, IRoleRepository roleRepo, CancellationToken ct)
    {
        var role = await roleRepo.GetByNameAsync("company_manager", ct);
        if (role == null) { Console.WriteLine("Role 'company_manager' not found."); return; }
        var users = await userRepo.GetByRoleIdAsync(role.Id, ct);
        if (users.Count == 0) { Console.WriteLine("No company managers found."); return; }
        for (int i = 0; i < users.Count; i++) Console.WriteLine($"{i + 1}. {users[i].Username} | {users[i].FullName}");
        Console.Write("Select number to delete: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > users.Count) { Console.WriteLine("Invalid selection"); return; }
        var u = users[idx - 1];
        var deleted = await userRepo.DeleteAsync(u.Id, ct);
        Console.WriteLine(deleted != null ? "User deleted." : "Failed to delete user (check references). ");
    }

    static async Task DeleteStoreManager(IUserRepository userRepo, IRoleRepository roleRepo, IStoreRepository storeRepo, CancellationToken ct)
    {
        var role = await roleRepo.GetByNameAsync("store_manager", ct);
        if (role == null) { Console.WriteLine("Role 'store_manager' not found."); return; }
        var users = await userRepo.GetByRoleIdAsync(role.Id, ct);
        if (users.Count == 0) { Console.WriteLine("No store managers found."); return; }
        for (int i = 0; i < users.Count; i++) Console.WriteLine($"{i + 1}. {users[i].Username} | {users[i].FullName}");
        Console.Write("Select number to delete: ");
        if (!int.TryParse(Console.ReadLine(), out var idx) || idx < 1 || idx > users.Count) { Console.WriteLine("Invalid selection"); return; }
        var u = users[idx - 1];
        // check if assigned to a store
        var assigned = await storeRepo.GetByManagerIdAsync(u.Id, ct);
        if (assigned != null) { Console.WriteLine($"Cannot delete user. Assigned as manager of store '{assigned.Name}'. Reassign or remove before deleting."); return; }
        var deleted = await userRepo.DeleteAsync(u.Id, ct);
        Console.WriteLine(deleted != null ? "User deleted." : "Failed to delete user.");
    }

    static async Task TransferCompanyToStoreInteractive(ICompanyAccountRepository companyAccountRepo, IStoreAccountRepository storeAccountRepo, ITransactionRepository transactionRepo, CancellationToken ct)
    {
        var compAccounts = await companyAccountRepo.GetAllAsync(ct);
        if (compAccounts.Count == 0) { Console.WriteLine("No company accounts found."); return; }
        Console.WriteLine("Company accounts:");
        for (int i = 0; i < compAccounts.Count; i++) Console.WriteLine($"{i + 1}. {compAccounts[i].Id} | Balance: {compAccounts[i].Balance}");
        Console.Write("Select company account number: ");
        if (!int.TryParse(Console.ReadLine(), out var cidx) || cidx < 1 || cidx > compAccounts.Count) { Console.WriteLine("Invalid selection"); return; }
        var comp = compAccounts[cidx - 1];

        var storeAccounts = await storeAccountRepo.GetAllAsync(ct);
        if (storeAccounts.Count == 0) { Console.WriteLine("No store accounts found."); return; }
        Console.WriteLine("Store accounts:");
        for (int i = 0; i < storeAccounts.Count; i++) Console.WriteLine($"{i + 1}. {storeAccounts[i].Id} | StoreId: {storeAccounts[i].StoreId} | Balance: {storeAccounts[i].Balance}");
        Console.Write("Select store account number: ");
        if (!int.TryParse(Console.ReadLine(), out var sidx) || sidx < 1 || sidx > storeAccounts.Count) { Console.WriteLine("Invalid selection"); return; }
        var storeAcc = storeAccounts[sidx - 1];

        Console.Write("Amount to transfer: ");
        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0) { Console.WriteLine("Invalid amount"); return; }

        if (comp.Balance < amount) { Console.WriteLine("Insufficient funds in company account"); return; }

        comp.Balance -= amount;
        storeAcc.Balance += amount;

        var updComp = await companyAccountRepo.UpdateAsync(comp, ct);
        var updStore = await storeAccountRepo.UpdateAsync(storeAcc, ct);
        if (updComp == null || updStore == null) { Console.WriteLine("Failed to update accounts"); return; }

        var tx = new Transaction { Id = Guid.NewGuid(), StoreAccountId = storeAcc.Id, CompanyAccountId = comp.Id, Amount = amount };
        var txId = await transactionRepo.CreateAsync(tx, ct);
        Console.WriteLine(txId != null ? "Transfer completed." : "Transfer failed when creating transaction record.");
    }

    // Company manager loop (extended)
    static async Task CompanyManagerMenuLoop(IProductRepository productRepo, ICategoryRepository categoryRepo, IStoreRepository storeRepo, ICompanyAccountRepository companyAccountRepo, IOrderRepository orderRepo, IOrderItemRepository orderItemRepo)
    {
        var ct = CancellationToken.None;
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Company Manager - Available commands:");
            Console.WriteLine("1. Add stock by product name");
            Console.WriteLine("2. Add new product to database");
            Console.WriteLine("3. Add new category to database");
            Console.WriteLine("4. Update product quantity by product name");
            Console.WriteLine("5. View list of all stores");
            Console.WriteLine("6. View all company accounts");
            Console.WriteLine("7. View all orders for a specific store (by store name)");
            Console.WriteLine("8. Update product price (by product name)");
            Console.WriteLine("9. Delete category from database");
            Console.WriteLine("10. Delete product from database");
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
                        await AddStockByName(productRepo, ct);
                        break;
                    case "2":
                        await AddNewProduct(productRepo, categoryRepo, ct);
                        break;
                    case "3":
                        await AddNewCategory(categoryRepo, ct);
                        break;
                    case "4":
                        await UpdateQuantityByName(productRepo, ct);
                        break;
                    case "5":
                        await ListAllStores(storeRepo, ct);
                        break;
                    case "6":
                        await ViewCompanyAccounts(companyAccountRepo, ct);
                        break;
                    case "7":
                        await ViewOrdersForStoreByName(storeRepo, orderRepo, orderItemRepo, productRepo, ct);
                        break;
                    case "8":
                        await UpdateProductPriceByName(productRepo, ct);
                        break;
                    case "9":
                        await DeleteCategory(categoryRepo, ct);
                        break;
                    case "10":
                        await DeleteProduct(productRepo, ct);
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

            await Task.Delay(10);
        }
    }

    static async Task ViewOrdersForStoreByName(IStoreRepository storeRepo, IOrderRepository orderRepo, IOrderItemRepository orderItemRepo, IProductRepository productRepo, CancellationToken ct)
    {
        Console.Write("Store name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var store = await storeRepo.GetByNameAsync(name, ct);
        if (store == null) { Console.WriteLine("Store not found"); return; }
        var orders = await orderRepo.GetByStoreIdAsync(store.Id, ct);
        if (orders.Count == 0) { Console.WriteLine("No orders for this store."); return; }
        foreach (var o in orders)
        {
            Console.WriteLine($"Order {o.Id} | Total: {o.TotalAmount} | Created: {o.CreatedAt}");
            var items = await orderItemRepo.GetByOrderIdAsync(o.Id, ct);
            foreach (var it in items)
            {
                var p = await productRepo.ReadAsync(it.ProductId, ct);
                var pname = p?.Name ?? it.ProductId.ToString();
                Console.WriteLine($"  - {pname} x{it.Quantity} @ {it.UnitPrice}");
            }
        }
    }

    static async Task UpdateProductPriceByName(IProductRepository productRepo, CancellationToken ct)
    {
        Console.Write("Product name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var product = await productRepo.GetByNameAsync(name, ct);
        if (product == null) { Console.WriteLine("Product not found"); return; }
        Console.Write("New price: ");
        if (!decimal.TryParse(Console.ReadLine(), out var price) || price < 0) { Console.WriteLine("Invalid price"); return; }
        product.Price = price;
        var upd = await productRepo.UpdateAsync(product, ct);
        Console.WriteLine(upd != null ? "Price updated." : "Failed to update price.");
    }

    static async Task DeleteCategory(ICategoryRepository categoryRepo, CancellationToken ct)
    {
        Console.Write("Category name to delete: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var cat = await categoryRepo.GetByNameAsync(name, ct);
        if (cat == null) { Console.WriteLine("Category not found"); return; }
        var del = await categoryRepo.DeleteAsync(cat.Id, ct);
        Console.WriteLine(del != null ? "Category deleted." : "Failed to delete category (check references).");
    }

    static async Task DeleteProduct(IProductRepository productRepo, CancellationToken ct)
    {
        Console.Write("Product name to delete: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var p = await productRepo.GetByNameAsync(name, ct);
        if (p == null) { Console.WriteLine("Product not found"); return; }
        var del = await productRepo.DeleteAsync(p.Id, ct);
        Console.WriteLine(del != null ? "Product deleted." : "Failed to delete product (check references).");
    }

    static async Task AddStockByName(IProductRepository productRepo, CancellationToken ct)
    {
        Console.Write("Product name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var product = await productRepo.GetByNameAsync(name, ct);
        if (product == null)
        {
            Console.WriteLine("Product not found.");
            return;
        }
        Console.Write("Amount to add: ");
        if (!int.TryParse(Console.ReadLine(), out var add) || add <= 0) { Console.WriteLine("Invalid amount"); return; }
        product.QuantityInStock += add;
        var res = await productRepo.UpdateAsync(product, ct);
        Console.WriteLine(res != null ? "Stock updated." : "Failed to update stock.");
    }

    static async Task AddNewProduct(IProductRepository productRepo, ICategoryRepository categoryRepo, CancellationToken ct)
    {
        Console.Write("Name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        Console.Write("Description: ");
        var desc = Console.ReadLine() ?? string.Empty;
        Console.Write("Price: ");
        if (!decimal.TryParse(Console.ReadLine(), out var price) || price < 0) { Console.WriteLine("Invalid price"); return; }
        Console.Write("Initial quantity in stock: ");
        if (!int.TryParse(Console.ReadLine(), out var qty) || qty < 0) { Console.WriteLine("Invalid quantity"); return; }
        Console.Write("Category name: ");
        var catName = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(catName)) { Console.WriteLine("Category name required"); return; }

        var category = await categoryRepo.GetByNameAsync(catName, ct);
        if (category == null)
        {
            Console.Write($"Category '{catName}' does not exist. Create it now? (y/n): ");
            var ans = Console.ReadLine()?.Trim().ToLower() ?? "n";
            if (ans == "y" || ans == "yes")
            {
                var newCat = new Category { Id = Guid.NewGuid(), Name = catName };
                var catId = await categoryRepo.CreateAsync(newCat, ct);
                if (catId == null) { Console.WriteLine("Failed to create category"); return; }
                category = newCat;
                Console.WriteLine("Category created.");
            }
            else
            {
                Console.WriteLine("Cannot create product without category.");
                return;
            }
        }

        var product = new Product { Id = Guid.NewGuid(), Name = name, Description = desc, Price = price, QuantityInStock = qty, CategoryId = category.Id };
        var created = await productRepo.CreateAsync(product, ct);
        Console.WriteLine(created != null ? $"Product created with id {created}" : "Failed to create product.");
    }

    static async Task AddNewCategory(ICategoryRepository categoryRepo, CancellationToken ct)
    {
        Console.Write("Category name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var exists = await categoryRepo.GetByNameAsync(name, ct);
        if (exists != null) { Console.WriteLine("Category already exists."); return; }
        var cat = new Category { Id = Guid.NewGuid(), Name = name };
        var created = await categoryRepo.CreateAsync(cat, ct);
        Console.WriteLine(created != null ? "Category created." : "Failed to create category.");
    }

    static async Task UpdateQuantityByName(IProductRepository productRepo, CancellationToken ct)
    {
        Console.Write("Product name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var product = await productRepo.GetByNameAsync(name, ct);
        if (product == null) { Console.WriteLine("Product not found"); return; }
        Console.Write("New quantity: ");
        if (!int.TryParse(Console.ReadLine(), out var newQty) || newQty < 0) { Console.WriteLine("Invalid quantity"); return; }
        product.QuantityInStock = newQty;
        var res = await productRepo.UpdateAsync(product, ct);
        Console.WriteLine(res != null ? "Quantity updated." : "Failed to update quantity.");
    }

    static async Task ListAllStores(IStoreRepository storeRepo, CancellationToken ct)
    {
        var stores = await storeRepo.GetAllAsync(ct);
        if (stores.Count == 0) { Console.WriteLine("No stores found."); return; }
        Console.WriteLine("Stores:");
        foreach (var s in stores)
        {
            Console.WriteLine($"{s.Id} | {s.Name} | {s.Address}");
        }
    }

    static async Task ViewCompanyAccounts(ICompanyAccountRepository companyAccountRepo, CancellationToken ct)
    {
        var accounts = await companyAccountRepo.GetAllAsync(ct);
        if (accounts.Count == 0) { Console.WriteLine("No company accounts found."); return; }
        Console.WriteLine("Company accounts:");
        foreach (var a in accounts)
        {
            Console.WriteLine($"{a.Id} | Balance: {a.Balance}");
        }
    }

    static async Task StoreManagerMenuLoop(Store store, IProductRepository productRepo, IOrderRepository orderRepo, IOrderItemRepository orderItemRepo, IStoreAccountRepository storeAccountRepo, ICompanyAccountRepository companyAccountRepo, ITransactionRepository transactionRepo)
    {
        var ct = CancellationToken.None;
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"Store Manager - {store.Name} - Available commands:");
            Console.WriteLine("1. List all orders for my store");
            Console.WriteLine("2. Create new order");
            Console.WriteLine("3. Transfer money to company");
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
                        await ListMyOrders(store, productRepo, orderRepo, orderItemRepo, ct);
                        break;
                    case "2":
                        await CreateOrderInteractive(store, productRepo, orderRepo, orderItemRepo, ct);
                        break;
                    case "3":
                        await TransferMoneyInteractive(store, storeAccountRepo, companyAccountRepo, transactionRepo, ct);
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

            await Task.Delay(10);
        }
    }

    static async Task ListMyOrders(Store store, IProductRepository productRepo, IOrderRepository orderRepo, IOrderItemRepository orderItemRepo, CancellationToken ct)
    {
        var orders = await orderRepo.GetByStoreIdAsync(store.Id, ct);
        if (orders.Count == 0) { Console.WriteLine("No orders found for your store."); return; }
        foreach (var o in orders)
        {
            Console.WriteLine($"Order {o.Id} | Total: {o.TotalAmount} | Created: {o.CreatedAt}");
            var items = await orderItemRepo.GetByOrderIdAsync(o.Id, ct);
            foreach (var it in items)
            {
                var p = await productRepo.ReadAsync(it.ProductId, ct);
                var pname = p?.Name ?? it.ProductId.ToString();
                Console.WriteLine($"  - {pname} x{it.Quantity} @ {it.UnitPrice}");
            }
        }
    }

    static async Task CreateOrderInteractive(Store store, IProductRepository productRepo, IOrderRepository orderRepo, IOrderItemRepository orderItemRepo, CancellationToken ct)
    {
        var items = new List<OrderItem>();
        while (true)
        {
            Console.Write("Product name (or 'done' to finish): ");
            var pname = Console.ReadLine()?.Trim() ?? string.Empty;
            if (pname.Equals("done", StringComparison.OrdinalIgnoreCase)) break;
            if (string.IsNullOrEmpty(pname)) { Console.WriteLine("Name required"); continue; }
            var product = await productRepo.GetByNameAsync(pname, ct);
            if (product == null) { Console.WriteLine("Product not found"); continue; }
            Console.Write("Quantity: ");
            if (!int.TryParse(Console.ReadLine(), out var qty) || qty <= 0) { Console.WriteLine("Invalid quantity"); continue; }
            if (qty > product.QuantityInStock) { Console.WriteLine("Not enough stock"); continue; }
            var oi = new OrderItem { Id = Guid.NewGuid(), ProductId = product.Id, Quantity = qty, UnitPrice = product.Price };
            items.Add(oi);
            Console.WriteLine("Item added to order.");
        }

        if (items.Count == 0) { Console.WriteLine("No items added. Order cancelled."); return; }

        decimal total = 0;
        foreach (var it in items) total += it.Quantity * it.UnitPrice;

        var order = new Order { Id = Guid.NewGuid(), StoreId = store.Id, TotalAmount = total, CreatedAt = DateTime.UtcNow };
        var createdOrderId = await orderRepo.CreateAsync(order, ct);
        if (createdOrderId == null) { Console.WriteLine("Failed to create order."); return; }

        foreach (var it in items)
        {
            it.OrderId = order.Id;
            await orderItemRepo.CreateAsync(it, ct);
            // decrement product stock
            var prod = await productRepo.ReadAsync(it.ProductId, ct);
            if (prod != null)
            {
                prod.QuantityInStock -= it.Quantity;
                await productRepo.UpdateAsync(prod, ct);
            }
        }

        Console.WriteLine($"Order created {order.Id} total {order.TotalAmount}");
    }

    static async Task TransferMoneyInteractive(Store store, IStoreAccountRepository storeAccountRepo, ICompanyAccountRepository companyAccountRepo, ITransactionRepository transactionRepo, CancellationToken ct)
    {
        Console.Write("Amount to transfer: ");
        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0) { Console.WriteLine("Invalid amount"); return; }

        Console.Write("Store account id: ");
        var storeAccInput = Console.ReadLine()?.Trim() ?? string.Empty;
        if (!Guid.TryParse(storeAccInput, out var storeAccId)) { Console.WriteLine("Invalid store account id"); return; }

        var storeAcc = await storeAccountRepo.ReadAsync(storeAccId, ct);
        if (storeAcc == null) { Console.WriteLine("Store account not found"); return; }
        if (storeAcc.StoreId != store.Id) { Console.WriteLine("This store account does not belong to your store"); return; }

        Console.Write("Company account id: ");
        var compAccInput = Console.ReadLine()?.Trim() ?? string.Empty;
        if (!Guid.TryParse(compAccInput, out var compAccId)) { Console.WriteLine("Invalid company account id"); return; }

        var compAcc = await companyAccountRepo.ReadAsync(compAccId, ct);
        if (compAcc == null) { Console.WriteLine("Company account not found"); return; }

        if (storeAcc.Balance < amount) { Console.WriteLine("Insufficient funds in store account"); return; }

        storeAcc.Balance -= amount;
        compAcc.Balance += amount;

        var upd1 = await storeAccountRepo.UpdateAsync(storeAcc, ct);
        var upd2 = await companyAccountRepo.UpdateAsync(compAcc, ct);
        if (upd1 == null || upd2 == null) { Console.WriteLine("Failed to update accounts"); return; }

        var tx = new Transaction { Id = Guid.NewGuid(), StoreAccountId = storeAcc.Id, CompanyAccountId = compAcc.Id, Amount = amount };
        var txId = await transactionRepo.CreateAsync(tx, ct);
        Console.WriteLine(txId != null ? "Transfer completed." : "Transfer failed when creating transaction record.");
    }
}
