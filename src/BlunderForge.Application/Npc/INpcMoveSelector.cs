using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Npc;

public interface INpcMoveSelector
{
    Task<NpcMoveSelection> SelectMoveAsync(GameState game, CancellationToken cancellationToken);

    bool ShouldResign(GameState game, int evaluationCentipawnsFromWhite);
}
