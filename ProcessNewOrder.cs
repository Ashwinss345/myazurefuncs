using System;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MyOrg.AzureFuncs;

public record NewOrderMessage(int ProductId, int Quantity, string CustomerName, string CustomerEmail, decimal PurchasePrice);
public class ProcessNewOrder
{
    private readonly ILogger<ProcessNewOrder> _logger;

    public ProcessNewOrder(ILogger<ProcessNewOrder> logger)
    {
        _logger = logger;
    }
    
    [Function(nameof(ProcessNewOrder))]
    public void Run([QueueTrigger("neworders", Connection = "AzureWebJobsStorage")] NewOrderMessage message)
    {
        _logger.LogInformation($"C# Queue trigger function processed: {message.CustomerName} bought {message.Quantity} of product {message.ProductId} for ${message.PurchasePrice}");
    }
}


