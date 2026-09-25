using System.Globalization;
using System.Text;
using AgenticLogAnalyzer.Application.Abstractions;
using AgenticLogAnalyzer.Detection;
using AgenticLogAnalyzer.Domain.Logs;

namespace AgenticLogAnalyzer.Agentic.Chat;

/// <summary>
/// Answers questions about stored logs. Facts are always computed deterministically; an optional LLM only
/// rephrases them, and the deterministic answer is kept as a fallback and for verification.
/// </summary>
public sealed class LogChatService(
    IEventRepository repository,
    RunDetectionsTool runDetectionsTool,
    ILlmProvider? llmProvider = null)
{
    private const int MaxListedEvents = 15;
    private const int MaxTimelineEvents = 25;
    private const int MaxPromptEvidence = 15;

    private const string SystemPrompt =
        "Tu es un assistant d'analyse de logs de sécurité. Tu reformules en français, de façon concise, les faits "
        + "calculés par un moteur déterministe. Règles : "
        + "1) Les faits et les détections fournis sont vérifiés : ne les contredis jamais et ne les minimise pas. "
        + "2) Si une détection est présente, mentionne-la explicitement comme un signal à traiter. "
        + "3) N'invente aucun événement, utilisateur, adresse IP, date ou chiffre absent des faits. "
        + "4) Sépare les faits observés des hypothèses, et présente toute hypothèse comme à vérifier. "
        + "5) Si les faits ne permettent pas de répondre, dis-le clairement. "
        + "6) Réponds en 3 à 6 phrases maximum, sans recopier les lignes de log une par une.";

    public async Task<ChatAnswer> AskAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var question = request.Question?.Trim() ?? string.Empty;
        if (question.Length == 0)
        {
            throw new ArgumentException("A question is required.", nameof(request));
        }

        var query = ChatQuestionAnalyzer.Analyze(question);
        var allEvents = await repository.SearchAsync(string.Empty, cancellationToken);
        var effectiveTerms = query.Terms
            .Where(term => allEvents.Any(item => Matches(item, term)))
            .ToArray();
        var scoped = effectiveTerms.Length == 0
            ? allEvents
            : allEvents.Where(item => effectiveTerms.Any(term => Matches(item, term))).ToArray();
        var filtered = query.ResultFilter is null
            ? scoped
            : scoped.Where(item => string.Equals(item.Result, query.ResultFilter, StringComparison.OrdinalIgnoreCase)).ToArray();

        var filters = effectiveTerms.Select(term => $"terme: {term}").ToList();
        if (query.ResultFilter is not null)
        {
            filters.Add($"résultat: {query.ResultFilter}");
        }

        var draft = allEvents.Count == 0
            ? new Draft("Aucun événement n'est encore stocké. Importez des logs avant de poser une question.", [], [])
            : query.Intent switch
            {
                ChatIntent.Security => BuildSecurity(scoped, query.ResultFilter),
                ChatIntent.TopSourceIps => BuildTopEntities(filtered, item => item.SourceIp, "adresse IP source"),
                ChatIntent.TopUsers => BuildTopEntities(filtered, item => item.User, "utilisateur"),
                ChatIntent.Timeline => BuildTimeline(filtered),
                ChatIntent.List => BuildList(filtered),
                _ => BuildSummary(filtered)
            };

        var answer = new ChatAnswer(
            question,
            query.Intent.ToString(),
            filters,
            query.Intent == ChatIntent.Security ? scoped.Count : filtered.Count,
            draft.Text,
            draft.Text,
            draft.Evidence,
            draft.Detections,
            LlmUsed: false,
            LlmError: null);

        return llmProvider is null || allEvents.Count == 0
            ? answer
            : await RephraseAsync(answer, cancellationToken);
    }

    private async Task<ChatAnswer> RephraseAsync(ChatAnswer answer, CancellationToken cancellationToken)
    {
        var prompt = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Question : {answer.Question}")
            .AppendLine()
            .AppendLine("Faits calculés par le moteur déterministe :")
            .AppendLine(answer.DeterministicAnswer)
            .AppendLine()
            .AppendLine("Événements de preuve (lignes brutes) :");
        foreach (var item in answer.Evidence.Take(MaxPromptEvidence))
        {
            prompt.AppendLine(item.RawContent);
        }

        try
        {
            var text = await llmProvider!.GenerateAsync(SystemPrompt, prompt.ToString(), cancellationToken);
            if (string.IsNullOrWhiteSpace(text))
            {
                return answer with { LlmError = "Le LLM a renvoyé une réponse vide." };
            }

            var facts = answer.DeterministicAnswer + "\n" + string.Join("\n", answer.Evidence.Select(item => item.RawContent));
            var issues = LlmAnswerVerifier.Verify(text, facts, answer.Detections.Count > 0);
            return issues.Count > 0
                ? answer with { LlmError = $"Réponse du LLM rejetée : {string.Join(" ; ", issues)}." }
                : answer with { Answer = text.Trim(), LlmUsed = true };
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException
            || (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return answer with { LlmError = exception.Message };
        }
    }

    private Draft BuildSecurity(IReadOnlyCollection<CanonicalEvent> events, string? resultFilter)
    {
        var detections = runDetectionsTool.Execute(events);
        var suspicious = events
            .Where(item => item.User is not null || item.SourceIp is not null)
            .Where(item => resultFilter is null
                ? IsResult(item, "failure") || IsResult(item, "warning")
                : IsResult(item, resultFilter))
            .OrderBy(item => item.Timestamp)
            .ToArray();

        var text = new StringBuilder();
        if (detections.Count == 0)
        {
            text.AppendLine("Aucune détection déterministe ne s'est déclenchée.");
        }
        else
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"{detections.Count} détection(s) déterministe(s) :");
            foreach (var finding in detections)
            {
                text.AppendLine(CultureInfo.InvariantCulture,
                    $"• [{finding.Severity}] {finding.RuleId} {finding.Title} : {finding.Description} ({FormatTime(finding.FirstSeen)} → {FormatTime(finding.LastSeen)})");
            }
        }

        text.AppendLine();
        if (suspicious.Length == 0)
        {
            text.AppendLine("Aucun échec ou avertissement lié à un utilisateur ou à une IP.");
        }
        else
        {
            text.AppendLine(CultureInfo.InvariantCulture,
                $"{suspicious.Length} événement(s) en échec ou avertissement impliquant un utilisateur ou une IP :");
            AppendEvents(text, suspicious.Take(MaxListedEvents));

            var ips = suspicious
                .Where(item => item.SourceIp is not null)
                .GroupBy(item => item.SourceIp!, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(group => group.Count())
                .Select(group => $"{group.Key} ({group.Count()})")
                .ToArray();
            if (ips.Length > 0)
            {
                text.AppendLine(CultureInfo.InvariantCulture, $"IP sources impliquées : {string.Join(", ", ips)}");
            }
        }

        var detectionIds = detections.SelectMany(item => item.EvidenceEventIds).ToHashSet();
        var evidence = suspicious
            .Concat(events.Where(item => detectionIds.Contains(item.Id)))
            .DistinctBy(item => item.Id)
            .OrderBy(item => item.Timestamp)
            .ToArray();
        return new Draft(text.ToString().TrimEnd(), evidence, detections);
    }

    private static Draft BuildTopEntities(
        IReadOnlyCollection<CanonicalEvent> events,
        Func<CanonicalEvent, string?> selector,
        string label)
    {
        var groups = events
            .Where(item => !string.IsNullOrWhiteSpace(selector(item)))
            .GroupBy(item => selector(item)!, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (groups.Length == 0)
        {
            return new Draft($"Aucun(e) {label} trouvé(e) parmi les {events.Count} événement(s) ciblé(s).", [], []);
        }

        var text = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"{groups.Length} {label}(s) distinct(e)s :");
        foreach (var group in groups.Take(MaxListedEvents))
        {
            var results = group
                .GroupBy(item => item.Result ?? "inconnu", StringComparer.OrdinalIgnoreCase)
                .Select(result => $"{result.Count()} {result.Key}");
            text.AppendLine(CultureInfo.InvariantCulture,
                $"• {group.Key} — {group.Count()} événement(s) ({string.Join(", ", results)}), dernière activité {FormatTime(group.Max(item => item.Timestamp))}");
        }

        var evidence = groups.SelectMany(group => group).OrderBy(item => item.Timestamp).ToArray();
        return new Draft(text.ToString().TrimEnd(), evidence, []);
    }

    private static Draft BuildTimeline(IReadOnlyCollection<CanonicalEvent> events)
    {
        if (events.Count == 0)
        {
            return new Draft("Aucun événement ne correspond à la question.", [], []);
        }

        var ordered = events.OrderBy(item => item.Timestamp).Take(MaxTimelineEvents).ToArray();
        var text = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Chronologie ({ordered.Length} sur {events.Count} événement(s)) :");
        AppendEvents(text, ordered);
        return new Draft(text.ToString().TrimEnd(), ordered, []);
    }

    private static Draft BuildList(IReadOnlyCollection<CanonicalEvent> events)
    {
        if (events.Count == 0)
        {
            return new Draft("Aucun événement ne correspond à la question.", [], []);
        }

        var latest = events.OrderByDescending(item => item.Timestamp).Take(MaxListedEvents).ToArray();
        var text = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"{events.Count} événement(s) correspondent.")
            .AppendLine(CultureInfo.InvariantCulture, $"Répartition par composant : {CountBy(events, item => item.Category)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Les {latest.Length} plus récents :");
        AppendEvents(text, latest);
        return new Draft(text.ToString().TrimEnd(), latest, []);
    }

    private Draft BuildSummary(IReadOnlyCollection<CanonicalEvent> events)
    {
        if (events.Count == 0)
        {
            return new Draft("Aucun événement ne correspond à la question.", [], []);
        }

        var detections = runDetectionsTool.Execute(events);
        var failures = events.Where(item => IsResult(item, "failure")).ToArray();
        var text = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture,
                $"{events.Count} événement(s) entre {FormatTime(events.Min(item => item.Timestamp))} et {FormatTime(events.Max(item => item.Timestamp))} (UTC).")
            .AppendLine(CultureInfo.InvariantCulture, $"Résultats : {CountBy(events, item => item.Result ?? "inconnu")}")
            .AppendLine(CultureInfo.InvariantCulture, $"Composants : {CountBy(events, item => item.Category)}");

        if (failures.Length > 0)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"Échecs par composant : {CountBy(failures, item => item.Category)}");
        }

        text.AppendLine(CultureInfo.InvariantCulture, $"Détections déterministes : {detections.Count}");
        foreach (var finding in detections)
        {
            text.AppendLine(CultureInfo.InvariantCulture, $"• [{finding.Severity}] {finding.Title} : {finding.Description}");
        }

        var latestFailures = failures.OrderByDescending(item => item.Timestamp).Take(5).ToArray();
        if (latestFailures.Length > 0)
        {
            text.AppendLine("Derniers échecs :");
            AppendEvents(text, latestFailures);
        }

        var detectionIds = detections.SelectMany(item => item.EvidenceEventIds).ToHashSet();
        var evidence = latestFailures
            .Concat(events.Where(item => detectionIds.Contains(item.Id)))
            .DistinctBy(item => item.Id)
            .OrderBy(item => item.Timestamp)
            .ToArray();
        return new Draft(text.ToString().TrimEnd(), evidence, detections);
    }

    private static void AppendEvents(StringBuilder text, IEnumerable<CanonicalEvent> events)
    {
        foreach (var item in events)
        {
            var entities = new[]
            {
                item.User is null ? null : $"user={item.User}",
                item.SourceIp is null ? null : $"ip={item.SourceIp}",
                item.Device is null ? null : $"device={item.Device}"
            }.Where(value => value is not null);
            var suffix = string.Join(" ", entities);
            text.AppendLine(CultureInfo.InvariantCulture,
                $"• {FormatTime(item.Timestamp)} [{item.Category}] {item.Action} → {item.Result ?? "inconnu"}{(suffix.Length > 0 ? " — " + suffix : string.Empty)}");
        }
    }

    private static string CountBy(IEnumerable<CanonicalEvent> events, Func<CanonicalEvent, string> selector) =>
        string.Join(", ", events
            .GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key} ({group.Count()})"));

    private static string FormatTime(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static bool IsResult(CanonicalEvent item, string result) =>
        string.Equals(item.Result, result, StringComparison.OrdinalIgnoreCase);

    private static bool Matches(CanonicalEvent item, string term) =>
        item.RawContent.Contains(term, StringComparison.OrdinalIgnoreCase)
        || item.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
        || item.Action.Contains(term, StringComparison.OrdinalIgnoreCase)
        || (item.User?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.Device?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false)
        || (item.SourceIp?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false);

    private sealed record Draft(
        string Text,
        IReadOnlyCollection<CanonicalEvent> Evidence,
        IReadOnlyCollection<DetectionFinding> Detections);
}
