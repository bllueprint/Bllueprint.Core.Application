namespace Bllueprint.Core.Application;

public interface IUnitOfWork
{
    Task CommitAsync();
}
