using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Nutrition.Application.Common;

namespace Nutrition.Infrastructure.Persistence;

public class EfRepository<T> : IRepository<T> where T : class
{
    private readonly DietTrackerDbContext _context;
    private readonly DbSet<T> _dbSet;

    public EfRepository(DietTrackerDbContext context)
    {
        _context = context;
        _dbSet = context.Set<T>();
    }

    public async Task<T?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, ct);
    }

    public async Task<List<T>> GetAllAsync(CancellationToken ct = default)
    {
        return await _dbSet.ToListAsync(ct);
    }

    public async Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default)
    {
        return await _dbSet.Where(predicate).ToListAsync(ct);
    }

    public async Task AddAsync(T entity, CancellationToken ct = default)
    {
        await _dbSet.AddAsync(entity, ct);
    }

    public Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        var entry = _context.Entry(entity);
        if (entry.State == EntityState.Detached)
        {
            _dbSet.Update(entity);
        }
        return Task.CompletedTask;
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity != null)
        {
            _dbSet.Remove(entity);
        }
    }
}

public class EfUnitOfWork : IUnitOfWork
{
    private readonly DietTrackerDbContext _context;

    public EfUnitOfWork(DietTrackerDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }
}
