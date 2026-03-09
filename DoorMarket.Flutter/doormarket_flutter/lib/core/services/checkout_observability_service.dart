import 'dart:math';

import 'package:shared_preferences/shared_preferences.dart';

import '../api/checkout_analytics_api.dart';

class CheckoutObservabilityService {
  CheckoutObservabilityService(this._api);

  final CheckoutAnalyticsApi _api;

  static const _sessionKey = 'dm_checkout_analytics_session_id';
  static const _groupKey = 'dm_checkout_experiment_group';
  static const _experimentName = 'checkout_ux_v1';

  Future<void> track({
    required String eventName,
    String? orderId,
    String? paymentProvider,
    String? paymentChannel,
    bool? success,
    int? durationMs,
    String? errorCode,
    String? errorMessage,
    Map<String, dynamic>? metadata,
  }) async {
    try {
      final context = await _ensureContext();
      await _api.track(
        eventName: eventName,
        orderId: orderId,
        sessionId: context.sessionId,
        paymentProvider: paymentProvider,
        paymentChannel: paymentChannel,
        experimentName: _experimentName,
        experimentGroup: context.experimentGroup,
        success: success,
        durationMs: durationMs,
        errorCode: errorCode,
        errorMessage: errorMessage,
        source: 'Mobile',
        metadata: metadata,
      );
    } catch (_) {
      // Observability must never interrupt checkout UX.
    }
  }

  Future<_TrackingContext> _ensureContext() async {
    final prefs = await SharedPreferences.getInstance();
    var sessionId = (prefs.getString(_sessionKey) ?? '').trim();
    if (sessionId.isEmpty) {
      sessionId = _generateSessionId();
      await prefs.setString(_sessionKey, sessionId);
    }

    var experimentGroup = (prefs.getString(_groupKey) ?? '').trim().toUpperCase();
    if (experimentGroup != 'A' && experimentGroup != 'B') {
      experimentGroup = Random().nextBool() ? 'A' : 'B';
      await prefs.setString(_groupKey, experimentGroup);
    }

    return _TrackingContext(sessionId: sessionId, experimentGroup: experimentGroup);
  }

  String _generateSessionId() {
    final now = DateTime.now().microsecondsSinceEpoch;
    final rand = Random();
    final suffix = List.generate(12, (_) => rand.nextInt(16).toRadixString(16)).join();
    return '$now$suffix';
  }
}

class _TrackingContext {
  const _TrackingContext({
    required this.sessionId,
    required this.experimentGroup,
  });

  final String sessionId;
  final String experimentGroup;
}
