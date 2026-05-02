namespace Bllueprint.Core.Application;

internal sealed class WithPipeline<T>(
    Func<T, bool> predicate,
    List<PipelineStep> steps,
    PipelineContext ctx) : IWithPipeline<T>
{
    private string _message = $"Validation failed for {typeof(T).Name}.";

    public IWithPipeline<T> WithMessage(string message)
    {
        _message = message;
        return this;
    }

    public IHandlerPipeline<TNext> Invoke<TNext>(Func<Task<TNext?>> entityTask)
    {
        string message = _message;

        steps.Add(async input =>
        {
            if (ctx.Failed)
            {
                return default;
            }

            var entity = (T?)input;
            if (entity is null)
            {
                return default;
            }

            if (!predicate(entity))
            {
                ctx.Fail(message);
                return default;
            }

            return await entityTask();
        });

        return new HandlerPipeline<TNext>(steps, ctx);
    }

    public IHandlerPipeline<T> Invoke(Func<Task> guardTask)
    {
        string message = _message;

        steps.Add(async input =>
        {
            if (ctx.Failed)
            {
                return default;
            }

            var entity = (T?)input;
            if (entity is null)
            {
                return default;
            }

            if (!predicate(entity))
            {
                ctx.Fail(message);
                return default;
            }

            try
            {
                await guardTask();
            }
            catch
            {
                ctx.Fail("An unexpected error occurred.");
                return default;
            }

            return (object?)entity;
        });

        return new HandlerPipeline<T>(steps, ctx);
    }

    public IHandlerPipeline<TNext> Invoke<TNext>(Func<T, Task<TNext?>> entityTask)
    {
        string message = _message;

        steps.Add(async input =>
        {
            if (ctx.Failed)
            {
                return default;
            }

            var entity = (T?)input;
            if (entity is null)
            {
                return default;
            }

            if (!predicate(entity))
            {
                ctx.Fail(message);
                return default;
            }

            return await entityTask(entity);
        });

        return new HandlerPipeline<TNext>(steps, ctx);
    }

    public IHandlerPipeline<T> Invoke(Action<T> transition)
    {
        steps.Add(input =>
        {
            if (ctx.Failed)
            {
                return Task.FromResult<object?>(input);
            }

            var entity = (T?)input;
            if (entity is null)
            {
                return Task.FromResult<object?>(null);
            }

            if (!predicate(entity))
            {
                ctx.Fail(_message);
                return Task.FromResult<object?>(null);
            }

            transition(entity);
            return Task.FromResult<object?>(entity);
        });

        return new HandlerPipeline<T>(steps, ctx);
    }

    public IHandlerPipeline<T> Save(Func<T, Task> persist)
    {
        string message = _message;

        steps.Add(async input =>
        {
            if (ctx.Failed)
            {
                return default;
            }

            var entity = (T?)input;
            if (entity is null)
            {
                return default;
            }

            if (!predicate(entity))
            {
                ctx.Fail(message);
                return default;
            }

            await persist(entity);
            return entity;
        });

        return new HandlerPipeline<T>(steps, ctx);
    }

    public Task<ICommandResult<T>> ToResultAsync()
        => new HandlerPipeline<T>(steps, ctx).ToResultAsync();
}
