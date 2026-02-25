using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MyOrg.AzureFuncs;

public class NewPurchaseWebhook
{
    private readonly ILogger<NewPurchaseWebhook> _logger;

    public NewPurchaseWebhook(ILogger<NewPurchaseWebhook> logger)
    {
        _logger = logger;
    }

    record NewOrderWorkbook(int ProductId, int Quantity, 
        string CustomerName, string CustomerEmail, decimal PurchasePrice);

    [Function(nameof(NewPurchaseWebhook))]
    public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = "purchase")]
     HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var order = await req.ReadFromJsonAsync<NewOrderWorkbook>();
        if (order == null)
        {
            return new BadRequestObjectResult("Order not found or invalid.");
        }
        return new OkObjectResult($"{order.CustomerName} purchased product {order.ProductId}");
    }

    [Function(nameof(GetPurchase))]
    public IActionResult GetPurchase([HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var name = req.Query["name"].ToString() ?? "";
        return new OkObjectResult($"Welcome {name}!");
    }
}