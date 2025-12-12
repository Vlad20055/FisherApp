using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Entities;
using Domain.Repositories;
using Npgsql;

namespace Infrastructure.Repositories;

public class ProductRepository : IProductRepository
{
    private readonly string _connectionString;

    public ProductRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid?> CreateAsync(Product entity, CancellationToken cancellationToken)
    {
        var id = entity.Id == Guid.Empty ? Guid.NewGuid() : entity.Id;
        const string sql = @"INSERT INTO products (id, name, description, price, quantity_in_stock, category_id)
                             VALUES (@id, @name, @description, @price, @quantity_in_stock, @category_id)";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("name", entity.Name);
        cmd.Parameters.AddWithValue("description", entity.Description);
        cmd.Parameters.AddWithValue("price", entity.Price);
        cmd.Parameters.AddWithValue("quantity_in_stock", entity.QuantityInStock);
        cmd.Parameters.AddWithValue("category_id", entity.CategoryId);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Guid?> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "DELETE FROM products WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", id);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? id : null;
    }

    public async Task<Product?> ReadAsync(Guid Id, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT id, name, description, price, quantity_in_stock, category_id FROM products WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Id);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Product
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Price = reader.GetDecimal(3),
            QuantityInStock = reader.GetInt32(4),
            CategoryId = reader.GetGuid(5)
        };
    }

    public async Task<Guid?> UpdateAsync(Product entity, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE products SET name = @name, description = @description, price = @price, quantity_in_stock = @quantity_in_stock, category_id = @category_id WHERE id = @id";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", entity.Id);
        cmd.Parameters.AddWithValue("name", entity.Name);
        cmd.Parameters.AddWithValue("description", entity.Description);
        cmd.Parameters.AddWithValue("price", entity.Price);
        cmd.Parameters.AddWithValue("quantity_in_stock", entity.QuantityInStock);
        cmd.Parameters.AddWithValue("category_id", entity.CategoryId);
        var affected = await cmd.ExecuteNonQueryAsync(cancellationToken);
        return affected > 0 ? entity.Id : null;
    }

    // Helpers
    public async Task<Product?> GetByNameAsync(string name, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT id, name, description, price, quantity_in_stock, category_id FROM products WHERE name = @name";
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("name", name);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;
        return new Product
        {
            Id = reader.GetGuid(0),
            Name = reader.GetString(1),
            Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Price = reader.GetDecimal(3),
            QuantityInStock = reader.GetInt32(4),
            CategoryId = reader.GetGuid(5)
        };
    }

    public async Task<List<Product>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = "SELECT id, name, description, price, quantity_in_stock, category_id FROM products";
        var list = new List<Product>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);
        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new Product
            {
                Id = reader.GetGuid(0),
                Name = reader.GetString(1),
                Description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                Price = reader.GetDecimal(3),
                QuantityInStock = reader.GetInt32(4),
                CategoryId = reader.GetGuid(5)
            });
        }
        return list;
    }
}
