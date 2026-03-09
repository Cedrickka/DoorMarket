using DoorMarket.Domain.Common;
using DoorMarket.Domain.Enums;

namespace DoorMarket.Domain.Entities;

public class User : AuditableEntity
{
    public string Email { get; set; } = "";
    public bool EmailConfirmed { get; set; } = false;
    public string? EmailVerificationCode { get; set; }
    public DateTime? EmailVerificationExpiresAtUtc { get; set; }
    public string? EmailVerificationPurpose { get; set; }
    public string? Phone { get; set; }
    public string? PendingPhone { get; set; }
    public string? ProfileImageUrl { get; set; }

    public string PasswordHash { get; set; } = "";
    public string? PendingPasswordHash { get; set; }

    public UserRole Role { get; set; } = UserRole.Client;

    public bool IsActive { get; set; } = true;

    // Navigation
    public List<RefreshToken> RefreshTokens { get; set; } = new();

    // Un user peut être propriétaire d'une boutique (optionnel)
    public Shop? Shop { get; set; }
    public List<UserAddress> Addresses { get; set; } = new();
    public List<ShopReview> ShopReviews { get; set; } = new();
    public List<ShopReviewHelpfulVote> ShopReviewHelpfulVotes { get; set; } = new();
    public List<ReturnRequest> ReturnRequests { get; set; } = new();
    public List<ReturnRequest> ReviewedReturnRequests { get; set; } = new();
    public List<ReturnRequestStatusHistory> ReturnRequestStatusHistories { get; set; } = new();
    public LoyaltyWallet? LoyaltyWallet { get; set; }
    public List<LoyaltyPointLedger> LoyaltyPointLedgers { get; set; } = new();

}
