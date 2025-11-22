using FlashcardApp.Application.Interfaces;
using Microsoft.AspNetCore.Hosting;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace FlashcardApp.Infrastructure.Services;

public class FileProcessingService : IFileProcessingService
{
    private readonly IWebHostEnvironment _environment;
    private const string UploadFolder = "uploads";

    public FileProcessingService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName)
    {
        var uploadPath = Path.Combine(_environment.WebRootPath ?? _environment.ContentRootPath, UploadFolder);
        
        if (!Directory.Exists(uploadPath))
        {
            Directory.CreateDirectory(uploadPath);
        }

        var savedFileName = $"{Guid.NewGuid()}_{fileName}";
        var filePath = Path.Combine(uploadPath, savedFileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(stream);
        }

        return filePath;
    }

    public Task<string> ExtractTextFromPdfAsync(string filePath)
    {
        try
        {
            var text = new System.Text.StringBuilder();
            
            using (PdfDocument document = PdfDocument.Open(filePath))
            {
                foreach (Page page in document.GetPages())
                {
                    text.AppendLine(page.Text);
                }
            }

            return Task.FromResult(text.ToString());
        }
        catch (Exception ex)
        {
            throw new Exception($"Error extracting text from PDF: {ex.Message}", ex);
        }
    }

    public bool IsPdfFile(string fileName)
    {
        return Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public bool IsImageFile(string fileName)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".jpg" || extension == ".jpeg" || extension == ".png" || extension == ".gif";
    }
}

