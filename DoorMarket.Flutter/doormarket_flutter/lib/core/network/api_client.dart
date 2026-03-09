import 'dart:async';
import 'dart:convert';
import 'dart:ui';

import 'package:dio/dio.dart';

import '../storage/token_store.dart';
import 'api_exception.dart';

class ApiClient {
  ApiClient({
    required this.baseUrl,
    required this.tokenStore,
    required this.onRefresh,
    required this.onLogout,
    Dio? dio,
  }) : _dio = dio ?? Dio() {
    _dio.options = BaseOptions(
      baseUrl: baseUrl,
      connectTimeout: const Duration(seconds: 20),
      receiveTimeout: const Duration(seconds: 20),
      sendTimeout: const Duration(seconds: 20),
    );

    _dio.interceptors.add(InterceptorsWrapper(
      onRequest: _handleRequest,
      onError: _handleError,
    ));
  }

  final String baseUrl;
  final TokenStore tokenStore;
  final Future<bool> Function() onRefresh;
  final Future<void> Function() onLogout;
  final Dio _dio;

  Dio get raw => _dio;

  Future<Response<T>> get<T>(String path,
      {Map<String, dynamic>? queryParameters}) {
    return _dio.get<T>(path, queryParameters: queryParameters);
  }

  Future<Response<T>> post<T>(String path, dynamic data) {
    return _dio.post<T>(path, data: data);
  }

  Future<Response<T>> put<T>(String path, dynamic data) {
    return _dio.put<T>(path, data: data);
  }

  Future<Response<T>> delete<T>(String path,
      {Map<String, dynamic>? queryParameters}) {
    return _dio.delete<T>(path, queryParameters: queryParameters);
  }

  Future<T> read<T>(
      Future<Response> request, T Function(dynamic) mapper) async {
    try {
      final response = await request;
      return mapper(response.data);
    } on DioException catch (ex) {
      throw _mapError(ex);
    }
  }

  ApiException _mapError(DioException ex) {
    final status = ex.response?.statusCode;
    if (ex.type == DioExceptionType.connectionTimeout ||
        ex.type == DioExceptionType.sendTimeout ||
        ex.type == DioExceptionType.receiveTimeout) {
      return ApiException(
          "La requete a expire. Verifiez la connexion ou l'etat de l'API.",
          statusCode: status);
    }

    final data = ex.response?.data;
    final text = data is String ? data : jsonEncode(data ?? {});
    final parsed = _parseError(text, ex.response?.statusMessage);
    return ApiException(parsed.$1, statusCode: status, code: parsed.$2);
  }

  static (String, String?) _parseError(String? text, String? fallback) {
    final fallbackMessage =
        (text == null || text.isEmpty) ? (fallback ?? 'Erreur serveur.') : text;
    if (text == null || text.isEmpty) return (fallbackMessage, null);

    try {
      final json = jsonDecode(text);
      if (json is Map<String, dynamic>) {
        final message = json['error'] as String? ?? json['title'] as String?;
        final code = json['code'] as String?;
        if (message != null && message.isNotEmpty) {
          return (message, code);
        }
      }
    } catch (_) {}

    return (fallbackMessage, null);
  }

  Future<void> _handleRequest(
      RequestOptions options, RequestInterceptorHandler handler) async {
    final path =
        options.path.startsWith('/') ? options.path.substring(1) : options.path;
    if (!path.startsWith('api/auth')) {
      final token = await tokenStore.getAccessToken();
      if (token != null && token.isNotEmpty) {
        options.headers['Authorization'] = 'Bearer $token';
      }
    }

    final locale = PlatformDispatcher.instance.locale;
    final lang = locale.languageCode.toLowerCase();
    options.headers['Accept-Language'] = lang;
    options.headers['X-App-Lang'] = lang;
    handler.next(options);
  }

  Future<void> _handleError(
      DioException err, ErrorInterceptorHandler handler) async {
    final status = err.response?.statusCode;
    final rawPath = err.requestOptions.path.startsWith('/')
        ? err.requestOptions.path.substring(1)
        : err.requestOptions.path;
    final isAuthEndpoint = rawPath.startsWith('api/auth');
    final alreadyRetried = err.requestOptions.extra['dm_retried'] == true;
    final skipRefresh = err.requestOptions.extra['dm_skip_refresh'] == true;
    if (status == 401 && !isAuthEndpoint && !alreadyRetried) {
      if (skipRefresh) {
        handler.next(err);
        return;
      }
      final refreshed = await onRefresh();
      if (refreshed) {
        final newToken = await tokenStore.getAccessToken();
        final opts = err.requestOptions;
        final clone = await _retryRequest(opts, newToken);
        handler.resolve(clone);
        return;
      }

      await onLogout();
    }

    handler.next(err);
  }

  Future<Response<dynamic>> _retryRequest(
      RequestOptions options, String? token) {
    final newOptions = Options(
      method: options.method,
      headers: Map<String, dynamic>.from(options.headers),
      responseType: options.responseType,
      contentType: options.contentType,
      extra: Map<String, dynamic>.from(options.extra)..['dm_retried'] = true,
    );
    if (token != null && token.isNotEmpty) {
      newOptions.headers?['Authorization'] = 'Bearer $token';
    }

    return _dio.request<dynamic>(
      options.path,
      data: options.data,
      queryParameters: options.queryParameters,
      options: newOptions,
      cancelToken: options.cancelToken,
      onSendProgress: options.onSendProgress,
      onReceiveProgress: options.onReceiveProgress,
    );
  }
}
