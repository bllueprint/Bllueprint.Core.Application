using Bllueprint.Core.Domain;

namespace Bllueprint.Core.Application;

public interface IRepository<TEntity>
where TEntity : class, IAggregate
{
    Task AddAsync(TEntity entity);

    Task DeleteAsync(TEntity entity);

    Task<TEntity?> GetByIdAsync(Guid id);

    Task UpdateAsync(TEntity entity);
}
