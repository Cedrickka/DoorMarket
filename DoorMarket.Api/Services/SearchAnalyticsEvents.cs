namespace DoorMarket.Api.Services;

public static class SearchAnalyticsEvents
{
    public const string Query = "Query";
    public const string Click = "Click";

    public const string TargetProduct = "Product";
    public const string TargetShop = "Shop";
    public const string TargetCategory = "Category";

    public static bool IsAllowedTargetType(string value)
        => string.Equals(value, TargetProduct, StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, TargetShop, StringComparison.OrdinalIgnoreCase)
           || string.Equals(value, TargetCategory, StringComparison.OrdinalIgnoreCase);
}
