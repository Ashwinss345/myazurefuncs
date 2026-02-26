using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MyOrg.AzureFuncs;

public record NewOrderMessage(Guid OrderId, int ProductId, int Quantity, 
        string CustomerName, string CustomerEmail, decimal PurchasePrice);
public class ProcessNewOrder
{
    private readonly ILogger<ProcessNewOrder> _logger;

    public ProcessNewOrder(ILogger<ProcessNewOrder> logger)
    {
        _logger = logger;
    }
    
    [Function(nameof(ProcessNewOrder))]
    [BlobOutput("tickets/{orderId}.txt", Connection = "AzureWebJobsStorage")]
    public string Run([QueueTrigger("neworders", Connection = "AzureWebJobsStorage")] NewOrderMessage message)
    {
        var description = $"Order {message.ProductId}: " + 
            $"{message.CustomerName} bought {message.Quantity} of product {message.ProductId} for ${message.PurchasePrice}";
        _logger.LogInformation(description);
        return description;
    }
}


