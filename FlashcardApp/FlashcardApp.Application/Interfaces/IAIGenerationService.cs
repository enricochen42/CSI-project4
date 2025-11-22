using FlashcardApp.Application.DTOs;

namespace FlashcardApp.Application.Interfaces;

public interface IAIGenerationService
{
    Task<List<GeneratedFlashcardDto>> GenerateFlashcardsAsync(string textContent);
}

