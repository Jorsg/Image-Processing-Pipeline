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
            var result = new VisionAnalysisResult();
            var stopwatch = Stopwatch.StartNew();
            var retryCount = 0;

            try
            {
                if (imageStream.CanSeek) imageStream.Position = 0;

                ReadInStreamHeaders readResponse = null!;

                await _retryPolicy.ExecuteAsync(async () =>
                {
                    retryCount++;
                    if (imageStream.CanSeek)
                        imageStream.Position = 0;
                });

                //Extract operation ID from the response URL.
                var operationId = ExtractOperationId(readResponse.OperationLocation);

                //Poll for completion
                ReadOperationResult readResult;
                var maxPolls = 10;
                var pollCount = 0;

                do
                {
                    await Task.Delay(1000);
                    readResult = await _client.GetReadResultAsync(Guid.Parse(operationId));
                    pollCount++;

                    _logger.LogDebug(
                       $" OCR poll {pollCount}: {readResult.Status}");

                } while (readResult.Status == OperationStatusCodes.Running
                        && pollCount < maxPolls);

                //Extract text from results
                if (readResult.Status == OperationStatusCodes.Succeeded)
                {
                    var textLines = new List<TextLine>();
                    var allText = new List<string>();

                    foreach (var page in readResult.AnalyzeResult.ReadResults)
                    {
                        foreach (var line in page.Lines)
                        {
                            textLines.Add(new TextLine
                            {
                                Text = line.Text,
                                Confidence = line.Appearance?.Style?.Confidence ?? 0
                            });
                        }
                    }

                    result.TextLines = textLines;
                    result.ExtractedText = string.Join("\n", allText);
                    result.Success = true;

                    _logger.LogInformation(
                       $"OCR complete: {textLines.Count} lines, " +
                       $"{result.ExtractedText.Length} characters extracted");
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = $"OCR operation ended with status: {readResult.Status}";
                }
            }
            catch (System.Exception ex)
            {

                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex,
                    $"OCR failed after {retryCount} attempts");
            }

            stopwatch.Stop();
            result.RetryCount = retryCount - 1;
            result.ApiCalDurationMs = stopwatch.Elapsed.TotalMilliseconds;

            return result;
        }

        public async Task<VisionAnalysisResult> FullAnalysisAsync(Stream imageStream)
        {
            // Run image analysis first
            var analysisResult = await AnalyzeImageAsync(imageStream);

            // Then run OCR
            if (imageStream.CanSeek) imageStream.Position = 0;
            var ocrResult = await ExtractTextAsync(imageStream);

            // Merge OCR results into analysis result
            analysisResult.ExtractedText = ocrResult.ExtractedText;
            analysisResult.TextLines = ocrResult.TextLines;
            analysisResult.ApiCalDurationMs += ocrResult.ApiCalDurationMs;
            analysisResult.RetryCount += ocrResult.RetryCount;

            // Only mark as failed if both failed
            if (!ocrResult.Success && !analysisResult.Success)
            {
                analysisResult.Success = false;
                analysisResult.ErrorMessage =
                    $"Analysis: {analysisResult.ErrorMessage} | " +
                    $"OCR: {ocrResult.ErrorMessage}";
            }

            _logger.LogInformation(
                "🧠 Full analysis done | Description: \"{Desc}\" | " +
                "Tags: {Tags} | Text: {HasText} | " +
                "Total API time: {Time}ms",
                analysisResult.Description,
                analysisResult.Tags.Count,
                !string.IsNullOrEmpty(analysisResult.ExtractedText),
                analysisResult.ApiCalDurationMs);

            return analysisResult;
        }

        private static string ExtractOperationId(string operationLocation)
        {
            var parts = operationLocation.Split('/');
            return parts[1];
        }
    }
}