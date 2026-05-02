using Bllueprint.Core.Domain;

namespace Bllueprint.Core.Application;

internal readonly record struct FailureDetail
{
    public required string Message { get; init; }

    public required string TransitionName { get; init; }

    public NotificationKind? Kind { get; init; }
}
