using System.Text.Json;
using BlunderForge.Application.Coaching;

namespace BlunderForge.Application.Ai;

public sealed class AiResponseValidationException(string message) : Exception(message);

public static class AiResponseValidator
{
    public const int MaxHintLength = 200;
    public const int MaxExplanationLength = 1000;

    public static AiCoachExplanation ValidateMoveHelp(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new AiResponseValidationException("AI move help must be a JSON object.");
            var names = root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            if (!names.SetEquals(["hint", "explanation"]))
                throw new AiResponseValidationException("AI move help contains unexpected or missing fields.");
            return new AiCoachExplanation(
                RequiredText(root, "hint", MaxHintLength),
                RequiredText(root, "explanation", MaxExplanationLength));
        }
        catch (JsonException exception)
        {
            throw new AiResponseValidationException($"Malformed JSON: {exception.Message}");
        }
    }

    public static GameReviewResponse ValidateGameReview(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new AiResponseValidationException("AI game review must be a JSON object.");
            var names = root.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
            if (!names.SetEquals(["summary", "learningMoments", "focus"]))
                throw new AiResponseValidationException("AI game review contains unexpected or missing fields.");
            return new GameReviewResponse(
                RequiredText(root, "summary", 100),
                RequiredStringArray(root, "learningMoments", 3, 300),
                RequiredText(root, "focus", 300));
        }
        catch (JsonException exception)
        {
            throw new AiResponseValidationException($"Malformed JSON: {exception.Message}");
        }
    }

    private static string RequiredText(JsonElement root, string name, int maximum) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) && value.GetString()!.Length <= maximum
            ? value.GetString()!
            : throw new AiResponseValidationException($"{name} is required and must be at most {maximum} characters.");

    private static string[] RequiredStringArray(JsonElement root, string name, int maximumCount, int maximumLength)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw new AiResponseValidationException($"{name} is required.");
        var result = value.EnumerateArray().Select(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()) && item.GetString()!.Length <= maximumLength
            ? item.GetString()!
            : throw new AiResponseValidationException($"{name} contains invalid text.")).ToArray();
        return result.Length <= maximumCount ? result : throw new AiResponseValidationException($"{name} contains too many values.");
    }
}
