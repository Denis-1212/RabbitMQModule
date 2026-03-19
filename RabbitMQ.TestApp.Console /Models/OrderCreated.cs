namespace RabbitMQ.TestApp.Console.Models;

public class OrderCreated
{

    #region Properties

    public string OrderId { get; set; } = Guid.NewGuid().ToString("N");
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public List<string> Items { get; set; } = [];

    #endregion

    #region Methods

    public override string ToString()
    {
        return $"Заказ #{OrderId[..8]}: {CustomerName} на сумму {Amount:C} ({Items.Count} товаров)";
    }

    #endregion

}
