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
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "purchase")]
     HttpRequest req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");
        var order = await req.ReadFromJsonAsync<NewOrderWorkbook>();
        if (order == null) throw new ArgumentException("Order not found or invalid.");
        
        return await Task.FromResult(new NewPurchaseWebhookResponse{
            Message = new NewOrderMessage(Guid.NewGuid(), order.ProductId, order.Quantity, order.CustomerName, order.CustomerEmail, order.PurchasePrice),
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
