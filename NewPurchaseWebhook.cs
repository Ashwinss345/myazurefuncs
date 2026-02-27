using System.Buffers.Text;
using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace MyOrg.AzureFuncs;

public class NewPurchaseWebhookResponse
{
    [QueueOutput(nameof(NewOrderMessage), Connection = "AzureWebJobsStorage")]
    public NewOrderMessage? Message { get; set; }
    
    [HttpResult]
    public IActionResult? Result { get; set; }

    [CosmosDBOutput("azurefuncs","Orders", Connection = "CosmosDbConnection")]
    public OrderDocument? OrderDocument { get; set; }
}
public class NewPurchaseWebhook
{
    private readonly ILogger<NewPurchaseWebhook> _logger;

    public NewPurchaseWebhook(ILogger<NewPurchaseWebhook> logger)
    {
        _logger = logger;
    }

    record NewOrderWorkbook(int ProductId, int Quantity, 
        string CustomerName, string CustomerEmail, decimal PurchasePrice);

    [Function(nameof(NewPurchaseWebhookResponse))]
    public async Task<NewPurchaseWebhookResponse> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "purchase")] HttpRequest req)
        
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var order = await req.ReadFromJsonAsync<NewOrderWorkbook>();
        if (order == null) throw new ArgumentException("Order not found or invalid.");
        
        NewOrderMessage message = new(Guid.NewGuid(), order.ProductId, order.Quantity, order.CustomerName, order.CustomerEmail, order.PurchasePrice);

        _logger.LogInformation("New order received: " +
            $"{message.OrderId} bought {order.Quantity} of product {order.ProductId} for ${order.PurchasePrice} customer details: {order.CustomerName}, {order.CustomerEmail}");
        
        var document1 = new OrderDocument
        {
            OrderId = message.OrderId.ToString(),
            ProductId = order.ProductId,
            Quantity = order.Quantity,            
            CustomerName = order.CustomerName,
            CustomerEmail = order.CustomerEmail,
            PurchasePrice = order.PurchasePrice
        };

        var document = new OrderDocument
        {
            OrderId = "testorderid",
            ProductId = 123,
            Quantity = 10,            
            CustomerName = "testcustomer",
            CustomerEmail = "testcustomer@example.com",
            PurchasePrice = 999.99M
        };

        _logger.LogInformation("New order received: " + 
            $"{document.OrderId} bought {document.Quantity} of product {document.ProductId} for ${document.PurchasePrice} customer details: {document.CustomerName}, {document.CustomerEmail}");

        return await Task.FromResult(new NewPurchaseWebhookResponse {
            OrderDocument = document,
            Message = message,
            Result = new OkObjectResult($"Thanks {order.CustomerName} for purchasing {order.Quantity} of product {order.ProductId} for ${order.PurchasePrice}")
        });        
    }

    [Function(nameof(GetPurchase))]
    public IActionResult GetPurchase(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "purchase/{orderId:guid}")] HttpRequest req, 
        [BlobInput("tickets/{orderId}.txt", Connection = "AzureWebJobsStorage")] BlobClient ticketClient,
        Guid orderId)
    {
        _logger.LogInformation($"Requested details of {orderId}");

        try
        {
            var ticketContent = ticketClient.DownloadContent().Value.Content.ToString();
            _logger.LogInformation($"Ticket content for {orderId}: {ticketContent}");

            return new OkObjectResult(ticketContent);
        }

        catch (RequestFailedException ex) when (ex.ErrorCode == "BlobNotFound")
        {
            _logger.LogError(ex, $"Error fetching details of {orderId}");
            return new NotFoundObjectResult($"No details found for {orderId}");
        }
        
    }
}
