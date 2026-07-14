using BlunderForge.Domain.Games;

namespace BlunderForge.Application.Coaching;

public interface IMoveClassifier
{
    MoveClassification Classify(int centipawnLoss);
}
