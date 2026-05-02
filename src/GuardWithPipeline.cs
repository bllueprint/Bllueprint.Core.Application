namespace Bllueprint.Core.Application;

internal sealed class GuardWithPipeline(
    Func<Task<bool>> guardTask,
    Func<bool, bool> predicate,
    List<PipelineStep> steps,
    PipelineContext ctx) : IGuardWithPipeline
{
    private string _message = "Guard condition was not satisfied.";

    public IGuardWithPipeline WithMessage(string message)
    {
        _message = message;
        return this;
    }

    public IHandlerPipeline<TNext> Invoke<TNext>(Func<Task<TNext?>> entityTask)
    {
        string message = _message;

        steps.Add(async _ =>
        {
            if (ctx.Failed)
            {
                return default;
            }

            bool result;
            try
            {
                result = await guardTask();
            }
            catch
            {
                ctx.Fail(message);
                return default;
            }

            if (!predicate(result))
            {
                ctx.Fail(message);
                return default;
            }

            return await entityTask();
        });

        return new HandlerPipeline<TNext>(steps, ctx);
    }
}
