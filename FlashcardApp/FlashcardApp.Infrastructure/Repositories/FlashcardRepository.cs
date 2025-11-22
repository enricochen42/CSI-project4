using FlashcardApp.Application.Interfaces;
using FlashcardApp.Application.Entities;
using FlashcardApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace FlashcardApp.Infrastructure.Repositories;

public class FlashcardRepository : Repository<Flashcard>, IFlashcardRepository
{
    public FlashcardRepository(FlashcardDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Flashcard>> GetByDeckIdAsync(int deckId)
    {
        // Using IQueryable allows EF Core to optimize the WHERE and ORDER BY clauses
        // Query is executed efficiently at the database level
        return await Query()
            .Where(f => f.DeckId == deckId)
            .OrderBy(f => f.Id)
            .ToListAsync();
    }
}

