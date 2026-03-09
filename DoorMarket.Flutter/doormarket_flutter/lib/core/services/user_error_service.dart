import 'package:dio/dio.dart';
import 'package:flutter/material.dart';

import '../network/api_exception.dart';

class UserErrorService {
  const UserErrorService._();

  static bool isFr(BuildContext context) =>
      Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';

  static String message(
    BuildContext context,
    Object? error, {
    String? fallbackFr,
    String? fallbackEn,
  }) {
    return messageByLanguage(
      isFr: isFr(context),
      error: error,
      fallbackFr: fallbackFr,
      fallbackEn: fallbackEn,
    );
  }

  static String messageByLanguage({
    required bool isFr,
    required Object? error,
    String? fallbackFr,
    String? fallbackEn,
  }) {
    final fallback = isFr
        ? (fallbackFr ?? 'Une erreur est survenue. Veuillez reessayer.')
        : (fallbackEn ?? 'An error occurred. Please try again.');

    if (error == null) {
      return fallback;
    }

    if (error is ApiException) {
      final fromApi = _clean(error.message);
      if (fromApi.isNotEmpty) {
        return fromApi;
      }
      return _statusFallback(isFr, error.statusCode, fallback);
    }

    if (error is DioException) {
      final statusCode = error.response?.statusCode;
      final data = error.response?.data;
      if (data is Map<String, dynamic>) {
        final apiMessage = _clean((data['error'] ?? data['title'])?.toString());
        if (apiMessage.isNotEmpty) {
          return apiMessage;
        }
      } else if (data is String) {
        final apiMessage = _clean(data);
        if (apiMessage.isNotEmpty) {
          return apiMessage;
        }
      }

      if (error.type == DioExceptionType.connectionTimeout ||
          error.type == DioExceptionType.sendTimeout ||
          error.type == DioExceptionType.receiveTimeout) {
        return isFr
            ? 'Le serveur met trop de temps a repondre. Verifiez votre connexion puis reessayez.'
            : 'The server is taking too long to respond. Check your connection and retry.';
      }

      return _statusFallback(isFr, statusCode, fallback);
    }

    final text = _normalizeExceptionText(error.toString());
    if (text.isEmpty) {
      return fallback;
    }

    final lower = text.toLowerCase();
    if (lower.contains('timeout') ||
        lower.contains('socket') ||
        lower.contains('connection refused') ||
        lower.contains('failed host lookup') ||
        lower.contains('network')) {
      return isFr
          ? 'Connexion indisponible. Verifiez Internet puis reessayez.'
          : 'Connection unavailable. Check Internet and retry.';
    }

    return text;
  }

  static bool isRetryable(Object? error) {
    if (error is ApiException) {
      final status = error.statusCode;
      return status == null || status == 408 || status == 429 || status >= 500;
    }

    if (error is DioException) {
      final status = error.response?.statusCode;
      return error.type == DioExceptionType.connectionTimeout ||
          error.type == DioExceptionType.sendTimeout ||
          error.type == DioExceptionType.receiveTimeout ||
          error.type == DioExceptionType.connectionError ||
          status == null ||
          status == 408 ||
          status == 429 ||
          status >= 500;
    }

    return true;
  }

  static String retryLabel(BuildContext context) =>
      isFr(context) ? 'Reessayer' : 'Retry';

  static String _statusFallback(bool fr, int? status, String fallback) {
    if (status == null) {
      return fr
          ? 'Connexion au serveur indisponible. Veuillez reessayer.'
          : 'Server connection unavailable. Please retry.';
    }

    if (status == 400) {
      return fr
          ? 'La requete est invalide. Verifiez les informations saisies.'
          : 'The request is invalid. Please verify entered information.';
    }
    if (status == 401 || status == 403) {
      return fr
          ? 'Votre session a expire. Reconnectez-vous puis reessayez.'
          : 'Your session has expired. Sign in again and retry.';
    }
    if (status == 404) {
      return fr
          ? 'La ressource demandee est introuvable.'
          : 'The requested resource could not be found.';
    }
    if (status == 409) {
      return fr
          ? 'Conflit detecte. Actualisez les donnees puis reessayez.'
          : 'A conflict was detected. Refresh data and retry.';
    }
    if (status == 429) {
      return fr
          ? 'Trop de requetes. Patientez un instant puis reessayez.'
          : 'Too many requests. Wait a moment and retry.';
    }
    if (status >= 500) {
      return fr
          ? 'Le serveur rencontre une erreur temporaire. Veuillez reessayer.'
          : 'The server has a temporary error. Please retry.';
    }

    return fallback;
  }

  static String _normalizeExceptionText(String value) {
    var text = value.trim();
    if (text.startsWith('ApiException')) {
      final idx = text.indexOf(':');
      if (idx >= 0 && idx < text.length - 1) {
        text = text.substring(idx + 1);
      }
    }
    if (text.startsWith('Exception:')) {
      text = text.substring('Exception:'.length);
    }
    return _clean(text);
  }

  static String _clean(String? value) {
    final text = (value ?? '').trim();
    if (text.isEmpty) {
      return '';
    }

    final compact = text.replaceAll(RegExp(r'\s+'), ' ');
    if (compact.toLowerCase().startsWith('dioexception')) {
      final idx = compact.indexOf(':');
      if (idx >= 0 && idx < compact.length - 1) {
        return compact.substring(idx + 1).trim();
      }
    }
    return compact;
  }
}
