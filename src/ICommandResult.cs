using Bllueprint.Core.Domain;

namespace Bllueprint.Core.Application;

public interface ICommandResult<T>
{
    T? Entity { get; }

    IReadOnlyList<Notification> Errors { get; }

    bool HasErrors { get; }

    bool NotFound { get; }
}
