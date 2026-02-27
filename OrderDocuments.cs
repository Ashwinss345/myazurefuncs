using System.ComponentModel.DataAnnotations;

public class OrderDocument
{
    public string OrderId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public decimal PurchasePrice { get; set; }
}