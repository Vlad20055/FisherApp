using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Npgsql;

namespace Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid?> CreateAsync(User entity, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        const string sql = @"INSERT INTO users (id, username, password_hash, full_name, role_id, is_active)
                             VALUES (@id, @username, @password_hash, @full_name, @role_id, @is_active)";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("username", entity.Username);
        cmd.Parameters.AddWithValue("password_hash", entity.PasswordHash);
        cmd.Parameters.AddWithValue("full_name", entity.FullName);
        cmd.Parameters.AddWithValue("role_id", entity.RoleId);
        cmd.Parameters.AddWithValue("is_active", entity.IsActive);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM users WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<User?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT id, username, password_hash, full_name, role_id, is_active
                             FROM users WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new User
        {
            Id = reader.GetGuid(0),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            FullName = reader.GetString(3),
            RoleId = reader.GetGuid(4),
            IsActive = reader.GetBoolean(5)
        };
    }

    public async Task<Guid?> UpdateAsync(User entity, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE users SET username = @username, password_hash = @password_hash, full_name = @full_name, role_id = @role_id, is_active = @is_active WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entity.Id);
        cmd.Parameters.AddWithValue("username", entity.Username);
        cmd.Parameters.AddWithValue("password_hash", entity.PasswordHash);
        cmd.Parameters.AddWithValue("full_name", entity.FullName);
        cmd.Parameters.AddWithValue("role_id", entity.RoleId);
        cmd.Parameters.AddWithValue("is_active", entity.IsActive);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? entity.Id : null;
    }

    // Convenience method used by UI: authenticate by username and password
    public async Task<User?> AuthenticateAsync(string username, string password, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT id, username, password_hash, full_name, role_id, is_active
                             FROM users WHERE username = @username AND password_hash = crypt(@password, password_hash) AND is_active = true";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("username", username);
        cmd.Parameters.AddWithValue("password", password);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new User
        {
            Id = reader.GetGuid(0),
            Username = reader.GetString(1),
            PasswordHash = reader.GetString(2),
            FullName = reader.GetString(3),
            RoleId = reader.GetGuid(4),
            IsActive = reader.GetBoolean(5)
        };
    }

    // Create user and hash password using Postgres crypt
    public async Task<Guid?> CreateWithPasswordAsync(User entity, string password, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        const string sql = @"INSERT INTO users (id, username, password_hash, full_name, role_id, is_active)
                             VALUES (@id, @username, crypt(@password, gen_salt('bf')), @full_name, @role_id, @is_active)";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("username", entity.Username);
        cmd.Parameters.AddWithValue("password", password);
        cmd.Parameters.AddWithValue("full_name", entity.FullName);
        cmd.Parameters.AddWithValue("role_id", entity.RoleId);
        cmd.Parameters.AddWithValue("is_active", entity.IsActive);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<List<User>> GetByRoleIdAsync(Guid roleId, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT id, username, password_hash, full_name, role_id, is_active
                             FROM users WHERE role_id = @role_id ORDER BY username";
        var list = new List<User>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("role_id", roleId);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new User
            {
                Id = reader.GetGuid(0),
                Username = reader.GetString(1),
                PasswordHash = reader.GetString(2),
                FullName = reader.GetString(3),
                RoleId = reader.GetGuid(4),
                IsActive = reader.GetBoolean(5)
            });
        }
        return list;
    }
}
