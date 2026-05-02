using Bllueprint.Core.Domain;

namespace Bllueprint.Core.Application;

internal sealed class PipelineContext(INotificationContext notifications)
{
    public INotificationContext Notifications { get; } = notifications;

    public bool Failed => Notifications.HasErrors;

    public void Fail(FailureDetail failureDetail)
    {
        Notifications.Add(new Notification
        {
            TransitionName = failureDetail.TransitionName,
            Message = failureDetail.Message,
            Kind = failureDetail.Kind ?? NotificationKind.Error
        });
    }

    public void Fail(string message)
    {
        Notifications.Add(new Notification
        {
            TransitionName = "Pipeline",
            Message = message,
            Kind = NotificationKind.Error
        });
    }
}
