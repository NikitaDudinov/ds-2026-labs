namespace Valuator.Services;

public record TextEvaluationResult(string Id, string Text, double Rank, double Similarity);

public interface ITextValuationService
{
    Task<string> EvaluateAsync(string text);
    Task<TextEvaluationResult?> GetResultAsync(string id);
}

public class TextValuationService : ITextValuationService
{
    private readonly ITextMetricsCalculator _calculator;
    private readonly IEvaluationStorage _storage;

    public TextValuationService(ITextMetricsCalculator calculator, IEvaluationStorage storage)
    {
        _calculator = calculator;
        _storage = storage;
    }

    public async Task<string> EvaluateAsync(string text)
    {
        string id = Guid.NewGuid().ToString();

        double rank = _calculator.CalculateRank(text);
        bool isUnique = await _storage.IsTextUniqueAsync(text);
        double similarity = isUnique ? 0.0 : 1.0;

        await _storage.SaveTextAsync(id, text);
        await _storage.SaveRankAsync(id, rank);
        await _storage.SaveSimilarityAsync(id, similarity);

        if (isUnique)
        {
            await _storage.AddToUniqueSetAsync(text);
        }

        return id;
    }

    public async Task<TextEvaluationResult?> GetResultAsync(string id)
    {
        var text = await _storage.GetTextAsync(id);
        var rank = await _storage.GetRankAsync(id);
        var similarity = await _storage.GetSimilarityAsync(id);

        if (text == null || rank == null || similarity == null) return null;

        return new TextEvaluationResult(id, text, rank.Value, similarity.Value);
    }
}