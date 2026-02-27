using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.Logging;

namespace MyOrg.AzureFuncs;


public class OrderOrchestrationInput
{
    public Guid OrderId { get; set; }
    public string[] SeatNumbers {get; set;} = [];
    public decimal TotalPrice { get; set; }
}

public class ApprovalResult
{
    public bool IsApproved { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
}

public static class NewOrderOrchestration
{
    public const string OrderApprovalEventName = "OrderApproval";

    [Function(nameof(NewOrderOrchestration))]
    public static async Task<List<string>> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context) {
        ILogger logger = context.CreateReplaySafeLogger(nameof(NewOrderOrchestration));
        logger.LogInformation("Saying hello.");

        var orderInput = context.GetInput<OrderOrchestrationInput>()!;

        /*
        var outputs = new List<string>();

        // Replace name and input with values relevant for your Durable Functions Activity
        outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Tokyo"));
        outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "Seattle"));
        outputs.Add(await context.CallActivityAsync<string>(nameof(SayHello), "London"));

        // returns ["Hello Tokyo!", "Hello Seattle!", "Hello London!"]
        */

        // check if the price is more than $500, need approval
        if (orderInput.TotalPrice > 500)
        {
           logger.LogInformation("Order {orderId} requires manual approval.", orderInput.OrderId);
           await context.CallActivityAsync(nameof(RequestApproval), orderInput.OrderId);

            try
            {
                var result = await context.WaitForExternalEvent<ApprovalResult>(
                    OrderApprovalEventName, TimeSpan.FromMinutes(1));

                if (!result.IsApproved)
                {
                    logger.LogInformation("Order {orderId} is rejected by {approver}.", orderInput.OrderId, result.ApprovedBy);
                    return [$"Order {orderInput.OrderId} rejected by {result.ApprovedBy}."];
                }
            }

            catch (TaskCanceledException)
            {
                logger.LogInformation("waiting for approval for order {orderId} timed out.", orderInput.OrderId);
                return [$"waiting for approval for order {orderInput.OrderId} timed out."];
            }
        }
        // run activities in parallel
        var tasks = new List<Task<string>>();

        foreach (var seat in orderInput.SeatNumbers)
        {
            tasks.Add(context.CallActivityAsync<string>(nameof(CreateTicket), seat));
        }

        string[] results = await Task.WhenAll(tasks);
        return [.. results];
    }

    [Function(nameof(RequestApproval))]
    public static void RequestApproval([ActivityTrigger] Guid orderId,     
    FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("RequestApproval");
        logger.LogInformation("Requesting approval for order {orderId}.", orderId);
    }

    [Function(nameof(CreateTicket))]
    public static string CreateTicket([ActivityTrigger] string seatNumber, 
    [BlobInput("tickets", Connection = "AzureWebJobsStorage")] BlobContainerClient blobContainerClient,
    FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("CreateTicket");
        logger.LogInformation("Creating ticket for seat {seat}.", seatNumber);
        return $"Ticket created for seat {seatNumber}";
    }

    [Function("NewOrderOrchestration_HttpStart")]
    public static async Task<HttpResponseData> HttpStart(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("NewOrderOrchestration_HttpStart");

        var price = req.Query["price"] ?? "100";

        var inputData = new OrderOrchestrationInput
        {
            OrderId = Guid.NewGuid(),
            SeatNumbers = ["A1", "A2", "A3"],
            TotalPrice = decimal.Parse(price)
        };

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(NewOrderOrchestration), inputData);
        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        // Returns an HTTP 202 response with an instance management payload.
        // See https://learn.microsoft.com/azure/azure-functions/durable/durable-functions-http-api#start-orchestration

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }

    [Function("NewOrderOrchestration_Approve")]
    public static async Task<IActionResult> Approve(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequest req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("NewOrderOrchestration_Approve");
        var approvalResult = await req.ReadFromJsonAsync<ApprovalResult>();
        
        var id = req.Query["id"];
        if  (string.IsNullOrEmpty(id) || approvalResult == null)
        {
            return new BadRequestObjectResult("Please provide instance ID and approval result in the request.");
        }

        logger.LogInformation("Raising approval event for instance ID = '{instanceId}'.", id);
        await client.RaiseEventAsync(id, OrderApprovalEventName, approvalResult);
        return new OkObjectResult($"Approval result for instance ID = '{id}' has been sent.");
    }
    
}