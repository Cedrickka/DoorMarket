using DoorMarket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/search-analytics")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminSearchAnalyticsController : ControllerBase
{
    private readonly SearchAnalyticsCalculator _analytics;

    public AdminSearchAnalyticsController(SearchAnalyticsCalculator analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("kpis")]
    public async Task<ActionResult<SearchAnalyticsCalculator.SearchAnalyticsKpis>> GetKpis(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? source,
        CancellationToken ct = default)
    {
        var result = await _analytics.GetKpisAsync(from, to, source, ct);
        return Ok(result);
    }

    [HttpGet("top-queries")]
    public async Task<ActionResult<IReadOnlyList<SearchAnalyticsCalculator.TopSearchQueryRow>>> GetTopQueries(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? source,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _analytics.GetTopQueriesAsync(from, to, source, take, ct);
        return Ok(result);
    }

    [HttpGet("no-result-queries")]
    public async Task<ActionResult<IReadOnlyList<SearchAnalyticsCalculator.NoResultSearchQueryRow>>> GetNoResultQueries(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? source,
        [FromQuery] int take = 20,
        CancellationToken ct = default)
    {
        var result = await _analytics.GetNoResultQueriesAsync(from, to, source, take, ct);
        return Ok(result);
    }
}
