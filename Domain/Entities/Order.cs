namespace Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid StoreId { get; set; }
    public DateTime CreatedAt { get; set; }
    public decimal TotalAmount { get; set; }
}
