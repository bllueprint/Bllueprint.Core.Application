using System.Runtime.CompilerServices;
using Bllueprint.Core.Domain;

namespace Bllueprint.Core.Application;

internal sealed class HandlerPipeline<T>(
    List<PipelineStep> steps,
    PipelineContext ctx) : IHandlerPipeline<T>
{
    public IWithPipeline<T> WithCheck(Func<T, bool> predicate)
        => new WithPipeline<T>(predicate, steps, ctx);

    public IHandlerPipeline<TNext> Invoke<TNext>(Func<Task<TNext?>> entityTask, [CallerMemberName] string stepName = "")
    {
        steps.Add(async _ =>
        {
            if (ctx.Failed)
            {
                return default;
            }

            try
            {
                return await entityTask();
            }
            catch (Exception ex)
            {
                ctx.Fail(new FailureDetail
                {
                    Message = ex.Message,
                    TransitionName = stepName,
                    Kind = NotificationKind.Error
                });
                return default;
            }
        });

        return new HandlerPipeline<TNext>(steps, ctx);
    }

    public IHandlerPipeline<TNext> Invoke<TNext>(Func<T, Task<TNext?>> entityTask, [CallerMemberName] string stepName = "")
    {
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

            try
            {
                return await entityTask(entity);
            }
            catch (Exception ex)
            {
                ctx.Fail(new FailureDetail
                {
                    Message = ex.Message,
                    TransitionName = stepName,
                    Kind = NotificationKind.Error
                });
                return default;
            }
        });

        return new HandlerPipeline<TNext>(steps, ctx);
    }

    public IHandlerPipeline<T> Invoke(Action<T> transition)
    {
        steps.Add(input =>
        {
            if (ctx.Failed)
            {
                return Task.FromResult(input);
            }

            var entity = (T?)input;
            if (entity is null)
            {
                return Task.FromResult<object?>(null);
            }

            transition(entity);
            return Task.FromResult<object?>(entity);
        });

        return this;
    }

    public IHandlerPipeline<T> Invoke(Func<Task> guardTask, [CallerMemberName] string stepName = "")
    {
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

            try
            {
                await guardTask();
            }
            catch (Exception ex)
            {
                ctx.Fail(new FailureDetail
                {
                    Message = ex.Message,
                    TransitionName = stepName,
                    Kind = NotificationKind.Error
                });
                return default;
            }

            return (object?)entity;
        });

        return this;
    }

    public IHandlerPipeline<T> Save(Func<T, Task> persist, [CallerMemberName] string stepName = "")
    {
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

            try
            {
                await persist(entity);
            }
            catch (Exception ex)
            {
                ctx.Fail(new FailureDetail
                {
                    Message = ex.Message,
                    TransitionName = stepName,
                    Kind = NotificationKind.Error
                });
                return default;
            }

            return (object?)entity;
        });

        return this;
    }

    public async Task<ICommandResult<T>> ToResultAsync()
    {
        object? current = null;

        foreach (PipelineStep step in steps)
        {
            current = await step(current);

            if (ctx.Failed)
            {
                return CommandResult<T>.Failed(ctx.Notifications.ValidationErrors);
            }

            if (current is null)
            {
                return CommandResult<T>.Missing();
            }
        }

        return CommandResult<T>.Success((T)current!);
    }
}
