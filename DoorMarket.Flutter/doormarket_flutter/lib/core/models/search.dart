class SearchSuggestionDto {
  final String type;
  final String? entityId;
  final String value;
  final String label;
  final String? subtitle;

  SearchSuggestionDto({
    required this.type,
    required this.entityId,
    required this.value,
    required this.label,
    required this.subtitle,
  });

  factory SearchSuggestionDto.fromJson(Map<String, dynamic> json) {
    return SearchSuggestionDto(
      type: json['type'] as String? ?? '',
      entityId: json['entityId'] as String?,
      value: json['value'] as String? ?? '',
      label: json['label'] as String? ?? '',
      subtitle: json['subtitle'] as String?,
    );
  }
}

class SearchCategoryDto {
  final String id;
  final String name;
  final String? nameEn;
  final String slug;
  final int activeProductsCount;
  final int activeShopsCount;

  SearchCategoryDto({
    required this.id,
    required this.name,
    required this.nameEn,
    required this.slug,
    required this.activeProductsCount,
    required this.activeShopsCount,
  });

  factory SearchCategoryDto.fromJson(Map<String, dynamic> json) {
    return SearchCategoryDto(
      id: json['id'] as String,
      name: json['name'] as String? ?? '',
      nameEn: json['nameEn'] as String?,
      slug: json['slug'] as String? ?? '',
      activeProductsCount: json['activeProductsCount'] as int? ?? 0,
      activeShopsCount: json['activeShopsCount'] as int? ?? 0,
    );
  }
}

class SearchProductsQuery {
  final String? q;
  final int page;
  final int pageSize;
  final bool inStockOnly;
  final bool promotedOnly;
  final double? minPrice;
  final double? maxPrice;
  final double? ratingMin;
  final String? shopId;
  final String? categoryId;
  final String? city;
  final String? countryTag;
  final String sort;

  const SearchProductsQuery({
    this.q,
    this.page = 1,
    this.pageSize = 20,
    this.inStockOnly = true,
    this.promotedOnly = false,
    this.minPrice,
    this.maxPrice,
    this.ratingMin,
    this.shopId,
    this.categoryId,
    this.city,
    this.countryTag,
    this.sort = 'relevance',
  });
}

class SearchShopsQuery {
  final String? q;
  final int page;
  final int pageSize;
  final bool verifiedOnly;
  final bool recommended;
  final double? ratingMin;
  final String? categoryId;
  final String? city;
  final String? countryTag;
  final String sort;

  const SearchShopsQuery({
    this.q,
    this.page = 1,
    this.pageSize = 20,
    this.verifiedOnly = true,
    this.recommended = false,
    this.ratingMin,
    this.categoryId,
    this.city,
    this.countryTag,
    this.sort = 'relevance',
  });
}

class SearchAnalyticsQueryEvent {
  final String? query;
  final int? resultsCount;
  final int? durationMs;
  final int? page;
  final String? sort;
  final String? filtersHash;
  final String? sessionId;
  final String? source;
  final String? countryTag;

  const SearchAnalyticsQueryEvent({
    this.query,
    this.resultsCount,
    this.durationMs,
    this.page,
    this.sort,
    this.filtersHash,
    this.sessionId,
    this.source,
    this.countryTag,
  });

  Map<String, dynamic> toJson() {
    return {
      'query': query,
      'resultsCount': resultsCount,
      'durationMs': durationMs,
      'page': page,
      'sort': sort,
      'filtersHash': filtersHash,
      'sessionId': sessionId,
      'source': source,
      'countryTag': countryTag,
    };
  }
}

class SearchAnalyticsClickEvent {
  final String? query;
  final String targetType;
  final String targetId;
  final int? position;
  final int? page;
  final String? sort;
  final String? filtersHash;
  final String? sessionId;
  final String? source;
  final String? countryTag;

  const SearchAnalyticsClickEvent({
    this.query,
    required this.targetType,
    required this.targetId,
    this.position,
    this.page,
    this.sort,
    this.filtersHash,
    this.sessionId,
    this.source,
    this.countryTag,
  });

  Map<String, dynamic> toJson() {
    return {
      'query': query,
      'targetType': targetType,
      'targetId': targetId,
      'position': position,
      'page': page,
      'sort': sort,
      'filtersHash': filtersHash,
      'sessionId': sessionId,
      'source': source,
      'countryTag': countryTag,
    };
  }
}
