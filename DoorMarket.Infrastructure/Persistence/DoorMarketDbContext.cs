using DoorMarket.Domain.Entities;
using Microsoft.EntityFrameworkCore;


namespace DoorMarket.Infrastructure.Persistence;

public class DoorMarketDbContext : DbContext
{
    public DoorMarketDbContext(DbContextOptions<DoorMarketDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<PaymentMethodSnapshot> PaymentMethodSnapshots => Set<PaymentMethodSnapshot>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<ProcessedWebhookEvent> ProcessedWebhookEvents => Set<ProcessedWebhookEvent>();
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<ShopApplication> ShopApplications => Set<ShopApplication>();
    public DbSet<ShopApplicationDocument> ShopApplicationDocuments => Set<ShopApplicationDocument>();
    public DbSet<UserAddress> UserAddresses => Set<UserAddress>();
    public DbSet<DeliveryZone> DeliveryZones => Set<DeliveryZone>();
    public DbSet<ShopPayout> ShopPayouts => Set<ShopPayout>();
    public DbSet<ShopPayoutStatusHistory> ShopPayoutStatusHistories => Set<ShopPayoutStatusHistory>();
    public DbSet<BankBalanceSnapshot> BankBalanceSnapshots => Set<BankBalanceSnapshot>();
    public DbSet<ShopReview> ShopReviews => Set<ShopReview>();
    public DbSet<ShopReviewHelpfulVote> ShopReviewHelpfulVotes => Set<ShopReviewHelpfulVote>();
    public DbSet<MarketingBanner> MarketingBanners => Set<MarketingBanner>();
    public DbSet<MarketingSegment> MarketingSegments => Set<MarketingSegment>();
    public DbSet<MarketingCampaign> MarketingCampaigns => Set<MarketingCampaign>();
    public DbSet<MarketingCampaignRun> MarketingCampaignRuns => Set<MarketingCampaignRun>();
    public DbSet<PromoAuditLog> PromoAuditLogs => Set<PromoAuditLog>();
    public DbSet<Coupon> Coupons => Set<Coupon>();
    public DbSet<TransactionalNotificationLog> TransactionalNotificationLogs => Set<TransactionalNotificationLog>();
    public DbSet<NotificationIncidentAcknowledgement> NotificationIncidentAcknowledgements => Set<NotificationIncidentAcknowledgement>();
    public DbSet<SearchAnalyticsEvent> SearchAnalyticsEvents => Set<SearchAnalyticsEvent>();
    public DbSet<CheckoutAnalyticsEvent> CheckoutAnalyticsEvents => Set<CheckoutAnalyticsEvent>();
    public DbSet<CheckoutAlertIncident> CheckoutAlertIncidents => Set<CheckoutAlertIncident>();
    public DbSet<SearchQueryRule> SearchQueryRules => Set<SearchQueryRule>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<ReturnRequestStatusHistory> ReturnRequestStatusHistories => Set<ReturnRequestStatusHistory>();
    public DbSet<ReturnReason> ReturnReasons => Set<ReturnReason>();
    public DbSet<LoyaltyWallet> LoyaltyWallets => Set<LoyaltyWallet>();
    public DbSet<LoyaltyPointLedger> LoyaltyPointLedgers => Set<LoyaltyPointLedger>();
    public DbSet<LoyaltyRule> LoyaltyRules => Set<LoyaltyRule>();
    public DbSet<AbandonedCartEvent> AbandonedCartEvents => Set<AbandonedCartEvent>();
    public DbSet<CartRecoveryJobRun> CartRecoveryJobRuns => Set<CartRecoveryJobRun>();
    public DbSet<ReconciliationJobRun> ReconciliationJobRuns => Set<ReconciliationJobRun>();
    public DbSet<UserPushDevice> UserPushDevices => Set<UserPushDevice>();
    public DbSet<CommissionRule> CommissionRules => Set<CommissionRule>();




    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DoorMarketDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
