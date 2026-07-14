using BlunderForge.Domain.Games;
using Microsoft.Extensions.Options;

namespace BlunderForge.Application.Coaching;

public sealed class CentipawnMoveClassifier(IOptions<MoveClassificationOptions> options) : IMoveClassifier
{
    private readonly MoveClassificationOptions thresholds = options.Value;

    public MoveClassification Classify(int centipawnLoss)
    {
        var loss = Math.Max(0, centipawnLoss);
        if (loss == 0)
        {
            return MoveClassification.Best;
        }

        if (loss <= thresholds.ExcellentMaxCentipawnLoss)
        {
            return MoveClassification.Excellent;
        }

        if (loss <= thresholds.GoodMaxCentipawnLoss)
        {
            return MoveClassification.Good;
        }

        if (loss <= thresholds.InaccuracyMaxCentipawnLoss)
        {
            return MoveClassification.Inaccuracy;
        }

        if (loss <= thresholds.MistakeMaxCentipawnLoss)
        {
            return MoveClassification.Mistake;
        }

        return MoveClassification.Blunder;
    }
}
