using az204_image_processor.Models;
using az204_image_processor.Services;
using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();


builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights()
    .AddSingleton<IImageResizeService, ImageResizeService>()
    .AddSingleton<IThumbnailService, ThumbnailService>()
    .AddSingleton<IBlobStorageService, BlobStorageService>()
    .Configure<ImageProcessingOptions>(options =>
    {
        options.MaxFileSizeBytes = long.Parse(Environment.GetEnvironmentVariable("MaxFileSizeBytes") ?? "10485760");
        options.MaxProcessedWidth = int.Parse(Environment.GetEnvironmentVariable("MaxProcessedWidth") ?? "1920");
        options.MaxProcessedHeight = int.Parse(Environment.GetEnvironmentVariable("MaxProcessedHeight") ?? "1080");
        options.ThumbnailWidth = int.Parse(Environment.GetEnvironmentVariable("ThumbnailWidth") ?? "150");
        options.ThumbnailHeight = int.Parse(Environment.GetEnvironmentVariable("ThumbnailHeight") ?? "150");
    })
    .Configure<ThumbnailOptions>(options =>
    {
        // Uses default values from ThumbnailOptions class
    })
    .AddSingleton(_ =>
    {
        var connectionString = Environment.GetEnvironmentVariable("ImageStorageConnection");
        return new BlobServiceClient(connectionString);
    })
    .AddHttpClient("CognitiveServices", client =>
     {
         client.Timeout = TimeSpan.FromSeconds(30);
     });

builder.Logging.SetMinimumLevel(LogLevel.Information);




builder.Build().Run();
