class QueryParams {
  static String build(String path, Map<String, String?> params, {Set<String> keepEmptyKeys = const {}}) {
    final filtered = <String, String>{};
    params.forEach((key, value) {
      if (value != null) {
        if (value.isNotEmpty || keepEmptyKeys.contains(key)) {
          filtered[key] = value;
        }
      } else if (keepEmptyKeys.contains(key)) {
        filtered[key] = '';
      }
    });
    if (filtered.isEmpty) return path;
    final query = filtered.entries
        .map((e) => '${Uri.encodeQueryComponent(e.key)}=${Uri.encodeQueryComponent(e.value)}')
        .join('&');
    return '$path?$query';
  }
}
