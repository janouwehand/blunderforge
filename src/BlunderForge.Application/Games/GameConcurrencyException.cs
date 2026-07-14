namespace BlunderForge.Application.Games;

public sealed class GameConcurrencyException(string message, Exception? innerException = null)
    : InvalidOperationException(message, innerException);
