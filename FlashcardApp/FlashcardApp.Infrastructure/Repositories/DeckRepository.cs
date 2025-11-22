using FlashcardApp.Application.Interfaces;
using FlashcardApp.Application.Entities;
using FlashcardApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlashcardApp.Infrastructure.Repositories;

public class DeckRepository : Repository<Deck>, IDeckRepository
{
    public DeckRepository(FlashcardDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Deck>> GetAllWithFlashcardsAsync()
    {
        // Using IQueryable allows EF Core to optimize the query
        // Include is executed in a single database query with JOIN
        return await Query()
            .Include(d => d.Flashcards)
            .OrderBy(d => d.Name)
            .ToListAsync();
    }

    public async Task<Deck?> GetByIdWithFlashcardsAsync(int id)
    {
        // Efficient single-query fetch with Include
        return await Query()
            .Include(d => d.Flashcards)
            .FirstOrDefaultAsync(d => d.Id == id);
    }
}

