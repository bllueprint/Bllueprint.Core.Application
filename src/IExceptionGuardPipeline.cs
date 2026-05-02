namespace Bllueprint.Core.Application;

public interface IExceptionGuardPipeline
{
    IExceptionGuardPipeline WithMessage(string message);

    IHandlerPipeline<TNext> Invoke<TNext>(Func<Task<TNext?>> entityTask);
}
