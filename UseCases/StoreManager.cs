using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;

namespace UseCases;

public class StoreManager
{
    private readonly IProductRepository _productRepo;
    private readonly IOrderRepository _orderRepo;
    private readonly IOrderItemRepository _orderItemRepo;
    private readonly IStoreAccountRepository _storeAccountRepo;
    private readonly ICompanyAccountRepository _companyAccountRepo;
    private readonly ITransactionRepository _transactionRepo;

    public StoreManager(IProductRepository productRepo, IOrderRepository orderRepo, IOrderItemRepository orderItemRepo, IStoreAccountRepository storeAccountRepo, ICompanyAccountRepository companyAccountRepo, ITransactionRepository transactionRepo)
    {
        _productRepo = productRepo;
        _orderRepo = orderRepo;
        _orderItemRepo = orderItemRepo;
        _storeAccountRepo = storeAccountRepo;
        _companyAccountRepo = companyAccountRepo;
        _transactionRepo = transactionRepo;
    }

    public async Task ListMyOrders(Store store, CancellationToken ct)
    {
        var orders = await _orderRepo.GetByStoreIdAsync(store.Id, ct);
        if (orders.Count == 0) { Console.WriteLine("No orders found for your store."); return; }
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

    public async Task CreateOrderInteractive(Store store, CancellationToken ct)
    {
        var items = new List<OrderItem>();
        while (true)
        {
            Console.Write("Product name (or 'done' to finish): ");
            var pname = Console.ReadLine()?.Trim() ?? string.Empty;
            if (pname.Equals("done", StringComparison.OrdinalIgnoreCase)) break;
            if (string.IsNullOrEmpty(pname)) { Console.WriteLine("Name required"); continue; }
            var product = await _productRepo.GetByNameAsync(pname, ct);
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
        var createdOrderId = await _orderRepo.CreateAsync(order, ct);
        if (createdOrderId == null) { Console.WriteLine("Failed to create order."); return; }

        foreach (var it in items)
        {
            it.OrderId = order.Id;
            await _orderItemRepo.CreateAsync(it, ct);
            // decrement product stock
            var prod = await _productRepo.ReadAsync(it.ProductId, ct);
            if (prod != null)
            {
                prod.QuantityInStock -= it.Quantity;
                await _productRepo.UpdateAsync(prod, ct);
            }
        }

        Console.WriteLine($"Order created {order.Id} total {order.TotalAmount}");
    }

    public async Task TransferMoneyInteractive(Store store, CancellationToken ct)
    {
        Console.Write("Amount to transfer: ");
        if (!decimal.TryParse(Console.ReadLine(), out var amount) || amount <= 0) { Console.WriteLine("Invalid amount"); return; }

        Console.Write("Store account id: ");
        var storeAccInput = Console.ReadLine()?.Trim() ?? string.Empty;
        if (!Guid.TryParse(storeAccInput, out var storeAccId)) { Console.WriteLine("Invalid store account id"); return; }

        var storeAcc = await _storeAccountRepo.ReadAsync(storeAccId, ct);
        if (storeAcc == null) { Console.WriteLine("Store account not found"); return; }
        if (storeAcc.StoreId != store.Id) { Console.WriteLine("This store account does not belong to your store"); return; }

        Console.Write("Company account id: ");
        var compAccInput = Console.ReadLine()?.Trim() ?? string.Empty;
        if (!Guid.TryParse(compAccInput, out var compAccId)) { Console.WriteLine("Invalid company account id"); return; }

        var compAcc = await _companyAccountRepo.ReadAsync(compAccId, ct);
        if (compAcc == null) { Console.WriteLine("Company account not found"); return; }

        var txId = await _transactionRepo.TransferStoreToCompany(compAccId, storeAccId, amount, ct);

        Console.WriteLine("Transfer completed.");
    }

    public async Task RunAsync(Store store, CancellationToken ct)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"Store manager — {store.Name}");
            Console.WriteLine("1. List orders for my store");
            Console.WriteLine("2. Create new order");
            Console.WriteLine("3. Transfer money to company");
            Console.WriteLine("0. Logout / Exit");
            Console.Write("Command: ");
            var cmd = Console.ReadLine()?.Trim() ?? string.Empty;
            if (cmd == "0") break;
            try
            {
                switch (cmd)
                {
                    case "1":
                        await ListMyOrders(store, ct);
                        break;
                    case "2":
                        await CreateOrderInteractive(store, ct);
                        break;
                    case "3":
                        await TransferMoneyInteractive(store, ct);
                        break;
                    default:
                        Console.WriteLine("Unknown command.");
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
