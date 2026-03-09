class AppConfig {
  static final String apiBaseUrl = _ensureTrailingSlash(
    const String.fromEnvironment(
      'API_BASE_URL',
      defaultValue: 'http://api.door-market.com',
    ),
  );

  static String _ensureTrailingSlash(String value) {
    if (value.isEmpty) return value;
    return value.endsWith('/') ? value : '$value/';
  }
}
