using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Npgsql;

namespace Infrastructure.Repositories;

public class StoreAccountRepository : IStoreAccountRepository
{
    private readonly string _connectionString;

    public StoreAccountRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid?> CreateAsync(StoreAccount entity, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        const string sql = "INSERT INTO store_accounts (id, balance, store_id) VALUES (@id, @balance, @store_id)";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("balance", entity.Balance);
        cmd.Parameters.AddWithValue("store_id", entity.StoreId);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM store_accounts WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<StoreAccount?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, balance, store_id FROM store_accounts WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new StoreAccount { Id = reader.GetGuid(0), Balance = reader.GetDecimal(1), StoreId = reader.GetGuid(2) };
    }

    public async Task<Guid?> UpdateAsync(StoreAccount entity, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE store_accounts SET balance = @balance, store_id = @store_id WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entity.Id);
        cmd.Parameters.AddWithValue("balance", entity.Balance);
        cmd.Parameters.AddWithValue("store_id", entity.StoreId);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? entity.Id : null;
    }

    // Helpers
    public async Task<StoreAccount?> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, balance, store_id FROM store_accounts WHERE store_id = @store_id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("store_id", storeId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new StoreAccount { Id = reader.GetGuid(0), Balance = reader.GetDecimal(1), StoreId = reader.GetGuid(2) };
    }

    public async Task<List<StoreAccount>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, balance, store_id FROM store_accounts";
        var list = new List<StoreAccount>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new StoreAccount { Id = reader.GetGuid(0), Balance = reader.GetDecimal(1), StoreId = reader.GetGuid(2) });
        }
        return list;
    }
}
