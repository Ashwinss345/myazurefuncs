using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MyOrg.AzureFuncs;

public class OnNewBlob
{
    private readonly ILogger<OnNewBlob> _logger;

    public OnNewBlob(ILogger<OnNewBlob> logger)
    {
        _logger = logger;
    }

    [Function(nameof(OnNewBlob))]
    public async Task Run([BlobTrigger("tickets/{name}", Connection = "AzureWebJobsStorage")] 
    Stream stream, string name)
    {
        using var blobStreamReader = new StreamReader(stream);
        var content = await blobStreamReader.ReadToEndAsync();
        _logger.LogInformation("C# Blob trigger function Processed blob\n Name: {name} \n Data: {content}", name, content);
    }

    [Function(nameof(OnNewBlob2))]
    public async Task OnNewBlob2([BlobTrigger("tickets2/{name}", Connection = "AzureWebJobsStorage")] 
    BlobClient blobclient, string name)
    {
        var content = (await blobclient.DownloadContentAsync()).Value.Content.ToString();
        var props = await blobclient.GetPropertiesAsync();

        _logger.LogInformation($"C# Blob trigger function Processed blob\n Name: {name} \n" + 
        $"Data: {content} \n" +
        $"{props.Value.ContentType} {props.Value.ContentLength} {props.Value.LastModified}");        
    }
}