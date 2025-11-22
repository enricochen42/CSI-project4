using FlashcardApp.Application.DTOs;
using FlashcardApp.Application.Interfaces;
using FlashcardApp.Application.Entities;

namespace FlashcardApp.Application.Services;

public class FlashcardService : IFlashcardService
{
    private readonly IDeckRepository _deckRepository;
    private readonly IFlashcardRepository _flashcardRepository;

    public FlashcardService(IDeckRepository deckRepository, IFlashcardRepository flashcardRepository)
    {
        _deckRepository = deckRepository;
        _flashcardRepository = flashcardRepository;
    }

    public async Task<IEnumerable<Deck>> GetAllDecksAsync()
    {
        return await _deckRepository.GetAllWithFlashcardsAsync();
    }

    public async Task<Deck?> GetDeckByIdAsync(int id)
    {
        return await _deckRepository.GetByIdWithFlashcardsAsync(id);
    }

    public async Task<Deck> CreateDeckAsync(string name, string? description = null)
    {
        var deck = new Deck
        {
            Name = name,
            Description = description,
            CreatedDate = DateTime.UtcNow
        };

        return await _deckRepository.AddAsync(deck);
    }

    public async Task<IEnumerable<Flashcard>> GetFlashcardsByDeckIdAsync(int deckId)
    {
        return await _flashcardRepository.GetByDeckIdAsync(deckId);
    }

    public async Task<Flashcard> CreateFlashcardAsync(int deckId, string question, string answer)
    {
        var flashcard = new Flashcard
        {
            DeckId = deckId,
            Question = question,
            Answer = answer,
            CreatedDate = DateTime.UtcNow
        };

        return await _flashcardRepository.AddAsync(flashcard);
    }

    public async Task<IEnumerable<Flashcard>> CreateFlashcardsAsync(int deckId, IEnumerable<GeneratedFlashcardDto> flashcards)
    {
        var createdFlashcards = new List<Flashcard>();

        foreach (var flashcardDto in flashcards)
        {
            var flashcard = new Flashcard
            {
                DeckId = deckId,
                Question = flashcardDto.Question,
                Answer = flashcardDto.Answer,
                CreatedDate = DateTime.UtcNow
            };

            var createdFlashcard = await _flashcardRepository.AddAsync(flashcard);
            createdFlashcards.Add(createdFlashcard);
        }

        return createdFlashcards;
    }
}

