namespace Domain.Entities;

public class Transaction
{
    public Guid Id { get; set; }
    public Guid StoreAccountId { get; set; }
    public Guid CompanyAccountId { get; set; }
    public decimal Amount { get; set; }
}
