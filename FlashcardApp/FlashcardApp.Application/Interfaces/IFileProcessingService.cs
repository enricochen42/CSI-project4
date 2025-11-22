namespace FlashcardApp.Application.Interfaces;

public interface IFileProcessingService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName);
    Task<string> ExtractTextFromPdfAsync(string filePath);
    bool IsPdfFile(string fileName);
    bool IsImageFile(string fileName);
}

