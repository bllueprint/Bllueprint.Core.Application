namespace Bllueprint.Core.Application;

public static class PipelineGuard
{
    public static Func<T, bool> NotNull<T>()
    where T : class => x => x is not null;

    public static Func<T, bool> NotEmpty<T, TKey>(Func<T, TKey> selector)
        where TKey : struct => x => !selector(x).Equals(default(TKey));
}
