using Bllueprint.Core.Domain;

namespace Bllueprint.Core.Application.Tests;

internal sealed class InMemoryNotificationContext : INotificationContext
{
    private readonly List<Notification> _notifications = [];

    public bool HasErrors => _notifications.Exists(n => n.Kind == NotificationKind.Error);

    public IReadOnlyList<Notification> ValidationErrors =>
        _notifications.Where(n => n.Kind == NotificationKind.Error).ToList();

    public IReadOnlyList<Notification> All => throw new InvalidOperationException();

    public IEnumerable<Notification> Warnings => throw new InvalidOperationException();

    public bool IsEmpty => throw new InvalidOperationException();

    IEnumerable<Notification> INotificationContext.ValidationErrors => ValidationErrors;

    public void Add(Notification notification) => _notifications.Add(notification);

    public void Add(string transitionName, string message, NotificationKind kind = NotificationKind.Error) => throw new InvalidOperationException();
}
