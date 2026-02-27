using Azure.Storage.Blobs;
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
}

public static class NewOrderOrchestration
{
    [Function(nameof(NewOrderOrchestration))]
    public static async Task<List<string>> RunOrchestrator(
        [OrchestrationTrigger] TaskOrchestrationContext context)
    {
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

        var tasks = new List<Task<string>>();

        foreach (var seat in orderInput.SeatNumbers)
        {
            tasks.Add(context.CallActivityAsync<string>(nameof(CreateTicket), seat));
        }

        string[] results = await Task.WhenAll(tasks);
        return [.. results];
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
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req,
        [DurableClient] DurableTaskClient client,
        FunctionContext executionContext)
    {
        ILogger logger = executionContext.GetLogger("NewOrderOrchestration_HttpStart");

        var inputData = new OrderOrchestrationInput
        {
            OrderId = Guid.NewGuid(),
            SeatNumbers = new string[] { "A1", "A2", "A3" }
        };

        string instanceId = await client.ScheduleNewOrchestrationInstanceAsync(
            nameof(NewOrderOrchestration), inputData);
        logger.LogInformation("Started orchestration with ID = '{instanceId}'.", instanceId);

        return await client.CreateCheckStatusResponseAsync(req, instanceId);
    }
}