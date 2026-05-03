using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;

namespace UseCases;

public class CompanyManager
{
    private readonly IProductRepository _productRepo;
    private readonly ICategoryRepository _categoryRepo;
    private readonly IStoreRepository _storeRepo;
    private readonly ICompanyAccountRepository _companyAccountRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IOrderItemRepository _orderItemRepo;

    public CompanyManager(IProductRepository productRepo, ICategoryRepository categoryRepo, IStoreRepository storeRepo, ICompanyAccountRepository companyAccountRepo, IOrderRepository orderRepo, IOrderItemRepository orderItemRepo)
    {
        _productRepo = productRepo;
        _categoryRepo = categoryRepo;
        _storeRepo = storeRepo;
        _companyAccountRepo = companyAccountRepo;
        _orderRepo = orderRepo;
        _orderItemRepo = orderItemRepo;
    }

    public async Task RunAsync(CancellationToken ct)
    {
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
                        await AddStockByName(ct);
                        break;
                    case "2":
                        await AddNewProduct(ct);
                        break;
                    case "3":
                        await AddNewCategory(ct);
                        break;
                    case "4":
                        await UpdateQuantityByName(ct);
                        break;
                    case "5":
                        await ListAllStores(ct);
                        break;
                    case "6":
                        await ViewCompanyAccounts(ct);
                        break;
                    case "7":
                        await ViewOrdersForStoreByName(ct);
                        break;
                    case "8":
                        await UpdateProductPriceByName(ct);
                        break;
                    case "9":
                        await DeleteCategory(ct);
                        break;
                    case "10":
                        await DeleteProduct(ct);
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

    public async Task AddStockByName(CancellationToken ct)
    {
        Console.Write("Product name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var product = await _productRepo.GetByNameAsync(name, ct);
        if (product == null)
        {
            Console.WriteLine("Product not found.");
            return;
        }
        Console.Write("Amount to add: ");
        if (!int.TryParse(Console.ReadLine(), out var add) || add <= 0) { Console.WriteLine("Invalid amount"); return; }
        product.QuantityInStock += add;
        var res = await _productRepo.UpdateAsync(product, ct);
        Console.WriteLine(res != null ? "Stock updated." : "Failed to update stock.");
    }

    public async Task AddNewProduct(CancellationToken ct)
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

        var category = await _categoryRepo.GetByNameAsync(catName, ct);
        if (category == null)
        {
            Console.Write($"Category '{catName}' does not exist. Create it now? (y/n): ");
            var ans = Console.ReadLine()?.Trim().ToLower() ?? "n";
            if (ans == "y" || ans == "yes")
            {
                var newCat = new Category { Id = Guid.NewGuid(), Name = catName };
                var catId = await _categoryRepo.CreateAsync(newCat, ct);
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
        var created = await _productRepo.CreateAsync(product, ct);
        Console.WriteLine(created != null ? $"Product created with id {created}" : "Failed to create product.");
    }

    public async Task AddNewCategory(CancellationToken ct)
    {
        Console.Write("Category name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var exists = await _categoryRepo.GetByNameAsync(name, ct);
        if (exists != null) { Console.WriteLine("Category already exists."); return; }
        var cat = new Category { Id = Guid.NewGuid(), Name = name };
        var created = await _categoryRepo.CreateAsync(cat, ct);
        Console.WriteLine(created != null ? "Category created." : "Failed to create category.");
    }

    public async Task UpdateQuantityByName(CancellationToken ct)
    {
        Console.Write("Product name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var product = await _productRepo.GetByNameAsync(name, ct);
        if (product == null) { Console.WriteLine("Product not found"); return; }
        Console.Write("New quantity: ");
        if (!int.TryParse(Console.ReadLine(), out var newQty) || newQty < 0) { Console.WriteLine("Invalid quantity"); return; }
        product.QuantityInStock = newQty;
        var res = await _productRepo.UpdateAsync(product, ct);
        Console.WriteLine(res != null ? "Quantity updated." : "Failed to update quantity.");
    }

    public async Task ListAllStores(CancellationToken ct)
    {
        var stores = await _storeRepo.GetAllAsync(ct);
        if (stores.Count == 0) { Console.WriteLine("No stores found."); return; }
        Console.WriteLine("Stores:");
        foreach (var s in stores)
        {
            Console.WriteLine($"{s.Id} | {s.Name} | {s.Address}");
        }
    }

    public async Task ViewCompanyAccounts(CancellationToken ct)
    {
        var accounts = await _companyAccountRepo.GetAllAsync(ct);
        if (accounts.Count == 0) { Console.WriteLine("No company accounts found."); return; }
        Console.WriteLine("Company accounts:");
        foreach (var a in accounts)
        {
            Console.WriteLine($"{a.Id} | Balance: {a.Balance}");
        }
    }

    public async Task ViewOrdersForStoreByName(CancellationToken ct)
    {
        Console.Write("Store name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var store = await _storeRepo.GetByNameAsync(name, ct);
        if (store == null) { Console.WriteLine("Store not found"); return; }
        var orders = await _orderRepo.GetByStoreIdAsync(store.Id, ct);
        if (orders.Count == 0) { Console.WriteLine("No orders for this store."); return; }
        foreach (var o in orders)
        {
            Console.WriteLine($"Order {o.Id} | Total: {o.TotalAmount} | Created: {o.CreatedAt}");
            var items = await _orderItemRepo.GetByOrderIdAsync(o.Id, ct);
            foreach (var it in items)
            {
                var p = await _productRepo.ReadAsync(it.ProductId, ct);
                var pname = p?.Name ?? it.ProductId.ToString();
                Console.WriteLine($"  - {pname} x{it.Quantity} @ {it.UnitPrice}");
            }
        }
    }

    public async Task UpdateProductPriceByName(CancellationToken ct)
    {
        Console.Write("Product name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var product = await _productRepo.GetByNameAsync(name, ct);
        if (product == null) { Console.WriteLine("Product not found"); return; }
        Console.Write("New price: ");
        if (!decimal.TryParse(Console.ReadLine(), out var price) || price < 0) { Console.WriteLine("Invalid price"); return; }
        product.Price = price;
        var upd = await _productRepo.UpdateAsync(product, ct);
        Console.WriteLine(upd != null ? "Price updated." : "Failed to update price.");
    }

    public async Task DeleteCategory(CancellationToken ct)
    {
        Console.Write("Category name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var cat = await _categoryRepo.GetByNameAsync(name, ct);
        if (cat == null) { Console.WriteLine("Category not found."); return; }
        var deleted = await _categoryRepo.DeleteAsync(cat.Id, ct);
        Console.WriteLine(deleted != null ? "Category deleted." : "Failed to delete category (it may still be in use).");
    }

    public async Task DeleteProduct(CancellationToken ct)
    {
        Console.Write("Product name: ");
        var name = Console.ReadLine()?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) { Console.WriteLine("Name required"); return; }
        var product = await _productRepo.GetByNameAsync(name, ct);
        if (product == null) { Console.WriteLine("Product not found."); return; }
        var deleted = await _productRepo.DeleteAsync(product.Id, ct);
        Console.WriteLine(deleted != null ? "Product deleted." : "Failed to delete product (it may still be referenced).");
    }
}
