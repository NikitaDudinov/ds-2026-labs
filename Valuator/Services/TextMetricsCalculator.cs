namespace Valuator.Services;

public interface ITextMetricsCalculator
{
    double CalculateRank(string text);
}

public class TextMetricsCalculator : ITextMetricsCalculator
{
    public double CalculateRank(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0.0;

        double nonLetters = text.Count(c => !char.IsLetter(c));
        return nonLetters / text.Length;
    }
}