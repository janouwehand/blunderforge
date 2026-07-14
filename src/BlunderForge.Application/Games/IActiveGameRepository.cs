using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Games;

public interface IActiveGameRepository
{
    Task<ChessGame?> GetActiveAsync(CancellationToken cancellationToken);

    Task SaveActiveAsync(ChessGame game, CancellationToken cancellationToken);

    Task ClearActiveAsync(CancellationToken cancellationToken);

    Task<bool> DeleteAsync(Guid gameId, CancellationToken cancellationToken);

    Task ReplaceActiveAsync(ChessGame game, CancellationToken cancellationToken);
}
