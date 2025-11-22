using FlashcardApp.Application.Entities;

namespace FlashcardApp.Application.Interfaces;

public interface IFlashcardRepository : IRepository<Flashcard>
{
    Task<IEnumerable<Flashcard>> GetByDeckIdAsync(int deckId);
}

