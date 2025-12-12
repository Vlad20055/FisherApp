using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Npgsql;

namespace Infrastructure.Repositories;

public class TransactionRepository : ITransactionRepository
{
    private readonly string _connectionString;

    public TransactionRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid?> CreateAsync(Transaction entity, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;

        const string sql = @"INSERT INTO transactions (id, store_account_id, company_account_id, amount)
                             VALUES (@id, @store_account_id, @company_account_id, @amount)";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("store_account_id", entity.StoreAccountId);
        cmd.Parameters.AddWithValue("company_account_id", entity.CompanyAccountId);
        cmd.Parameters.AddWithValue("amount", entity.Amount);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM transactions WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Transaction?> ReadAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT id, store_account_id, company_account_id, amount
                             FROM transactions
                             WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        return new Transaction
        {
            Id = reader.GetGuid(0),
            StoreAccountId = reader.GetGuid(1),
            CompanyAccountId = reader.GetGuid(2),
            Amount = reader.GetDecimal(3)
        };
    }

    public async Task<Guid?> UpdateAsync(Transaction entity, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE transactions
                             SET store_account_id = @store_account_id,
                                 company_account_id = @company_account_id,
                                 amount = @amount
                             WHERE id = @id";

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entity.Id);
        cmd.Parameters.AddWithValue("store_account_id", entity.StoreAccountId);
        cmd.Parameters.AddWithValue("company_account_id", entity.CompanyAccountId);
        cmd.Parameters.AddWithValue("amount", entity.Amount);

        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? entity.Id : null;
    }

    // Helper
    public async Task<List<Transaction>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, store_account_id, company_account_id, amount FROM transactions";
        var list = new List<Transaction>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Transaction
            {
                Id = reader.GetGuid(0),
                StoreAccountId = reader.GetGuid(1),
                CompanyAccountId = reader.GetGuid(2),
                Amount = reader.GetDecimal(3)
            });
        }
        return list;
    }
}
