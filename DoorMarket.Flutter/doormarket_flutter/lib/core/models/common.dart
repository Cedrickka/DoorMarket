class PagedResult<T> {
  final int page;
  final int pageSize;
  final int total;
  final List<T> items;

  PagedResult({
    required this.page,
    required this.pageSize,
    required this.total,
    required this.items,
  });

  factory PagedResult.fromJson(Map<String, dynamic> json, T Function(dynamic) mapper) {
    final itemsJson = (json['items'] as List<dynamic>? ?? []);
    return PagedResult(
      page: json['page'] as int? ?? 1,
      pageSize: json['pageSize'] as int? ?? itemsJson.length,
      total: json['total'] as int? ?? itemsJson.length,
      items: itemsJson.map(mapper).toList(),
    );
  }
}
