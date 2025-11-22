using FlashcardApp.Application.Entities;

namespace FlashcardApp.Application.Interfaces;

public interface IDeckRepository : IRepository<Deck>
{
    Task<IEnumerable<Deck>> GetAllWithFlashcardsAsync();
    Task<Deck?> GetByIdWithFlashcardsAsync(int id);
}

