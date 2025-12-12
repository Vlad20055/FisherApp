using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Npgsql;

namespace Infrastructure.Repositories;

public class OrderItemRepository : IOrderItemRepository
{
    private readonly string _connectionString;

    public OrderItemRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid?> CreateAsync(OrderItem entity, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        const string sql = "INSERT INTO order_items (id, order_id, product_id, quantity, unit_price) VALUES (@id, @order_id, @product_id, @quantity, @unit_price)";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("order_id", entity.OrderId);
        cmd.Parameters.AddWithValue("product_id", entity.ProductId);
        cmd.Parameters.AddWithValue("quantity", entity.Quantity);
        cmd.Parameters.AddWithValue("unit_price", entity.UnitPrice);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM order_items WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<OrderItem?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, order_id, product_id, quantity, unit_price FROM order_items WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new OrderItem
        {
            Id = reader.GetGuid(0),
            OrderId = reader.GetGuid(1),
            ProductId = reader.GetGuid(2),
            Quantity = reader.GetInt32(3),
            UnitPrice = reader.GetDecimal(4)
        };
    }

    public async Task<Guid?> UpdateAsync(OrderItem entity, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE order_items SET order_id = @order_id, product_id = @product_id, quantity = @quantity, unit_price = @unit_price WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entity.Id);
        cmd.Parameters.AddWithValue("order_id", entity.OrderId);
        cmd.Parameters.AddWithValue("product_id", entity.ProductId);
        cmd.Parameters.AddWithValue("quantity", entity.Quantity);
        cmd.Parameters.AddWithValue("unit_price", entity.UnitPrice);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? entity.Id : null;
    }

    // Helpers
    public async Task<List<OrderItem>> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, order_id, product_id, quantity, unit_price FROM order_items WHERE order_id = @order_id";
        var list = new List<OrderItem>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("order_id", orderId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new OrderItem
            {
                Id = reader.GetGuid(0),
                OrderId = reader.GetGuid(1),
                ProductId = reader.GetGuid(2),
                Quantity = reader.GetInt32(3),
                UnitPrice = reader.GetDecimal(4)
            });
        }
        return list;
    }
}
