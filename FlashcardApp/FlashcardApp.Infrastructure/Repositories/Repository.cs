using FlashcardApp.Application.Interfaces;
using FlashcardApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace FlashcardApp.Infrastructure.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly FlashcardDbContext _context;
    protected readonly DbSet<T> _dbSet;

    public Repository(FlashcardDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    // Protected method to expose IQueryable for use within repository implementations
    // This allows subclasses to build complex queries using EF Core's query capabilities
    protected IQueryable<T> Query()
    {
        return _dbSet.AsQueryable();
    }

    public virtual async Task<T?> GetByIdAsync(int id)
    {
        return await _dbSet.FindAsync(id);
    }

    public virtual async Task<IEnumerable<T>> GetAllAsync()
    {
        // Using IQueryable internally - query is built and executed efficiently
        return await Query().ToListAsync();
    }

    public virtual async Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate)
    {
        // IQueryable allows EF Core to translate predicate to SQL
        return await Query().Where(predicate).ToListAsync();
    }

    public virtual async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
    {
        return await Query().FirstOrDefaultAsync(predicate);
    }

    public virtual async Task<bool> AnyAsync(Expression<Func<T, bool>> predicate)
    {
        // Efficient - checks existence without loading entities
        return await Query().AnyAsync(predicate);
    }

    public virtual async Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null)
    {
        var query = Query();
        if (predicate != null)
        {
            query = query.Where(predicate);
        }
        // Efficient - counts in database without loading entities
        return await query.CountAsync();
    }

    public virtual async Task<T> AddAsync(T entity)
    {
        await _dbSet.AddAsync(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task<T> UpdateAsync(T entity)
    {
        _dbSet.Update(entity);
        await _context.SaveChangesAsync();
        return entity;
    }

    public virtual async Task DeleteAsync(T entity)
    {
        _dbSet.Remove(entity);
        await _context.SaveChangesAsync();
    }

    public virtual async Task<bool> ExistsAsync(int id)
    {
        // Try to get entity by ID - if it exists, we'll find it
        // This could be optimized further with reflection, but for simplicity:
        var entity = await GetByIdAsync(id);
        return entity != null;
    }
}

