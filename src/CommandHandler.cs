using Bllueprint.Core.Domain;
using MediatR;

namespace Bllueprint.Core.Application;

public abstract class CommandHandler<TCommand, T>(INotificationContext notifications)
    : IRequestHandler<TCommand, ICommandResult<T>>
    where TCommand : IRequest<ICommandResult<T>>
{
    private readonly PipelineContext _ctx = new(notifications);

    public abstract Task<ICommandResult<T>> Handle(
        TCommand request, CancellationToken cancellationToken);

    protected IExceptionGuardPipeline Invoke(Func<Task> guardTask)
        => new ExceptionGuardPipeline(guardTask, [], _ctx);

    protected IGuardPipeline Invoke(Func<Task<bool>> guardTask)
        => new GuardPipeline(guardTask, [], _ctx);

    protected IHandlerPipeline<T> Invoke(Func<Task<T?>> entityTask)
    {
        var steps = new List<PipelineStep>
        {
            async _ => await entityTask()
        };
        return new HandlerPipeline<T>(steps, _ctx);
    }
}
