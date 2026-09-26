using System.Text.Json;
using System.Text.RegularExpressions;

namespace EmployeeRequests.Api.Domain;

public sealed record ClassificationRules(
    double MinConfidence,
    int MinScore,
    Dictionary<string, Dictionary<string, int>> Departments,
    List<string> PriorityBoosters)
{
    public static ClassificationRules Load(string path) =>
        JsonSerializer.Deserialize<ClassificationRules>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
        ?? throw new InvalidOperationException($"Could not read classification rules from {path}");
}

/// <param name="ClassifiedBy">"rules" when the keyword rules were confident, otherwise "fallback" (or "ai" once triaged).</param>
public sealed record Classification(
    string Department,
    double Confidence,
    string ClassifiedBy,
    IReadOnlyDictionary<string, int> Scores);

/// <summary>
/// Keyword/weight classifier. Each department scores the sum of weights of its keywords found in the text;
/// confidence is the top score's share of all points. Rules decide only when confidence and score clear the thresholds.
/// </summary>
public sealed class Classifier
{
    private readonly ClassificationRules _rules;
    private readonly Dictionary<string, List<(Regex Pattern, int Weight)>> _patterns;

    public Classifier(ClassificationRules rules)
    {
        _rules = rules;
        _patterns = rules.Departments.ToDictionary(
            d => d.Key,
            d => d.Value.Select(k => (KeywordPattern(k.Key), k.Value)).ToList());
    }

    public Classification Classify(string subject, string description)
    {
        var text = Normalize($"{subject} {description}");

        var scores = _patterns.ToDictionary(
            d => d.Key,
            d => d.Value.Where(k => k.Pattern.IsMatch(text)).Sum(k => k.Weight));

        var total = scores.Values.Sum();
        if (total == 0)
            return new Classification(Departments.Other, 0, "fallback", scores);

        var (top, topScore) = scores.MaxBy(s => s.Value);
        var confidence = Math.Round((double)topScore / total, 2);

        return confidence >= _rules.MinConfidence && topScore >= _rules.MinScore
            ? new Classification(top, confidence, "rules", scores)
            : new Classification(Departments.Other, confidence, "fallback", scores);
    }

    /// <summary>The employee's urgency is the floor; booster phrases can raise it to High, never lower it.</summary>
    public Priority ResolvePriority(Priority requested, string subject, string description)
    {
        var text = Normalize($"{subject} {description}");
        var boosted = _rules.PriorityBoosters.Any(b => text.Contains(Normalize(b)));
        return boosted && requested < Priority.High ? Priority.High : requested;
    }

    private static string Normalize(string s) =>
        Regex.Replace(s.ToLowerInvariant().Replace('’', '\''), @"\s+", " ");

    // Whole-word match with an optional plural ("payslips", "leaves"), so "ac" doesn't match "access".
    private static Regex KeywordPattern(string keyword) =>
        new($@"\b{Regex.Escape(Normalize(keyword))}(s|es)?\b", RegexOptions.Compiled | RegexOptions.CultureInvariant);
}
