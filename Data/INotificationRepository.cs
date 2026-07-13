using BingCook.Api.Models;

namespace BingCook.Api.Data;

public interface INotificationRepository
{
    Task<IReadOnlyList<UserNotification>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task CreateAsync(
        CreateNotificationCommand command,
        CancellationToken cancellationToken);

    Task<bool> MarkReadAsync(
        Guid notificationId,
        Guid userId,
        CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
