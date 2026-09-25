using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace AgenticLogAnalyzer.Agentic.Chat;

/// <summary>Keyword-based (French and English) question interpretation. No LLM is involved.</summary>
public static partial class ChatQuestionAnalyzer
{
    private static readonly HashSet<string> SecurityWords =
    [
        "securite", "security", "attaque", "attaques", "attack", "attacks", "intrusion", "brute", "bruteforce",
        "suspect", "suspects", "suspecte", "suspicious", "menace", "menaces", "threat", "threats",
        "detection", "detections", "piratage", "hack"
    ];

    private static readonly HashSet<string> FailureWords =
    [
        "erreur", "erreurs", "error", "errors", "echec", "echecs", "echoue", "echouee", "echouees", "echoues",
        "fail", "failed", "failure", "failures", "probleme", "problemes", "problem", "problems",
        "crash", "crashs", "panne", "pannes", "incident", "incidents", "bug", "bugs"
    ];

    private static readonly HashSet<string> WarningWords =
    [
        "warning", "warnings", "warn", "avertissement", "avertissements", "alerte", "alertes", "alert", "alerts"
    ];

    private static readonly HashSet<string> SuccessWords =
    [
        "succes", "success", "successful", "reussi", "reussie", "reussis", "reussies", "ok"
    ];

    private static readonly HashSet<string> IpWords = ["ip", "ips", "adresse", "adresses", "address", "addresses"];

    private static readonly HashSet<string> UserWords =
    [
        "utilisateur", "utilisateurs", "user", "users", "compte", "comptes", "account", "accounts", "qui", "who"
    ];

    private static readonly HashSet<string> TimelineWords =
    [
        "chronologie", "timeline", "quand", "when", "sequence", "deroulement", "ordre", "chronologique", "historique"
    ];

    private static readonly HashSet<string> SummaryWords =
    [
        "resume", "resumer", "summary", "summarize", "synthese", "overview", "global", "globale", "bilan", "etat"
    ];

    private static readonly HashSet<string> StopWords =
    [
        // French
        "le", "la", "les", "un", "une", "des", "de", "du", "et", "ou", "au", "aux", "en", "dans", "sur", "pour",
        "par", "avec", "sans", "ce", "ces", "cet", "cette", "que", "quoi", "quel", "quelle", "quels", "quelles",
        "est", "sont", "ont", "il", "ils", "elle", "elles", "on", "se", "sa", "son", "ses", "mes", "mon", "ma",
        "moi", "me", "je", "tu", "nous", "vous", "leur", "leurs", "pas", "ne", "plus", "tout", "tous", "toutes",
        "ete", "etre", "avoir", "fait", "fais", "faire", "passe", "donne", "donner", "montre", "montrer", "affiche",
        "afficher", "liste", "lister", "voir", "vois", "peux", "peut", "pourrais", "dis", "dire", "explique",
        "logs", "log", "evenement", "evenements", "entree", "entrees", "ligne", "lignes", "combien", "nombre",
        "total", "y", "a", "l", "d", "s", "t", "c", "j", "n", "qu", "il", "ai", "as", "avons", "avez",
        "eu", "derniers", "dernieres", "dernier", "derniere", "recents", "recentes", "principaux", "principales",
        "trouve", "trouves", "ya", "stp", "svp", "merci", "bonjour", "salut",
        // English
        "the", "an", "of", "to", "in", "on", "for", "with", "and", "or", "is", "are", "was", "were", "what",
        "which", "show", "me", "list", "give", "tell", "about", "any", "there", "have", "has", "did", "do", "does",
        "happened", "happen", "events", "event", "how", "many", "much", "count", "all", "my", "please", "last",
        "latest", "recent", "top", "main", "found", "i", "it", "be", "been"
    ];

    public static ChatQuery Analyze(string question)
    {
        ArgumentNullException.ThrowIfNull(question);

        var tokens = TokenRegex().Split(RemoveDiacritics(question.ToLowerInvariant()))
            .Select(token => token.Trim('.', ':', '-', '/'))
            .Where(token => token.Length > 0)
            .ToArray();

        var resultFilter = tokens.Any(FailureWords.Contains) ? "failure"
            : tokens.Any(WarningWords.Contains) ? "warning"
            : tokens.Any(SuccessWords.Contains) ? "success"
            : null;

        var terms = tokens
            .Where(token => !IsNoise(token))
            .Where(token => token.Length >= 3 || token.Any(char.IsDigit))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var intent = tokens.Any(SecurityWords.Contains) ? ChatIntent.Security
            : tokens.Any(IpWords.Contains) ? ChatIntent.TopSourceIps
            : tokens.Any(UserWords.Contains) ? ChatIntent.TopUsers
            : tokens.Any(TimelineWords.Contains) ? ChatIntent.Timeline
            : tokens.Any(SummaryWords.Contains) ? ChatIntent.Summary
            : terms.Length > 0 || resultFilter is not null ? ChatIntent.List
            : ChatIntent.Summary;

        return new ChatQuery(intent, resultFilter, terms);
    }

    // Hyphenated tokens such as "est-il" or "fais-moi" are noise; identifiers such as "worker-02" are kept.
    private static bool IsNoise(string token) =>
        token.Split('-', StringSplitOptions.RemoveEmptyEntries)
            .All(part => StopWords.Contains(part) || IsIntentWord(part));

    private static bool IsIntentWord(string token) =>
        SecurityWords.Contains(token) || FailureWords.Contains(token) || WarningWords.Contains(token)
        || SuccessWords.Contains(token) || IpWords.Contains(token) || UserWords.Contains(token)
        || TimelineWords.Contains(token) || SummaryWords.Contains(token);

    private static string RemoveDiacritics(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex(@"[^\p{L}\p{N}._:@/-]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
