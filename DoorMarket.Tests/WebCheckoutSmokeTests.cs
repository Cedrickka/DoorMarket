using System.Reflection;
using DoorMarket.Api.Controllers;
using DoorMarket.Api.Services;
using DoorMarket.Application.DTOs.Cart;
using DoorMarket.Application.DTOs.Orders;
using DoorMarket.Application.Interfaces.Auth;
using DoorMarket.Application.Interfaces.Loyalty;
using DoorMarket.Application.Interfaces.Payments;
using DoorMarket.Domain.Entities;
using DoorMarket.Domain.Enums;
using DoorMarket.Infrastructure.Carts;
using DoorMarket.Infrastructure.Orders;
using DoorMarket.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace DoorMarket.Tests;

public class WebCheckoutSmokeTests
{
    [Fact]
    public async Task CheckoutPayAndResume_WithPayPalCaptureRetry_CompletesOrderAndClearsCart()
    {
        await using var fixture = await SmokeFixture.CreateAsync();

        var readiness = AssertOk(await fixture.CartController.PreCheckout(
            new PreCheckoutRequest(
                DeliveryZoneId: fixture.ZoneId,
                PromoCode: null,
                PaymentProvider: "paypal",
                PrepaidCardCode: null,
                RequireDeliveryZone: true),
            CancellationToken.None));

        Assert.True(readiness.IsReady);
        Assert.Equal("PayPal", readiness.PaymentProvider);
        Assert.Equal(2, readiness.ItemCount);

        var order = AssertOrderOk(await fixture.OrdersController.Checkout(
            new CheckoutRequest(
                DeliveryName: "Client DM60",
                DeliveryPhone: "+243990000001",
                DeliveryLine1: "Avenue Test 1",
                DeliveryCity: "Kinshasa",
                DeliveryCountry: "CD",
                DeliveryZoneId: fixture.ZoneId,
                DeliveryNotes: "Smoke DM60",
                PromoCode: null,
                PaymentProvider: "PayPal",
                PrepaidCardCode: null),
            CancellationToken.None));

        Assert.Equal("Unpaid", order.PaymentStatus);
        Assert.Equal("PayPal", order.PaymentProvider);
        Assert.Equal(1, fixture.ClientNotifications.CreatedCount);

        var createPayPal = Assert.IsType<OkObjectResult>(
            await fixture.PaymentsController.CreatePayPalOrder(order.Id, CancellationToken.None));
        var payPalOrderId = GetStringProperty(createPayPal.Value, "payPalOrderId");
        Assert.False(string.IsNullOrWhiteSpace(GetStringProperty(createPayPal.Value, "url")));

        var firstCapture = Assert.IsType<OkObjectResult>(
            await fixture.PaymentsController.CapturePayPal(
                new PaymentsController.PayPalCaptureRequest(order.Id, payPalOrderId),
                CancellationToken.None));
        Assert.False(GetBoolProperty(firstCapture.Value, "paid"));

        var afterFirstAttempt = AssertOrderOk(await fixture.OrdersController.Get(order.Id, CancellationToken.None));
        Assert.Equal("Unpaid", afterFirstAttempt.PaymentStatus);

        var secondCapture = Assert.IsType<OkObjectResult>(
            await fixture.PaymentsController.CapturePayPal(
                new PaymentsController.PayPalCaptureRequest(order.Id, payPalOrderId),
                CancellationToken.None));
        Assert.True(GetBoolProperty(secondCapture.Value, "paid"));
        Assert.Equal(2, fixture.PayPal.CaptureCount);

        var paidOrder = AssertOrderOk(await fixture.OrdersController.Get(order.Id, CancellationToken.None));
        Assert.Equal("Paid", paidOrder.PaymentStatus);
        Assert.Equal("Paid", paidOrder.Status);
        Assert.Equal(1, fixture.ClientNotifications.PaidCount);

        var cartItemCount = await fixture.Db.CartItems.AsNoTracking()
            .CountAsync(x => x.CartId == fixture.CartId);
        Assert.Equal(0, cartItemCount);

        Assert.Contains(fixture.Observability.Events, x => x.EventType == CheckoutObservabilityEvents.OrderCreated);
        Assert.Contains(fixture.Observability.Events, x => x.EventType == CheckoutObservabilityEvents.PaymentInitiated);
        Assert.Contains(fixture.Observability.Events, x => x.EventType == CheckoutObservabilityEvents.PaymentFailed);
        Assert.Contains(fixture.Observability.Events, x => x.EventType == CheckoutObservabilityEvents.PaymentConfirmed);
    }

    [Fact]
    public async Task RecoveryStatus_WithActiveReminder_ReturnsCheckoutResumeData()
    {
        await using var fixture = await SmokeFixture.CreateAsync();

        fixture.Db.AbandonedCartEvents.Add(new AbandonedCartEvent
        {
            UserId = fixture.ClientUserId,
            CartId = fixture.CartId,
            Currency = "USD",
            ItemCount = 2,
            Subtotal = 40m,
            LastCartActivityAtUtc = DateTime.UtcNow.AddHours(-3),
            DetectedAtUtc = DateTime.UtcNow.AddHours(-2),
            ReminderStatus = NotificationEvents.StatusSent,
            ReminderAttemptCount = 1,
            SentChannels = "Email",
            RecipientEmail = "client-smoke@test.local",
            ReminderSentAtUtc = DateTime.UtcNow.AddHours(-2)
        });
        await fixture.Db.SaveChangesAsync();

        var recovery = AssertRecoveryOk(await fixture.CartController.GetRecoveryStatus(30, CancellationToken.None));
        Assert.True(recovery.HasActiveReminder);
        Assert.Equal(2, recovery.ItemCount);
        Assert.Equal(40m, recovery.Subtotal);
        Assert.Equal("USD", recovery.Currency);
        Assert.Equal("/checkout", recovery.CheckoutPath);
    }

    private static CheckoutReadinessDto AssertOk(ActionResult<CheckoutReadinessDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<CheckoutReadinessDto>(ok.Value);
    }

    private static CartRecoveryStatusDto AssertRecoveryOk(ActionResult<CartRecoveryStatusDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<CartRecoveryStatusDto>(ok.Value);
    }

    private static OrderDto AssertOrderOk(ActionResult<OrderDto> result)
    {
        var ok = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<OrderDto>(ok.Value);
    }

    private static string GetStringProperty(object? value, string name)
    {
        var property = GetProperty(value, name);
        var raw = Assert.IsType<string>(property.GetValue(value));
        return raw;
    }

    private static bool GetBoolProperty(object? value, string name)
    {
        var property = GetProperty(value, name);
        var raw = Assert.IsType<bool>(property.GetValue(value));
        return raw;
    }

    private static PropertyInfo GetProperty(object? value, string name)
    {
        Assert.NotNull(value);
        var property = value!.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
        Assert.NotNull(property);
        return property!;
    }

    private sealed class SmokeFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private SmokeFixture(
            SqliteConnection connection,
            DoorMarketDbContext db,
            Guid clientUserId,
            Guid cartId,
            Guid zoneId,
            CartController cartController,
            OrdersController ordersController,
            PaymentsController paymentsController,
            FakePayPalPaymentService payPal,
            FakeCheckoutObservabilityService observability,
            FakeClientOrderNotificationService clientNotifications)
        {
            _connection = connection;
            Db = db;
            ClientUserId = clientUserId;
            CartId = cartId;
            ZoneId = zoneId;
            CartController = cartController;
            OrdersController = ordersController;
            PaymentsController = paymentsController;
            PayPal = payPal;
            Observability = observability;
            ClientNotifications = clientNotifications;
        }

        public DoorMarketDbContext Db { get; }
        public Guid ClientUserId { get; }
        public Guid CartId { get; }
        public Guid ZoneId { get; }
        public CartController CartController { get; }
        public OrdersController OrdersController { get; }
        public PaymentsController PaymentsController { get; }
        public FakePayPalPaymentService PayPal { get; }
        public FakeCheckoutObservabilityService Observability { get; }
        public FakeClientOrderNotificationService ClientNotifications { get; }

        public static async Task<SmokeFixture> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<DoorMarketDbContext>()
                .UseSqlite(connection)
                .Options;

            var db = new DoorMarketDbContext(options);
            await db.Database.EnsureCreatedAsync();

            var clientUser = new User
            {
                Email = "client-smoke@test.local",
                PasswordHash = "hash",
                Role = UserRole.Client
            };

            var ownerUser = new User
            {
                Email = "owner-smoke@test.local",
                PasswordHash = "hash",
                Role = UserRole.Shop
            };

            var category = new Category
            {
                Name = "Epicerie Smoke",
                Slug = "epicerie-smoke"
            };

            var shop = new Shop
            {
                OwnerUser = ownerUser,
                Name = "Shop Smoke",
                CountryTag = "RDC",
                City = "Kinshasa",
                IsVerified = true
            };

            var product = new Product
            {
                Shop = shop,
                Category = category,
                Name = "Produit Smoke",
                Price = 20m,
                Currency = "USD",
                StockQty = 20,
                IsActive = true
            };

            var cart = new Cart
            {
                User = clientUser
            };

            var cartItem = new CartItem
            {
                Cart = cart,
                Product = product,
                Qty = 2,
                UnitPrice = 20m
            };

            var zone = new DeliveryZone
            {
                Code = "KIN-SMOKE",
                Name = "Kin Centre",
                Country = "CD",
                FeeUsd = 5m,
                IsActive = true
            };

            db.AddRange(clientUser, ownerUser, category, shop, product, cart, cartItem, zone);
            await db.SaveChangesAsync();

            var current = new TestCurrentUserService(clientUser.Id);
            var promoService = new PromoService();
            var cartService = new CartService(db, current);
            var orderService = new OrderService(db, current, promoService);
            var clientNotifications = new FakeClientOrderNotificationService();
            var observability = new FakeCheckoutObservabilityService(current);

            var workflow = new OrderPaymentWorkflowService(
                db,
                new FakeOrderNotificationService(),
                new FakeAdminOrderNotificationService(),
                clientNotifications,
                new FakeLoyaltyService(),
                NullLogger<OrderPaymentWorkflowService>.Instance);

            var payPal = new FakePayPalPaymentService();
            var cartController = new CartController(cartService, promoService, current, db);
            var ordersController = new OrdersController(
                orderService,
                clientNotifications,
                observability,
                NullLogger<OrdersController>.Instance);
            var paymentsController = new PaymentsController(
                new FakeStripePaymentService(),
                payPal,
                new FakeMobileMoneyPaymentService(),
                workflow,
                observability,
                db,
                current);

            return new SmokeFixture(
                connection,
                db,
                clientUser.Id,
                cart.Id,
                zone.Id,
                cartController,
                ordersController,
                paymentsController,
                payPal,
                observability,
                clientNotifications);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public TestCurrentUserService(Guid userId)
        {
            UserId = userId;
        }

        public Guid? UserId { get; }
        public string? Email => "client-smoke@test.local";
        public string? Role => "Client";
        public bool IsAuthenticated => true;
    }

    private sealed class FakeCheckoutObservabilityService : ICheckoutObservabilityService
    {
        private readonly ICurrentUserService _current;

        public FakeCheckoutObservabilityService(ICurrentUserService current)
        {
            _current = current;
        }

        public List<CheckoutAnalyticsEvent> Events { get; } = new();

        public Task<CheckoutAnalyticsEvent> TrackAsync(
            string eventType,
            Guid? orderId = null,
            string? sessionId = null,
            string? paymentProvider = null,
            string? paymentChannel = null,
            string? experimentName = null,
            string? experimentGroup = null,
            bool? success = null,
            int? durationMs = null,
            string? errorCode = null,
            string? errorMessage = null,
            string? source = null,
            string? countryTag = null,
            object? metadata = null,
            CancellationToken ct = default)
        {
            var row = new CheckoutAnalyticsEvent
            {
                UserId = _current.UserId,
                OrderId = orderId,
                SessionId = sessionId,
                EventType = eventType,
                PaymentProvider = paymentProvider,
                PaymentChannel = paymentChannel,
                ExperimentName = experimentName,
                ExperimentGroup = experimentGroup,
                Success = success,
                DurationMs = durationMs,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                Source = source,
                CountryTag = countryTag,
                MetadataJson = metadata?.ToString(),
                OccurredAtUtc = DateTime.UtcNow
            };

            Events.Add(row);
            return Task.FromResult(row);
        }
    }

    private sealed class FakeStripePaymentService : IStripePaymentService
    {
        public Task<(string clientSecret, string paymentIntentId)> CreatePaymentIntentAsync(Guid orderId, CancellationToken ct)
            => Task.FromResult(("pi_client_secret_smoke", "pi_smoke_1"));

        public Task<(string url, string sessionId)> CreateCheckoutSessionAsync(Guid orderId, CancellationToken ct)
            => Task.FromResult(("https://stripe.test/checkout", "cs_smoke_1"));
    }

    private sealed class FakePayPalPaymentService : IPayPalPaymentService
    {
        private bool _firstCapture = true;

        public int CaptureCount { get; private set; }

        public Task<(string ApprovalUrl, string PayPalOrderId)> CreateOrderAsync(Guid orderId, CancellationToken ct)
            => Task.FromResult(("https://paypal.test/checkout", "PP-ORDER-SMOKE-1"));

        public Task<PayPalCaptureResult> CaptureOrderAsync(Guid orderId, string payPalOrderId, CancellationToken ct)
        {
            CaptureCount++;
            if (_firstCapture)
            {
                _firstCapture = false;
                return Task.FromResult(new PayPalCaptureResult(
                    Paid: false,
                    PaymentStatus: "Failed",
                    Message: "Capture refused by provider.",
                    CaptureId: null,
                    FundingSource: "paypal:wallet"));
            }

            return Task.FromResult(new PayPalCaptureResult(
                Paid: true,
                PaymentStatus: "Paid",
                Message: "Capture completed.",
                CaptureId: "CAP-SMOKE-1",
                FundingSource: "paypal:wallet"));
        }
    }

    private sealed class FakeMobileMoneyPaymentService : IMobileMoneyPaymentService
    {
        public Task<MobileMoneyCheckoutResult> InitiateAsync(
            Guid orderId,
            string provider,
            string phoneNumber,
            string? callbackUrl,
            CancellationToken ct)
            => Task.FromResult(new MobileMoneyCheckoutResult(
                Provider: provider,
                TransactionId: "MM-SMOKE-1",
                Status: "PENDING",
                CheckoutUrl: null,
                Message: "Pending"));

        public Task<MobileMoneyPaymentStatusResult> CheckStatusAsync(
            Guid orderId,
            string provider,
            string transactionId,
            CancellationToken ct)
            => Task.FromResult(new MobileMoneyPaymentStatusResult(
                Provider: provider,
                TransactionId: transactionId,
                Status: "PENDING",
                Paid: false,
                Message: "Pending",
                RawStatus: "PENDING"));
    }

    private sealed class FakeOrderNotificationService : IOrderNotificationService
    {
        public Task NotifyShopsOrderPaidPendingOnceAsync(Guid orderId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeAdminOrderNotificationService : IAdminOrderNotificationService
    {
        public Task NotifyAdminOrderPaidOnceAsync(Guid orderId, CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeClientOrderNotificationService : IClientOrderNotificationService
    {
        public int CreatedCount { get; private set; }
        public int PaidCount { get; private set; }
        public int FailedCount { get; private set; }

        public Task NotifyClientOrderCreatedAsync(Guid orderId, CancellationToken ct)
        {
            CreatedCount++;
            return Task.CompletedTask;
        }

        public Task NotifyClientPaymentPaidAsync(Guid orderId, CancellationToken ct)
        {
            PaidCount++;
            return Task.CompletedTask;
        }

        public Task NotifyClientPaymentFailedAsync(Guid orderId, string? reason, CancellationToken ct)
        {
            FailedCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLoyaltyService : ILoyaltyService
    {
        public Task AwardOrderPaidAsync(Guid orderId, CancellationToken ct) => Task.CompletedTask;
    }
}
