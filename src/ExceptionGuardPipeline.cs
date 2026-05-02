namespace Bllueprint.Core.Application;

internal delegate Task<object?> PipelineStep(object? input);

internal sealed class ExceptionGuardPipeline(
    Func<Task> guardTask,
    List<PipelineStep> steps,
    PipelineContext ctx) : IExceptionGuardPipeline
{
    private string _message = "An unexpected error occurred.";

    public IExceptionGuardPipeline WithMessage(string message)
    {
        _message = message;
        return this;
    }

    public IHandlerPipeline<TNext> Invoke<TNext>(Func<Task<TNext?>> entityTask)
    {
        string message = _message;

        steps.Add(async _ =>
        {
            if (ctx.Failed || ctx.Notifications.HasErrors)
            {
                return CommandResult<TNext>.Failed(ctx.Notifications.ValidationErrors);
            }

            try
            {
                await guardTask();
            }
            catch
            {
                ctx.Fail(message);
                return default;
            }

            return await entityTask();
        });

        return new HandlerPipeline<TNext>(steps, ctx);
    }
}
