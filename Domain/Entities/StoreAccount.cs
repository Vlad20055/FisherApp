namespace Domain.Entities;

public class StoreAccount
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public decimal Balance { get; set; }
}
