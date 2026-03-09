class ShopDto {
  final String id;
  final String name;
  final String? imageUrl;
  final String countryTag;
  final String city;
  final bool isVerified;
  final double? rating;
  final int? reviewCount;
  final DateTime createdAtUtc;

  ShopDto({
    required this.id,
    required this.name,
    this.imageUrl,
    required this.countryTag,
    required this.city,
    required this.isVerified,
    this.rating,
    this.reviewCount,
    required this.createdAtUtc,
  });

  factory ShopDto.fromJson(Map<String, dynamic> json) {
    return ShopDto(
      id: json['id'] as String,
      name: json['name'] as String,
      imageUrl: json['imageUrl'] as String?,
      countryTag: json['countryTag'] as String? ?? '',
      city: json['city'] as String? ?? '',
      isVerified: json['isVerified'] as bool? ?? false,
      rating: (json['rating'] as num?)?.toDouble(),
      reviewCount: json['reviewCount'] as int?,
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
    );
  }
}

class ShopQuery {
  final String? countryTag;
  final String? city;
  final String? q;
  final int page;
  final int pageSize;
  final bool verifiedOnly;

  const ShopQuery({
    this.countryTag,
    this.city,
    this.q,
    this.page = 1,
    this.pageSize = 20,
    this.verifiedOnly = true,
  });
}

class ShopReviewDto {
  final String id;
  final String shopId;
  final String userId;
  final int rating;
  final String? comment;
  final DateTime createdAtUtc;
  final String? userEmail;
  final bool isVerifiedPurchase;
  final List<String> photoUrls;
  final List<String> videoUrls;
  final int helpfulCount;
  final bool isHelpfulByCurrentUser;

  const ShopReviewDto({
    required this.id,
    required this.shopId,
    required this.userId,
    required this.rating,
    this.comment,
    required this.createdAtUtc,
    this.userEmail,
    required this.isVerifiedPurchase,
    required this.photoUrls,
    required this.videoUrls,
    required this.helpfulCount,
    required this.isHelpfulByCurrentUser,
  });

  factory ShopReviewDto.fromJson(Map<String, dynamic> json) {
    final photos = (json['photoUrls'] as List<dynamic>? ?? const [])
        .map((e) => e?.toString() ?? '')
        .where((e) => e.trim().isNotEmpty)
        .toList(growable: false);
    final videos = (json['videoUrls'] as List<dynamic>? ?? const [])
        .map((e) => e?.toString() ?? '')
        .where((e) => e.trim().isNotEmpty)
        .toList(growable: false);

    return ShopReviewDto(
      id: json['id'] as String? ?? '',
      shopId: json['shopId'] as String? ?? '',
      userId: json['userId'] as String? ?? '',
      rating: json['rating'] as int? ?? 0,
      comment: json['comment'] as String?,
      createdAtUtc: DateTime.tryParse(json['createdAtUtc'] as String? ?? '') ??
          DateTime.fromMillisecondsSinceEpoch(0, isUtc: true),
      userEmail: json['userEmail'] as String?,
      isVerifiedPurchase: json['isVerifiedPurchase'] as bool? ?? false,
      photoUrls: photos,
      videoUrls: videos,
      helpfulCount: json['helpfulCount'] as int? ?? 0,
      isHelpfulByCurrentUser: json['isHelpfulByCurrentUser'] as bool? ?? false,
    );
  }
}

class ShopReviewSummaryDto {
  final String shopId;
  final double? averageRating;
  final int reviewCount;
  final int count1;
  final int count2;
  final int count3;
  final int count4;
  final int count5;

  const ShopReviewSummaryDto({
    required this.shopId,
    this.averageRating,
    required this.reviewCount,
    required this.count1,
    required this.count2,
    required this.count3,
    required this.count4,
    required this.count5,
  });

  factory ShopReviewSummaryDto.fromJson(Map<String, dynamic> json) {
    return ShopReviewSummaryDto(
      shopId: json['shopId'] as String? ?? '',
      averageRating: (json['averageRating'] as num?)?.toDouble(),
      reviewCount: json['reviewCount'] as int? ?? 0,
      count1: json['count1'] as int? ?? 0,
      count2: json['count2'] as int? ?? 0,
      count3: json['count3'] as int? ?? 0,
      count4: json['count4'] as int? ?? 0,
      count5: json['count5'] as int? ?? 0,
    );
  }
}

class CreateShopReviewRequest {
  final int rating;
  final String? comment;
  final String? orderId;
  final List<String>? photoUrls;
  final List<String>? videoUrls;

  const CreateShopReviewRequest({
    required this.rating,
    this.comment,
    this.orderId,
    this.photoUrls,
    this.videoUrls,
  });

  Map<String, dynamic> toJson() => {
        'rating': rating,
        'comment': comment,
        'orderId': orderId,
        'photoUrls': photoUrls,
        'videoUrls': videoUrls,
      };
}

class ShopReviewHelpfulVoteResultDto {
  final String reviewId;
  final int helpfulCount;
  final bool isHelpfulByCurrentUser;

  const ShopReviewHelpfulVoteResultDto({
    required this.reviewId,
    required this.helpfulCount,
    required this.isHelpfulByCurrentUser,
  });

  factory ShopReviewHelpfulVoteResultDto.fromJson(Map<String, dynamic> json) {
    return ShopReviewHelpfulVoteResultDto(
      reviewId: json['reviewId'] as String? ?? '',
      helpfulCount: json['helpfulCount'] as int? ?? 0,
      isHelpfulByCurrentUser: json['isHelpfulByCurrentUser'] as bool? ?? false,
    );
  }
}
