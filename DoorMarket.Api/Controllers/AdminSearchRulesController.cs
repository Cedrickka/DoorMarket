using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DoorMarket.Api.Services;
using System.Text;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/search-rules")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminSearchRulesController : ControllerBase
{
    private const string TargetTypeProduct = "Product";
    private const string TargetTypeShop = "Shop";
    private const string TargetTypeCategory = "Category";

    private readonly DoorMarketDbContext _db;
    private readonly SearchRuleSuggestionService _suggestions;

    public AdminSearchRulesController(DoorMarketDbContext db, SearchRuleSuggestionService suggestions)
    {
        _db = db;
        _suggestions = suggestions;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SearchRuleDto>>> GetRules(
        [FromQuery] string? q = null,
        [FromQuery] bool? active = null,
        CancellationToken ct = default)
    {
        var normalizedQuery = NormalizeFilterQuery(q);
        var query = _db.SearchQueryRules.AsNoTracking().AsQueryable();

        if (active.HasValue)
        {
            query = query.Where(x => x.IsActive == active.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(x =>
                x.TriggerQuery.Contains(normalizedQuery) ||
                (x.CanonicalQuery != null && x.CanonicalQuery.Contains(normalizedQuery)) ||
                (x.Note != null && x.Note.Contains(normalizedQuery)));
        }

        var rows = await query
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.TriggerQuery)
            .Select(x => ToDto(x))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [HttpGet("effectiveness")]
    public async Task<ActionResult<IReadOnlyList<SearchRuleEffectivenessDto>>> GetEffectiveness(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? source,
        [FromQuery] string? q = null,
        [FromQuery] bool? active = null,
        [FromQuery] int take = 100,
        [FromQuery] bool problemOnly = false,
        [FromQuery] int minQueries = 3,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 300);
        minQueries = Math.Clamp(minQueries, 1, 5000);
        var rows = await ComputeEffectivenessAsync(from, to, source, q, active, take, problemOnly, minQueries, ct);
        return Ok(rows);
    }

    [HttpGet("effectiveness/export.csv")]
    public async Task<IActionResult> ExportEffectivenessCsv(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? source,
        [FromQuery] string? q = null,
        [FromQuery] bool? active = null,
        [FromQuery] int take = 300,
        [FromQuery] bool problemOnly = false,
        [FromQuery] int minQueries = 3,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 2000);
        minQueries = Math.Clamp(minQueries, 1, 5000);

        var rows = await ComputeEffectivenessAsync(from, to, source, q, active, take, problemOnly, minQueries, ct);
        var sb = new StringBuilder();
        sb.AppendLine("Id,TriggerQuery,CanonicalQuery,TargetType,TargetId,IsActive,Performance,QueryCount,NoResultCount,ClickCount,NoResultRatePct,CtrPct,LastSeenAtUtc");
        foreach (var row in rows)
        {
            sb.AppendLine(string.Join(",",
                Csv(row.Id),
                Csv(row.TriggerQuery),
                Csv(row.CanonicalQuery),
                Csv(row.TargetType),
                Csv(row.TargetId),
                Csv(row.IsActive),
                Csv(row.Performance),
                Csv(row.QueryCount),
                Csv(row.NoResultCount),
                Csv(row.ClickCount),
                Csv(row.NoResultRatePct),
                Csv(row.CtrPct),
                Csv(row.LastSeenAtUtc)));
        }

        var fileName = $"search-rules-effectiveness-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv; charset=utf-8", fileName);
    }

    [HttpPost("effectiveness/deactivate-problematic")]
    public async Task<ActionResult<DeactivateProblematicRulesResponseDto>> DeactivateProblematicRules(
        [FromBody] DeactivateProblematicRulesRequestDto? request,
        CancellationToken ct = default)
    {
        request ??= new DeactivateProblematicRulesRequestDto(
            From: null,
            To: null,
            Source: null,
            Q: null,
            Active: true,
            Take: 200,
            MinQueries: 5,
            MinNoResultRatePct: 40m,
            MaxCtrPct: 8m,
            NoteSuffix: "Auto-disabled: low search effectiveness");

        var take = Math.Clamp(request.Take, 1, 300);
        var minQueries = Math.Clamp(request.MinQueries, 1, 5000);
        var minNoResultRatePct = Math.Clamp(request.MinNoResultRatePct, 0m, 100m);
        var maxCtrPct = Math.Clamp(request.MaxCtrPct, 0m, 100m);
        var dryRun = request.DryRun;
        var noteSuffix = NormalizeWords(request.NoteSuffix, 120);

        var rows = await ComputeEffectivenessAsync(
            request.From,
            request.To,
            request.Source,
            request.Q,
            request.Active,
            take,
            problemOnly: false,
            minQueries,
            ct);

        var matched = rows
            .Where(x => x.QueryCount >= minQueries)
            .Where(x => x.NoResultRatePct >= minNoResultRatePct)
            .Where(x => x.CtrPct <= maxCtrPct)
            .ToList();

        if (matched.Count == 0)
        {
            return Ok(new DeactivateProblematicRulesResponseDto(
                EvaluatedRules: rows.Count,
                MatchedProblematicRules: 0,
                DeactivatedRules: 0,
                WouldDeactivateRules: 0,
                SkippedAlreadyInactive: 0,
                ThresholdMinQueries: minQueries,
                ThresholdMinNoResultRatePct: minNoResultRatePct,
                ThresholdMaxCtrPct: maxCtrPct,
                DryRun: dryRun,
                TriggerQueries: Array.Empty<string>()));
        }

        var matchedIds = matched.Select(x => x.Id).ToHashSet();
        var candidates = await _db.SearchQueryRules
            .Where(x => matchedIds.Contains(x.Id))
            .ToListAsync(ct);

        var deactivated = 0;
        var wouldDeactivate = 0;
        var skippedAlreadyInactive = 0;
        var deactivatedTriggers = new List<string>(candidates.Count);
        foreach (var row in candidates)
        {
            if (!row.IsActive)
            {
                skippedAlreadyInactive++;
                continue;
            }

            if (!dryRun)
            {
                row.IsActive = false;
                row.UpdatedAtUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(noteSuffix))
                {
                    var mergedNote = string.IsNullOrWhiteSpace(row.Note)
                        ? noteSuffix
                        : $"{row.Note} | {noteSuffix}";
                    row.Note = NormalizeNote(mergedNote);
                }

                deactivated++;
            }

            wouldDeactivate++;
            deactivatedTriggers.Add(row.TriggerQuery);
        }

        if (!dryRun && deactivated > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new DeactivateProblematicRulesResponseDto(
            EvaluatedRules: rows.Count,
            MatchedProblematicRules: matched.Count,
            DeactivatedRules: deactivated,
            WouldDeactivateRules: wouldDeactivate,
            SkippedAlreadyInactive: skippedAlreadyInactive,
            ThresholdMinQueries: minQueries,
            ThresholdMinNoResultRatePct: minNoResultRatePct,
            ThresholdMaxCtrPct: maxCtrPct,
            DryRun: dryRun,
            TriggerQueries: deactivatedTriggers
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList()));
    }

    [HttpPost("effectiveness/reactivate-recovered")]
    public async Task<ActionResult<ReactivateRecoveredRulesResponseDto>> ReactivateRecoveredRules(
        [FromBody] ReactivateRecoveredRulesRequestDto? request,
        CancellationToken ct = default)
    {
        request ??= new ReactivateRecoveredRulesRequestDto(
            From: null,
            To: null,
            Source: null,
            Q: null,
            Active: false,
            Take: 200,
            MinQueries: 5,
            MaxNoResultRatePct: 15m,
            MinCtrPct: 12m,
            NoteSuffix: "Auto-reactivated: search effectiveness recovered");

        var take = Math.Clamp(request.Take, 1, 300);
        var minQueries = Math.Clamp(request.MinQueries, 1, 5000);
        var maxNoResultRatePct = Math.Clamp(request.MaxNoResultRatePct, 0m, 100m);
        var minCtrPct = Math.Clamp(request.MinCtrPct, 0m, 100m);
        var dryRun = request.DryRun;
        var noteSuffix = NormalizeWords(request.NoteSuffix, 120);

        var rows = await ComputeEffectivenessAsync(
            request.From,
            request.To,
            request.Source,
            request.Q,
            request.Active,
            take,
            problemOnly: false,
            minQueries,
            ct);

        var matched = rows
            .Where(x => x.QueryCount >= minQueries)
            .Where(x => x.NoResultRatePct <= maxNoResultRatePct)
            .Where(x => x.CtrPct >= minCtrPct)
            .ToList();

        if (matched.Count == 0)
        {
            return Ok(new ReactivateRecoveredRulesResponseDto(
                EvaluatedRules: rows.Count,
                MatchedRecoveredRules: 0,
                ReactivatedRules: 0,
                WouldReactivateRules: 0,
                SkippedAlreadyActive: 0,
                ThresholdMinQueries: minQueries,
                ThresholdMaxNoResultRatePct: maxNoResultRatePct,
                ThresholdMinCtrPct: minCtrPct,
                DryRun: dryRun,
                TriggerQueries: Array.Empty<string>()));
        }

        var matchedIds = matched.Select(x => x.Id).ToHashSet();
        var candidates = await _db.SearchQueryRules
            .Where(x => matchedIds.Contains(x.Id))
            .ToListAsync(ct);

        var reactivated = 0;
        var wouldReactivate = 0;
        var skippedAlreadyActive = 0;
        var reactivatedTriggers = new List<string>(candidates.Count);
        foreach (var row in candidates)
        {
            if (row.IsActive)
            {
                skippedAlreadyActive++;
                continue;
            }

            if (!dryRun)
            {
                row.IsActive = true;
                row.UpdatedAtUtc = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(noteSuffix))
                {
                    var mergedNote = string.IsNullOrWhiteSpace(row.Note)
                        ? noteSuffix
                        : $"{row.Note} | {noteSuffix}";
                    row.Note = NormalizeNote(mergedNote);
                }

                reactivated++;
            }

            wouldReactivate++;
            reactivatedTriggers.Add(row.TriggerQuery);
        }

        if (!dryRun && reactivated > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new ReactivateRecoveredRulesResponseDto(
            EvaluatedRules: rows.Count,
            MatchedRecoveredRules: matched.Count,
            ReactivatedRules: reactivated,
            WouldReactivateRules: wouldReactivate,
            SkippedAlreadyActive: skippedAlreadyActive,
            ThresholdMinQueries: minQueries,
            ThresholdMaxNoResultRatePct: maxNoResultRatePct,
            ThresholdMinCtrPct: minCtrPct,
            DryRun: dryRun,
            TriggerQueries: reactivatedTriggers
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Take(50)
                .ToList()));
    }

    [HttpGet("suggestions")]
    public async Task<ActionResult<IReadOnlyList<SearchRuleSuggestionDto>>> GetSuggestions(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? source,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var rows = await _suggestions.GetSuggestionsAsync(from, to, source, take, ct);
        return Ok(rows.Select(x => new SearchRuleSuggestionDto(
            x.TriggerQuery,
            x.SuggestedCanonicalQuery,
            x.TargetType,
            x.TargetId,
            x.TargetLabel,
            x.Score,
            x.Confidence,
            x.Reason,
            x.NoResultCount,
            x.LastSeenAtUtc)).ToList());
    }

    [HttpPost("suggestions/apply")]
    public async Task<ActionResult<ApplySuggestionsResponseDto>> ApplySuggestions(
        [FromBody] ApplySuggestionsRequestDto? request,
        CancellationToken ct = default)
    {
        request ??= new ApplySuggestionsRequestDto(
            From: null,
            To: null,
            Source: null,
            Take: 30,
            MinScore: 82,
            MinNoResultCount: 2,
            ConfidenceFilter: "MediumOrHigh",
            IsActive: true,
            NotePrefix: null);

        var take = Math.Clamp(request.Take, 1, 100);
        var minScore = Math.Clamp(request.MinScore, 0, 100);
        var minNoResultCount = Math.Clamp(request.MinNoResultCount, 1, 1000);

        var suggestions = await _suggestions.GetSuggestionsAsync(request.From, request.To, request.Source, take, ct);
        var eligible = suggestions
            .Where(x => x.Score >= minScore)
            .Where(x => x.NoResultCount >= minNoResultCount)
            .Where(x => IsConfidenceAccepted(x.Confidence, request.ConfidenceFilter))
            .ToList();

        var existingTriggers = await _db.SearchQueryRules.AsNoTracking()
            .Select(x => x.TriggerQuery)
            .ToListAsync(ct);
        var existingSet = existingTriggers
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var skippedExisting = 0;
        var skippedInvalid = 0;
        var skippedMissingTarget = 0;

        foreach (var suggestion in eligible)
        {
            var trigger = NormalizeTriggerQuery(suggestion.TriggerQuery);
            if (string.IsNullOrWhiteSpace(trigger))
            {
                skippedInvalid++;
                continue;
            }

            if (existingSet.Contains(trigger))
            {
                skippedExisting++;
                continue;
            }

            var targetType = NormalizeTargetType(suggestion.TargetType);
            var targetId = suggestion.TargetId;
            if (targetType is null || targetId == Guid.Empty)
            {
                skippedInvalid++;
                continue;
            }

            if (!await TargetExistsAsync(targetType, targetId, ct))
            {
                skippedMissingTarget++;
                continue;
            }

            var canonical = NormalizeCanonicalQuery(suggestion.SuggestedCanonicalQuery);
            var notePrefix = NormalizeWords(request.NotePrefix, 120) ?? "Auto-suggestion";
            var note = NormalizeNote($"{notePrefix} ({suggestion.Confidence}, score={suggestion.Score}, hits={suggestion.NoResultCount})");

            _db.SearchQueryRules.Add(new Domain.Entities.SearchQueryRule
            {
                TriggerQuery = trigger,
                CanonicalQuery = canonical,
                TargetType = targetType,
                TargetId = targetId,
                IsActive = request.IsActive,
                Note = note
            });

            existingSet.Add(trigger);
            created++;
        }

        if (created > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return Ok(new ApplySuggestionsResponseDto(
            TotalSuggestions: suggestions.Count,
            EligibleSuggestions: eligible.Count,
            Created: created,
            SkippedExisting: skippedExisting,
            SkippedInvalid: skippedInvalid,
            SkippedMissingTarget: skippedMissingTarget));
    }

    [HttpPost]
    public async Task<ActionResult<SearchRuleDto>> CreateRule(
        [FromBody] UpsertSearchRuleRequest request,
        CancellationToken ct = default)
    {
        var validate = await ValidateRequestAsync(request, null, ct);
        if (!validate.Ok)
        {
            return BadRequest(validate.Error);
        }

        var existing = await _db.SearchQueryRules
            .AsNoTracking()
            .AnyAsync(x => x.TriggerQuery == validate.TriggerQuery, ct);
        if (existing)
        {
            return Conflict("Une regle existe deja pour cette requete.");
        }

        var row = new Domain.Entities.SearchQueryRule
        {
            TriggerQuery = validate.TriggerQuery!,
            CanonicalQuery = validate.CanonicalQuery,
            TargetType = validate.TargetType,
            TargetId = validate.TargetId,
            IsActive = validate.IsActive,
            Note = validate.Note
        };

        _db.SearchQueryRules.Add(row);
        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(row));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SearchRuleDto>> UpdateRule(
        Guid id,
        [FromBody] UpsertSearchRuleRequest request,
        CancellationToken ct = default)
    {
        var row = await _db.SearchQueryRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            return NotFound("Regle introuvable.");
        }

        var validate = await ValidateRequestAsync(request, id, ct);
        if (!validate.Ok)
        {
            return BadRequest(validate.Error);
        }

        row.TriggerQuery = validate.TriggerQuery!;
        row.CanonicalQuery = validate.CanonicalQuery;
        row.TargetType = validate.TargetType;
        row.TargetId = validate.TargetId;
        row.IsActive = validate.IsActive;
        row.Note = validate.Note;
        row.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(ToDto(row));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRule(Guid id, CancellationToken ct = default)
    {
        var row = await _db.SearchQueryRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null)
        {
            return NotFound("Regle introuvable.");
        }

        _db.SearchQueryRules.Remove(row);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<RequestValidationResult> ValidateRequestAsync(
        UpsertSearchRuleRequest request,
        Guid? currentRuleId,
        CancellationToken ct)
    {
        var triggerQuery = NormalizeTriggerQuery(request.TriggerQuery);
        if (string.IsNullOrWhiteSpace(triggerQuery))
        {
            return RequestValidationResult.Fail("TriggerQuery requis (2 caracteres minimum).");
        }

        var canonicalQuery = NormalizeCanonicalQuery(request.CanonicalQuery);
        var targetType = NormalizeTargetType(request.TargetType);
        var note = NormalizeNote(request.Note);

        if ((request.TargetId.HasValue && !request.TargetId.Value.Equals(Guid.Empty)) && targetType is null)
        {
            return RequestValidationResult.Fail("TargetType invalide.");
        }

        var targetId = request.TargetId.HasValue && request.TargetId.Value != Guid.Empty
            ? request.TargetId.Value
            : (Guid?)null;

        if (targetType is null)
        {
            targetId = null;
        }

        if (canonicalQuery is null && !targetId.HasValue)
        {
            return RequestValidationResult.Fail("Definir CanonicalQuery ou un mapping cible (TargetType + TargetId).");
        }

        if (targetType is not null && !targetId.HasValue)
        {
            return RequestValidationResult.Fail("TargetId requis quand TargetType est renseigne.");
        }

        if (targetType is not null && targetId.HasValue)
        {
            var targetExists = await TargetExistsAsync(targetType, targetId.Value, ct);
            if (!targetExists)
            {
                return RequestValidationResult.Fail("La cible referencee est introuvable.");
            }
        }

        var duplicated = await _db.SearchQueryRules.AsNoTracking()
            .AnyAsync(x => x.TriggerQuery == triggerQuery && (!currentRuleId.HasValue || x.Id != currentRuleId.Value), ct);
        if (duplicated)
        {
            return RequestValidationResult.Fail("Une regle existe deja pour cette requete.");
        }

        return RequestValidationResult.Success(
            triggerQuery,
            canonicalQuery,
            targetType,
            targetId,
            request.IsActive,
            note);
    }

    private async Task<bool> TargetExistsAsync(string targetType, Guid targetId, CancellationToken ct)
    {
        if (targetType == TargetTypeProduct)
        {
            return await _db.Products.AsNoTracking().AnyAsync(x => x.Id == targetId, ct);
        }

        if (targetType == TargetTypeShop)
        {
            return await _db.Shops.AsNoTracking().AnyAsync(x => x.Id == targetId, ct);
        }

        if (targetType == TargetTypeCategory)
        {
            return await _db.Categories.AsNoTracking().AnyAsync(x => x.Id == targetId, ct);
        }

        return false;
    }

    private static SearchRuleDto ToDto(Domain.Entities.SearchQueryRule row)
        => new(
            row.Id,
            row.TriggerQuery,
            row.CanonicalQuery,
            row.TargetType,
            row.TargetId,
            row.IsActive,
            row.Note,
            row.CreatedAtUtc,
            row.UpdatedAtUtc);

    private static string? NormalizeFilterQuery(string? value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= 80 ? normalized : normalized[..80];
    }

    private static string? NormalizeTriggerQuery(string? value)
    {
        var normalized = NormalizeWords(value, 120);
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length < 2)
        {
            return null;
        }

        return normalized.ToLowerInvariant();
    }

    private static string? NormalizeCanonicalQuery(string? value)
    {
        var normalized = NormalizeWords(value, 120);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.ToLowerInvariant();
    }

    private static string? NormalizeTargetType(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (string.Equals(normalized, TargetTypeProduct, StringComparison.OrdinalIgnoreCase))
        {
            return TargetTypeProduct;
        }

        if (string.Equals(normalized, TargetTypeShop, StringComparison.OrdinalIgnoreCase))
        {
            return TargetTypeShop;
        }

        if (string.Equals(normalized, TargetTypeCategory, StringComparison.OrdinalIgnoreCase))
        {
            return TargetTypeCategory;
        }

        return null;
    }

    private static string? NormalizeNote(string? value)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        return normalized.Length <= 500 ? normalized : normalized[..500];
    }

    private static string? NormalizeWords(string? value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        var words = normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var compact = string.Join(' ', words);
        return compact.Length <= maxLength ? compact : compact[..maxLength];
    }

    private static bool IsConfidenceAccepted(string confidence, string? filter)
    {
        var normalizedConfidence = (confidence ?? string.Empty).Trim();
        var normalizedFilter = (filter ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedFilter) || normalizedFilter == "any")
        {
            return true;
        }

        if (normalizedFilter is "high")
        {
            return string.Equals(normalizedConfidence, "High", StringComparison.OrdinalIgnoreCase);
        }

        if (normalizedFilter is "mediumorhigh" or "medium+" or "medium")
        {
            return string.Equals(normalizedConfidence, "High", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(normalizedConfidence, "Medium", StringComparison.OrdinalIgnoreCase);
        }

        return true;
    }

    private static bool IsProblematic(SearchRuleEffectivenessDto dto, int minQueries)
    {
        if (dto.QueryCount < minQueries)
        {
            return false;
        }

        return dto.NoResultRatePct >= 30m || dto.CtrPct < 8m;
    }

    private static string EvaluatePerformance(int queryCount, decimal noResultRatePct, decimal ctrPct)
    {
        if (queryCount <= 0)
        {
            return "NoData";
        }

        if (noResultRatePct >= 60m)
        {
            return "Critical";
        }

        if (noResultRatePct >= 30m)
        {
            return "NeedsAttention";
        }

        if (noResultRatePct <= 10m && ctrPct >= 15m)
        {
            return "Good";
        }

        return "Watch";
    }

    private static string? NormalizeSourceFilter(string? source)
    {
        var normalized = (source ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (string.Equals(normalized, "web", StringComparison.OrdinalIgnoreCase))
        {
            return "Web";
        }

        if (string.Equals(normalized, "mobile", StringComparison.OrdinalIgnoreCase))
        {
            return "Mobile";
        }

        if (string.Equals(normalized, "admin", StringComparison.OrdinalIgnoreCase))
        {
            return "Admin";
        }

        return normalized.Length <= 30 ? normalized : normalized[..30];
    }

    private static string Csv(object? value)
    {
        if (value is null)
        {
            return "\"\"";
        }

        var s = value switch
        {
            DateTime dt => dt.ToString("O"),
            DateTimeOffset dto => dto.ToString("O"),
            _ => value.ToString() ?? string.Empty
        };

        s = s.Replace("\"", "\"\"", StringComparison.Ordinal);
        return $"\"{s}\"";
    }

    private async Task<List<SearchRuleEffectivenessDto>> ComputeEffectivenessAsync(
        DateTime? from,
        DateTime? to,
        string? source,
        string? q,
        bool? active,
        int take,
        bool problemOnly,
        int minQueries,
        CancellationToken ct)
    {
        var normalizedQ = NormalizeFilterQuery(q);
        var rulesQuery = _db.SearchQueryRules.AsNoTracking().AsQueryable();
        if (active.HasValue)
        {
            rulesQuery = rulesQuery.Where(x => x.IsActive == active.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedQ))
        {
            rulesQuery = rulesQuery.Where(x =>
                x.TriggerQuery.Contains(normalizedQ) ||
                (x.CanonicalQuery != null && x.CanonicalQuery.Contains(normalizedQ)) ||
                (x.Note != null && x.Note.Contains(normalizedQ)));
        }

        var ruleRows = await rulesQuery
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.TriggerQuery)
            .Take(take)
            .Select(x => new
            {
                x.Id,
                x.TriggerQuery,
                x.CanonicalQuery,
                x.TargetType,
                x.TargetId,
                x.IsActive
            })
            .ToListAsync(ct);

        if (ruleRows.Count == 0)
        {
            return new List<SearchRuleEffectivenessDto>();
        }

        var triggers = ruleRows
            .Select(x => x.TriggerQuery)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var normalizedSource = NormalizeSourceFilter(source);
        var eventBase = _db.SearchAnalyticsEvents.AsNoTracking()
            .Where(x => x.NormalizedQuery != null && triggers.Contains(x.NormalizedQuery!));

        if (from.HasValue)
        {
            eventBase = eventBase.Where(x => x.OccurredAtUtc >= from.Value.ToUniversalTime());
        }

        if (to.HasValue)
        {
            var toUtc = to.Value.ToUniversalTime();
            if (toUtc.TimeOfDay == TimeSpan.Zero)
            {
                toUtc = toUtc.AddDays(1);
            }

            eventBase = eventBase.Where(x => x.OccurredAtUtc < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(normalizedSource))
        {
            eventBase = eventBase.Where(x => x.Source == normalizedSource);
        }

        var queryStats = await eventBase
            .Where(x => x.EventType == "Query")
            .GroupBy(x => x.NormalizedQuery!)
            .Select(g => new
            {
                TriggerQuery = g.Key,
                QueryCount = g.Count(),
                NoResultCount = g.Count(e => (e.ResultsCount ?? 0) <= 0),
                LastSeenAtUtc = g.Max(e => (DateTime?)e.OccurredAtUtc)
            })
            .ToListAsync(ct);

        var clickStats = await eventBase
            .Where(x => x.EventType == "Click")
            .GroupBy(x => x.NormalizedQuery!)
            .Select(g => new
            {
                TriggerQuery = g.Key,
                ClickCount = g.Count()
            })
            .ToListAsync(ct);

        var queryStatsByTrigger = queryStats.ToDictionary(x => x.TriggerQuery, StringComparer.OrdinalIgnoreCase);
        var clickStatsByTrigger = clickStats.ToDictionary(x => x.TriggerQuery, StringComparer.OrdinalIgnoreCase);

        var rows = new List<SearchRuleEffectivenessDto>(ruleRows.Count);
        foreach (var rule in ruleRows)
        {
            queryStatsByTrigger.TryGetValue(rule.TriggerQuery, out var queryStat);
            clickStatsByTrigger.TryGetValue(rule.TriggerQuery, out var clickStat);

            var queryCount = queryStat?.QueryCount ?? 0;
            var noResultCount = queryStat?.NoResultCount ?? 0;
            var clickCount = clickStat?.ClickCount ?? 0;

            var noResultRatePct = queryCount <= 0
                ? 0m
                : Math.Round((decimal)noResultCount * 100m / queryCount, 2, MidpointRounding.AwayFromZero);

            var ctrPct = queryCount <= 0
                ? 0m
                : Math.Round((decimal)clickCount * 100m / queryCount, 2, MidpointRounding.AwayFromZero);

            var performance = EvaluatePerformance(queryCount, noResultRatePct, ctrPct);
            var dto = new SearchRuleEffectivenessDto(
                rule.Id,
                rule.TriggerQuery,
                rule.CanonicalQuery,
                rule.TargetType,
                rule.TargetId,
                rule.IsActive,
                queryCount,
                noResultCount,
                clickCount,
                noResultRatePct,
                ctrPct,
                queryStat?.LastSeenAtUtc,
                performance);

            if (!problemOnly || IsProblematic(dto, minQueries))
            {
                rows.Add(dto);
            }
        }

        return rows
            .OrderByDescending(x => x.QueryCount)
            .ThenByDescending(x => x.NoResultRatePct)
            .ThenBy(x => x.TriggerQuery)
            .ToList();
    }

    public sealed record UpsertSearchRuleRequest(
        string TriggerQuery,
        string? CanonicalQuery,
        string? TargetType,
        Guid? TargetId,
        bool IsActive = true,
        string? Note = null);

    public sealed record SearchRuleDto(
        Guid Id,
        string TriggerQuery,
        string? CanonicalQuery,
        string? TargetType,
        Guid? TargetId,
        bool IsActive,
        string? Note,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc);

    public sealed record SearchRuleSuggestionDto(
        string TriggerQuery,
        string? SuggestedCanonicalQuery,
        string TargetType,
        Guid TargetId,
        string TargetLabel,
        int Score,
        string Confidence,
        string Reason,
        int NoResultCount,
        DateTime? LastSeenAtUtc);

    public sealed record SearchRuleEffectivenessDto(
        Guid Id,
        string TriggerQuery,
        string? CanonicalQuery,
        string? TargetType,
        Guid? TargetId,
        bool IsActive,
        int QueryCount,
        int NoResultCount,
        int ClickCount,
        decimal NoResultRatePct,
        decimal CtrPct,
        DateTime? LastSeenAtUtc,
        string Performance);

    public sealed record ApplySuggestionsRequestDto(
        DateTime? From,
        DateTime? To,
        string? Source,
        int Take = 30,
        int MinScore = 82,
        int MinNoResultCount = 2,
        string? ConfidenceFilter = "MediumOrHigh",
        bool IsActive = true,
        string? NotePrefix = null);

    public sealed record ApplySuggestionsResponseDto(
        int TotalSuggestions,
        int EligibleSuggestions,
        int Created,
        int SkippedExisting,
        int SkippedInvalid,
        int SkippedMissingTarget);

    public sealed record DeactivateProblematicRulesRequestDto(
        DateTime? From,
        DateTime? To,
        string? Source,
        string? Q,
        bool? Active = true,
        int Take = 200,
        int MinQueries = 5,
        decimal MinNoResultRatePct = 40m,
        decimal MaxCtrPct = 8m,
        string? NoteSuffix = "Auto-disabled: low search effectiveness",
        bool DryRun = false);

    public sealed record DeactivateProblematicRulesResponseDto(
        int EvaluatedRules,
        int MatchedProblematicRules,
        int DeactivatedRules,
        int WouldDeactivateRules,
        int SkippedAlreadyInactive,
        int ThresholdMinQueries,
        decimal ThresholdMinNoResultRatePct,
        decimal ThresholdMaxCtrPct,
        bool DryRun,
        IReadOnlyList<string> TriggerQueries);

    public sealed record ReactivateRecoveredRulesRequestDto(
        DateTime? From,
        DateTime? To,
        string? Source,
        string? Q,
        bool? Active = false,
        int Take = 200,
        int MinQueries = 5,
        decimal MaxNoResultRatePct = 15m,
        decimal MinCtrPct = 12m,
        string? NoteSuffix = "Auto-reactivated: search effectiveness recovered",
        bool DryRun = false);

    public sealed record ReactivateRecoveredRulesResponseDto(
        int EvaluatedRules,
        int MatchedRecoveredRules,
        int ReactivatedRules,
        int WouldReactivateRules,
        int SkippedAlreadyActive,
        int ThresholdMinQueries,
        decimal ThresholdMaxNoResultRatePct,
        decimal ThresholdMinCtrPct,
        bool DryRun,
        IReadOnlyList<string> TriggerQueries);

    private sealed record RequestValidationResult(
        bool Ok,
        string? Error,
        string? TriggerQuery,
        string? CanonicalQuery,
        string? TargetType,
        Guid? TargetId,
        bool IsActive,
        string? Note)
    {
        public static RequestValidationResult Fail(string error)
            => new(false, error, null, null, null, null, true, null);

        public static RequestValidationResult Success(
            string triggerQuery,
            string? canonicalQuery,
            string? targetType,
            Guid? targetId,
            bool isActive,
            string? note)
            => new(true, null, triggerQuery, canonicalQuery, targetType, targetId, isActive, note);
    }
}
