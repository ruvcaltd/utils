using Microsoft.KernelMemory;
using Microsoft.KernelMemory.Pipeline;
using Microsoft.KernelMemory.DataFormats;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

public class ImageExtractionHandler : IPipelineStepHandler
{
    private readonly string _stepName = "extract_images";
    
    public string StepName => _stepName;

    public async Task<(bool success, DataPipeline updatedPipeline)> InvokeAsync(
        DataPipeline pipeline, 
        CancellationToken cancellationToken = default)
    {
        // Iterate through all files in the pipeline
        foreach (var file in pipeline.Files)
        {
            // Skip if already processed or not a supported format
            if (!IsSupportedFormat(file.Name))
                continue;

            // Extract images based on file type
            if (file.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                await ExtractImagesFromPdf(file, pipeline, cancellationToken);
            }
            else if (IsImageFile(file.Name))
            {
                // Handle standalone images
                await ProcessStandaloneImage(file, pipeline, cancellationToken);
            }
            else if (file.Name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                await ExtractImagesFromDocx(file, pipeline, cancellationToken);
            }
        }

        return (true, pipeline);
    }

    private async Task ExtractImagesFromPdf(DataPipeline.FileDetails file, 
        DataPipeline pipeline, CancellationToken ct)
    {
        // Use a library like PdfPig or iTextSharp
        using var pdfStream = file.GetStream();
        using var document = UglyToad.PdfPig.PdfDocument.Open(pdfStream);
        
        int imageIndex = 0;
        foreach (var page in document.GetPages())
        {
            foreach (var image in page.GetImages())
            {
                var imageBytes = image.RawBytes.ToArray();
                var imageName = $"{Path.GetFileNameWithoutExtension(file.Name)}_img_{imageIndex}.png";
                
                // Save image as a new file in the pipeline
                pipeline.Files.Add(new DataPipeline.GeneratedFileDetails
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = imageName,
                    ContentType = "image/png",
                    Size = imageBytes.Length,
                    Tags = new()
                    {
                        { "__file_type", "image" },
                        { "__source_file", file.Name },
                        { "__page_number", page.Number.ToString() },
                        { "__is_embedded_image", "true" }
                    }
                });
                
                await pipeline.Storage.WriteFileAsync(
                    pipeline.Index, 
                    pipeline.DocumentId, 
                    imageName, 
                    new MemoryStream(imageBytes), 
                    ct);
                
                imageIndex++;
            }
        }
    }

    private async Task ExtractImagesFromDocx(DataPipeline.FileDetails file, 
        DataPipeline pipeline, CancellationToken ct)
    {
        // Use DocumentFormat.OpenXml
        using var docxStream = file.GetStream();
        using var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(docxStream, false);
        
        var imageParts = document.MainDocumentPart?.ImageParts;
        if (imageParts == null) return;
        
        int imageIndex = 0;
        foreach (var imagePart in imageParts)
        {
            using var imageStream = imagePart.GetStream();
            var imageBytes = new byte[imageStream.Length];
            await imageStream.ReadAsync(imageBytes, 0, imageBytes.Length, ct);
            
            var imageName = $"{Path.GetFileNameWithoutExtension(file.Name)}_img_{imageIndex}.png";
            
            pipeline.Files.Add(new DataPipeline.GeneratedFileDetails
            {
                Id = Guid.NewGuid().ToString(),
                Name = imageName,
                ContentType = imagePart.ContentType,
                Size = imageBytes.Length,
                Tags = new()
                {
                    { "__file_type", "image" },
                    { "__source_file", file.Name },
                    { "__is_embedded_image", "true" }
                }
            });
            
            await pipeline.Storage.WriteFileAsync(
                pipeline.Index, 
                pipeline.DocumentId, 
                imageName, 
                new MemoryStream(imageBytes), 
                ct);
            
            imageIndex++;
        }
    }

    private async Task ProcessStandaloneImage(DataPipeline.FileDetails file, 
        DataPipeline pipeline, CancellationToken ct)
    {
        // Tag standalone images for embedding generation
        file.Tags["__file_type"] = "image";
        file.Tags["__is_standalone_image"] = "true";
    }

    private bool IsSupportedFormat(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext is ".pdf" or ".docx" or ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp";
    }

    private bool IsImageFile(string filename)
    {
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp";
    }
}
