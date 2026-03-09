import 'package:doormarket_flutter/core/notifications/notification_preferences_controller.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('NotificationPreferencesController', () {
    test('loads defaults and persists toggles', () async {
      SharedPreferences.setMockInitialValues({});
      final c1 = NotificationPreferencesController();
      await _waitLoaded(c1);

      expect(c1.enabled, isTrue);
      expect(c1.ordersEnabled, isTrue);
      expect(c1.paymentsEnabled, isTrue);
      expect(c1.deliveryEnabled, isTrue);

      await c1.setEnabled(false);
      await c1.setOrdersEnabled(false);
      await c1.setPaymentsEnabled(false);
      await c1.setDeliveryEnabled(false);

      final c2 = NotificationPreferencesController();
      await _waitLoaded(c2);
      expect(c2.enabled, isFalse);
      expect(c2.ordersEnabled, isFalse);
      expect(c2.paymentsEnabled, isFalse);
      expect(c2.deliveryEnabled, isFalse);
    });

    test('marks events as read and can reset history', () async {
      SharedPreferences.setMockInitialValues({});
      final c = NotificationPreferencesController();
      await _waitLoaded(c);

      await c.markRead('event-1');
      await c.markManyRead(['event-2', 'event-3']);
      expect(c.isRead('event-1'), isTrue);
      expect(c.isRead('event-2'), isTrue);
      expect(c.readEventsCount, 3);

      await c.clearReadEvents();
      expect(c.readEventsCount, 0);
      expect(c.isRead('event-1'), isFalse);
    });

    test('allowEventKind respects category toggles', () async {
      SharedPreferences.setMockInitialValues({});
      final c = NotificationPreferencesController();
      await _waitLoaded(c);

      expect(c.allowEventKind('payment_failed'), isTrue);
      await c.setPaymentsEnabled(false);
      expect(c.allowEventKind('payment_failed'), isFalse);
      expect(c.allowEventKind('in_progress'), isTrue);
      await c.setOrdersEnabled(false);
      expect(c.allowEventKind('in_progress'), isFalse);
      expect(c.allowEventKind('delivered'), isTrue);
      await c.setDeliveryEnabled(false);
      expect(c.allowEventKind('delivered'), isFalse);
    });
  });
}

Future<void> _waitLoaded(NotificationPreferencesController controller) async {
  for (var i = 0; i < 60; i++) {
    if (controller.loaded) return;
    await Future<void>.delayed(const Duration(milliseconds: 5));
  }
  fail('NotificationPreferencesController did not load in time');
}
