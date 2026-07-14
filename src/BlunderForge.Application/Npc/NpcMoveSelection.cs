using BlunderForge.Application.Engine;
using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Npc;

public sealed record NpcMoveSelection(
    UciMove Move,
    string Source,
    string EngineVersion,
    EngineSettingsSnapshot EngineSettings,
    IReadOnlyList<CandidateMove> Candidates);
