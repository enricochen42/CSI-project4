using FlashcardApp.Application.DTOs;
using FlashcardApp.Application.Entities;

namespace FlashcardApp.Application.Interfaces;

public interface IFlashcardService
{
    Task<IEnumerable<Deck>> GetAllDecksAsync();
    Task<Deck?> GetDeckByIdAsync(int id);
    Task<Deck> CreateDeckAsync(string name, string? description = null);
    Task<IEnumerable<Flashcard>> GetFlashcardsByDeckIdAsync(int deckId);
    Task<Flashcard> CreateFlashcardAsync(int deckId, string question, string answer);
    Task<IEnumerable<Flashcard>> CreateFlashcardsAsync(int deckId, IEnumerable<GeneratedFlashcardDto> flashcards);
}

