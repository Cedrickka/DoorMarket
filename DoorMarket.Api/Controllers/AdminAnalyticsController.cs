using DoorMarket.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DoorMarket.Api.Controllers;

[ApiController]
[Route("api/admin/analytics")]
[Authorize(Roles = "Admin,SuperAdmin,3,4")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly FinanceCalculator _finance;

    public AdminAnalyticsController(FinanceCalculator finance)
    {
        _finance = finance;
    }

    [HttpGet("kpis")]
    public async Task<ActionResult<FinanceCalculator.FinanceKpis>> GetKpis(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken ct)
        => Ok(await _finance.GetKpisAsync(from, to, ct));

    [HttpGet("top-products")]
    public async Task<ActionResult<List<FinanceCalculator.TopProductRow>>> GetTopProducts(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);
        return Ok(await _finance.GetTopProductsAsync(from, to, take, ct));
    }

    [HttpGet("top-shops")]
    public async Task<ActionResult<List<FinanceCalculator.TopShopRow>>> GetTopShops(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);
        return Ok(await _finance.GetTopShopsAsync(from, to, take, ct));
    }

    [HttpGet("top-categories")]
    public async Task<ActionResult<List<FinanceCalculator.TopCategoryRow>>> GetTopCategories(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 50);
        return Ok(await _finance.GetTopCategoriesAsync(from, to, take, ct));
    }
}
