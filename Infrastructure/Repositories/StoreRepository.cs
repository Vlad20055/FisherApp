using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Npgsql;

namespace Infrastructure.Repositories;

public class StoreRepository : IStoreRepository
{
    private readonly string _connectionString;

    public StoreRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid?> CreateAsync(Store entity, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        const string sql = "INSERT INTO stores (id, name, address, tax_id, manager_id) VALUES (@id, @name, @address, @tax_id, @manager_id)";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", entity.Name);
        cmd.Parameters.AddWithValue("address", entity.Address);
        cmd.Parameters.AddWithValue("tax_id", entity.TaxId);
        cmd.Parameters.AddWithValue("manager_id", entity.ManagerId);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM stores WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Store?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, address, tax_id, manager_id FROM stores WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Store
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            Address = reader.GetString(2),
            TaxId = reader.GetString(3),
            ManagerId = reader.GetGuid(4)
        };
    }

    public async Task<Guid?> UpdateAsync(Store entity, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE stores SET name = @name, address = @address, tax_id = @tax_id, manager_id = @manager_id WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entity.Id);
        cmd.Parameters.AddWithValue("name", entity.Name);
        cmd.Parameters.AddWithValue("address", entity.Address);
        cmd.Parameters.AddWithValue("tax_id", entity.TaxId);
        cmd.Parameters.AddWithValue("manager_id", entity.ManagerId);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? entity.Id : null;
    }

    // Helpers
    public async Task<Store?> GetByManagerIdAsync(Guid managerId, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, address, tax_id, manager_id FROM stores WHERE manager_id = @manager_id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("manager_id", managerId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Store
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            Address = reader.GetString(2),
            TaxId = reader.GetString(3),
            ManagerId = reader.GetGuid(4)
        };
    }

    public async Task<Store?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, address, tax_id, manager_id FROM stores WHERE name = @name";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Store
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            Address = reader.GetString(2),
            TaxId = reader.GetString(3),
            ManagerId = reader.GetGuid(4)
        };
    }

    public async Task<List<Store>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, address, tax_id, manager_id FROM stores";
        var list = new List<Store>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Store
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Address = reader.GetString(2),
                TaxId = reader.GetString(3),
                ManagerId = reader.GetGuid(4)
            });
        }
        return list;
    }
}
