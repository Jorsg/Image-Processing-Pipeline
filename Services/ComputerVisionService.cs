using System.Diagnostics;
using az204_image_processor.Models;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using ImageTag = az204_image_processor.Models.ImageTag;

namespace az204_image_processor.Services
{
    public class ComputerVisionService : IComputerVisionService
    {
        private readonly ComputerVisionClient _client;
        private readonly ILogger<ComputerVisionService> _logger;
        private readonly AsyncRetryPolicy _retryPolicy;
        private readonly int _maxRetrice;


        public ComputerVisionService(ILogger<ComputerVisionService> logger)
        {
            _logger = logger;

            //Read config from enviroment
            var endpoint = Environment.GetEnvironmentVariable("ComputerVisionEndpoint") ??
                           throw new InvalidOperationException("ComputerVisionEndpoint not configured");

            var key = Environment.GetEnvironmentVariable("ComputerVisionkey") ??
                           throw new InvalidOperationException("ComputerVisionKey not configured");

            _maxRetrice = int.Parse(Environment.GetEnvironmentVariable("MaxRetriyAttempts") ?? "3");

            //Initialize the SDK client
            _client = new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
            {
                Endpoint = endpoint
            };

            //Poly rety with exponential backoff
            _retryPolicy = Policy.Handle<HttpRequestException>().
                        Or<ComputerVisionErrorResponseException>(ex =>
                                                                ex.Response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                                                                || (int)ex.Response.StatusCode >= 500)
                                                                   .WaitAndRetryAsync(
                                                                    retryCount: _maxRetrice,
                                                                    sleepDurationProvider: attempt =>
                                                                    TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                                                                    onRetry: (exception, timeSpan, attempt, _) =>
                                                                    {
                                                                        _logger.LogWarning($"Retry {attempt}/{_maxRetrice} for Computer Vision \n",
                                                                                 $"Api after {timeSpan.TotalSeconds}s Error: {exception.Message}");
                                                                    });
        }

        /// <summary>
        /// Analyze image: description, tags, categories, colors, adult content
        /// </summary>
        public async Task<VisionAnalysisResult> AnalyzeImageAsync(Stream imageStream)
        {
            var result = new VisionAnalysisResult();
            var stopwatch = Stopwatch.StartNew();
            var retryCount = 0;

            try
            {
                if (imageStream.CanSeek) imageStream.Position = 0;

                var features = new List<VisualFeatureTypes?>
                {
                  VisualFeatureTypes.Description,
                  VisualFeatureTypes.Tags,
                  VisualFeatureTypes.Categories,
                  VisualFeatureTypes.Color,
                  VisualFeatureTypes.Adult
                };

                var analysis = await _retryPolicy.ExecuteAsync(
                    async () =>
                    {
                        retryCount++;
                        if (imageStream.CanSeek) imageStream.Position = 0;

                        return await _client.AnalyzeImageInStreamAsync(imageStream, features);
                    });

                //Map tags
                result.Tags = analysis.Tags?
                .Select(t => new ImageTag
                {
                    Name = t.Name,
                    Confidence = t.Confidence

                })
                .OrderByDescending(t => t.Confidence)
                .ToList() ?? new List<ImageTag>();

                //Map Categories
                result.Categories = analysis.Categories?
                .Select(c => new ImageCategory
                {
                    Name = c.Name,
                    Score = c.Score

                })
                .ToList() ?? new List<ImageCategory>();

                //Map Color
                result.DomainantColorForeground =
                       analysis.Color?.DominantColorForeground ?? "";
                result.DomainantColorBackground =
                       analysis.Color?.DominantColorForeground ?? "";

                //Map adult content flags
                result.IsAdultContent =
                       analysis.Adult?.IsAdultContent ?? false;
                result.IsRacyContent =
                       analysis.Adult?.IsRacyContent ?? false;

                result.Success = true;

                _logger.LogInformation($"Analysis complete: \n{result.Description} \n{result.DescriptionConfidence} \n {result.Tags.Count} \n {result.Categories.Count}");


            }
            catch (System.Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex,
                    "❌ Image analysis failed after {Retries} attempts",
                    retryCount);
            }

            stopwatch.Stop();
            result.RetryCount = retryCount - 1; // First attempt isn't a retry
            result.ApiCalDurationMs = stopwatch.Elapsed.TotalMilliseconds;

            return result;
        }

         /// <summary>
        /// OCR: Extract text using the Read API (best for documents)
        /// </summary>

        public async Task<VisionAnalysisResult> ExtractTextAsync(Stream imageStream)
        {
            throw new NotImplementedException();
        }

        public async Task<VisionAnalysisResult> FullAnalysisAsync(Stream imageStream)
        {
            throw new NotImplementedException();
        }
    }
}