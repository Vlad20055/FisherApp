using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Npgsql;

namespace Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly string _connectionString;

    public OrderRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid?> CreateAsync(Order entity, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        const string sql = "INSERT INTO orders (id, store_id, total_amount, created_at) VALUES (@id, @store_id, @total_amount, @created_at)";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("store_id", entity.StoreId);
        cmd.Parameters.AddWithValue("total_amount", entity.TotalAmount);
        cmd.Parameters.AddWithValue("created_at", entity.CreatedAt);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM orders WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Order?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, store_id, total_amount, created_at FROM orders WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Order
        {
            Id = reader.GetGuid(0),
            StoreId = reader.GetGuid(1),
            TotalAmount = reader.GetDecimal(2),
            CreatedAt = reader.GetDateTime(3)
        };
    }

    public async Task<Guid?> UpdateAsync(Order entity, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE orders SET store_id = @store_id, total_amount = @total_amount, created_at = @created_at WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entity.Id);
        cmd.Parameters.AddWithValue("store_id", entity.StoreId);
        cmd.Parameters.AddWithValue("total_amount", entity.TotalAmount);
        cmd.Parameters.AddWithValue("created_at", entity.CreatedAt);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? entity.Id : null;
    }

    // Helpers
    public async Task<List<Order>> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, store_id, total_amount, created_at FROM orders WHERE store_id = @store_id ORDER BY created_at DESC";
        var list = new List<Order>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("store_id", storeId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Order
            {
                Id = reader.GetGuid(0),
                StoreId = reader.GetGuid(1),
                TotalAmount = reader.GetDecimal(2),
                CreatedAt = reader.GetDateTime(3)
            });
        }
        return list;
    }

    public async Task<List<Order>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, store_id, total_amount, created_at FROM orders ORDER BY created_at DESC";
        var list = new List<Order>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Order
            {
                Id = reader.GetGuid(0),
                StoreId = reader.GetGuid(1),
                TotalAmount = reader.GetDecimal(2),
                CreatedAt = reader.GetDateTime(3)
            });
        }
        return list;
    }
}
