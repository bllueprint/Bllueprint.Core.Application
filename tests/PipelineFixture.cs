namespace Bllueprint.Core.Application.Tests;

public static class PipelineFixture
{
    internal static PipelineContext NewContext() =>
        new(new InMemoryNotificationContext());

    internal static (HandlerPipeline<T> Pipeline, PipelineContext Ctx) StartPipeline<T>(Func<Task<T?>> seed)
    {
        PipelineContext ctx = NewContext();
        var steps = new List<PipelineStep> { async _ => await seed() };
        return (new HandlerPipeline<T>(steps, ctx), ctx);
    }
}
