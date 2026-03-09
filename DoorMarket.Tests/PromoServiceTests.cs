using DoorMarket.Infrastructure.Carts;

namespace DoorMarket.Tests;

public class PromoServiceTests
{
    private readonly PromoService _sut = new();

    [Theory]
    [InlineData("DOOR10", 40, 4)]
    [InlineData("door10", 40, 4)]
    [InlineData("WELCOME5", 25, 5)]
    [InlineData("WELCOME5", 3, 3)]
    public void Evaluate_WithValidCode_ReturnsAppliedDiscount(string code, decimal subtotal, decimal expectedDiscount)
    {
        var result = _sut.Evaluate(code, subtotal);

        Assert.True(result.Applied);
        Assert.Equal(expectedDiscount, result.Discount);
        Assert.False(string.IsNullOrWhiteSpace(result.PromoCode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("INVALID")]
    public void Evaluate_WithInvalidCode_ReturnsNoDiscount(string? code)
    {
        var result = _sut.Evaluate(code, 100m);

        Assert.False(result.Applied);
        Assert.Equal(0m, result.Discount);
        Assert.Null(result.PromoCode);
    }
}
