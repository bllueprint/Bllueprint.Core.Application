namespace Bllueprint.Core.Application;

public interface IGuardPipeline
{
    IGuardWithPipeline With(Func<bool, bool> predicate);
}
