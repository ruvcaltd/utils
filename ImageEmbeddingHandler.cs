using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.AI;

public class ImageEmbeddingHandler : IPipelineStepHandler
{
    private readonly ITextEmbeddingGenerator _textEmbeddingGenerator;
    private readonly IImageEmbeddingService _imageEmbeddingService;
    private readonly string _stepName = "generate_image_embeddings";

    public string StepName => _stepName;

    public ImageEmbeddingHandler(
        IImageEmbeddingService imageEmbeddingService,
        ITextEmbeddingGenerator textEmbeddingGenerator)
    {
        _imageEmbeddingService = imageEmbeddingService;
        _textEmbeddingGenerator = textEmbeddingGenerator;
    }

    public async Task<(bool success, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, 
        CancellationToken cancellationToken = default)
    {
        foreach (var file in pipeline.Files)
        {
            // Only process files tagged as images
            if (!file.Tags.TryGetValue("__file_type", out var fileType) || fileType != "image")
                continue;

            // Generate image description using vision model (optional but recommended)
            var imageDescription = await GenerateImageDescription(file, pipeline, cancellationToken);
            
            // Generate embedding from the image
            var imageEmbedding = await _imageEmbeddingService.GenerateEmbeddingAsync(
                await GetImageBytes(file, pipeline, cancellationToken),
                cancellationToken);

            // Generate text embedding for the description
            Embedding textEmbedding = null;
            if (!string.IsNullOrEmpty(imageDescription))
            {
                textEmbedding = await _textEmbeddingGenerator.GenerateEmbeddingAsync(
                    imageDescription, 
                    cancellationToken);
            }

            // Create a new partition for the image
            var imagePartition = new DataPipeline.Partition
            {
                PartitionNumber = pipeline.Partitions.Count,
                SectionNumber = 0,
                Content = imageDescription ?? $"Image: {file.Name}",
                LastUpdate = DateTimeOffset.UtcNow,
                Tags = new TagCollection(file.Tags)
                {
                    { "__content_type", "image" },
                    { "__image_file", file.Name },
                    { "__has_description", (!string.IsNullOrEmpty(imageDescription)).ToString() }
                }
            };

            // Add image embedding
            imagePartition.Embeddings.Add(new DataPipeline.EmbeddingVector
            {
                GeneratorName = _imageEmbeddingService.GetType().Name,
                Vector = imageEmbedding
            });

            // Add text embedding if available (for hybrid search)
            if (textEmbedding != null)
            {
                imagePartition.Embeddings.Add(new DataPipeline.EmbeddingVector
                {
                    GeneratorName = _textEmbeddingGenerator.GetType().Name,
                    Vector = textEmbedding.Data.ToArray()
                });
            }

            pipeline.Partitions.Add(imagePartition);
        }

        return (true, pipeline);
    }

    private async Task<string> GenerateImageDescription(
        DataPipeline.FileDetails file,
        DataPipeline pipeline,
        CancellationToken ct)
    {
        var imageBytes = await GetImageBytes(file, pipeline, ct);
        return await _imageEmbeddingService.GenerateDescriptionAsync(imageBytes, ct);
    }

    private async Task<byte[]> GetImageBytes(
        DataPipeline.FileDetails file,
        DataPipeline pipeline,
        CancellationToken ct)
    {
        var stream = await pipeline.Storage.ReadFileAsync(
            pipeline.Index, 
            pipeline.DocumentId, 
            file.Name, 
            ct);
        
        using var memoryStream = new MemoryStream();
        await stream.CopyToAsync(memoryStream, ct);
        return memoryStream.ToArray();
    }
}

// Interface for image embedding service
public interface IImageEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken ct);
    Task<string> GenerateDescriptionAsync(byte[] imageBytes, CancellationToken ct);
}

// Example implementation using Azure OpenAI GPT-4 Vision
public class AzureOpenAIImageService : IImageEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiKey;
    private readonly string _deploymentName;
    
    public AzureOpenAIImageService(string endpoint, string apiKey, string deploymentName)
    {
        _endpoint = endpoint;
        _apiKey = apiKey;
        _deploymentName = deploymentName;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("api-key", apiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken ct)
    {
        // For now, use CLIP or similar service
        // Azure OpenAI doesn't directly provide image embeddings
        // You might want to:
        // 1. Use Azure Computer Vision for image embeddings
        // 2. Generate description first, then embed the text
        // 3. Use a dedicated CLIP service
        
        // Option: Generate description and use text embedding
        var description = await GenerateDescriptionAsync(imageBytes, ct);
        
        // This is a workaround - ideally use a proper image embedding model
        throw new NotImplementedException(
            "Use Azure Computer Vision or CLIP for proper image embeddings");
    }

    public async Task<string> GenerateDescriptionAsync(byte[] imageBytes, CancellationToken ct)
    {
        var base64Image = Convert.ToBase64String(imageBytes);
        
        var requestBody = new
        {
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = "Describe this image in detail, focusing on key elements, text, diagrams, and any important visual information." },
                        new
                        {
                            type = "image_url",
                            image_url = new
                            {
                                url = $"data:image/jpeg;base64,{base64Image}"
                            }
                        }
                    }
                }
            },
            max_tokens = 500
        };

        var response = await _httpClient.PostAsJsonAsync(
            $"{_endpoint}/openai/deployments/{_deploymentName}/chat/completions?api-version=2024-02-15-preview",
            requestBody,
            ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<dynamic>(cancellationToken: ct);
        
        return result?.choices[0]?.message?.content?.ToString() ?? string.Empty;
    }
}

// Alternative: Azure Computer Vision for proper image embeddings
public class AzureComputerVisionImageService : IImageEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly string _endpoint;
    private readonly string _apiKey;
    
    public AzureComputerVisionImageService(string endpoint, string apiKey)
    {
        _endpoint = endpoint;
        _apiKey = apiKey;
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(byte[] imageBytes, CancellationToken ct)
    {
        // Use Azure Computer Vision 4.0 vectorize API
        using var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        
        var response = await _httpClient.PostAsync(
            $"{_endpoint}/computervision/retrieval:vectorizeImage?api-version=2023-02-01-preview&modelVersion=latest",
            content,
            ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<VectorizeImageResponse>(cancellationToken: ct);
        
        return result?.vector ?? Array.Empty<float>();
    }

    public async Task<string> GenerateDescriptionAsync(byte[] imageBytes, CancellationToken ct)
    {
        // Use Azure Computer Vision analyze API
        using var content = new ByteArrayContent(imageBytes);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        
        var response = await _httpClient.PostAsync(
            $"{_endpoint}/computervision/imageanalysis:analyze?api-version=2023-02-01-preview&features=caption,denseCaptions,tags&language=en",
            content,
            ct);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ImageAnalysisResponse>(cancellationToken: ct);
        
        return result?.captionResult?.text ?? string.Empty;
    }

    private class VectorizeImageResponse
    {
        public float[] vector { get; set; }
        public string modelVersion { get; set; }
    }

    private class ImageAnalysisResponse
    {
        public CaptionResult captionResult { get; set; }
        public class CaptionResult
        {
            public string text { get; set; }
            public float confidence { get; set; }
        }
    }
}
