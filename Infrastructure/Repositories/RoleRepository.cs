using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Npgsql;

namespace Infrastructure.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly string _connectionString;

    public RoleRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid?> CreateAsync(Role entity, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        const string sql = "INSERT INTO roles (id, name) VALUES (@id, @name)";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", entity.Name);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM roles WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Role?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name FROM roles WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Role { Id = reader.GetGuid(0), Name = reader.GetString(1) };
    }

    public async Task<Guid?> UpdateAsync(Role entity, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE roles SET name = @name WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entity.Id);
        cmd.Parameters.AddWithValue("name", entity.Name);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? entity.Id : null;
    }

    // Helpers
    public async Task<Role?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name FROM roles WHERE name = @name";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Role { Id = reader.GetGuid(0), Name = reader.GetString(1) };
    }

    public async Task<List<Role>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name FROM roles";
        var list = new List<Role>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Role { Id = reader.GetGuid(0), Name = reader.GetString(1) });
        }
        return list;
    }
}
