namespace BingCook.Api.Data;

public interface ISavedPropertyRepository
{
    Task<IReadOnlyList<Guid>> GetPropertyIdsByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);

    Task<bool> SaveAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken);

    Task RemoveAsync(
        Guid userId,
        Guid propertyId,
        CancellationToken cancellationToken);
}
