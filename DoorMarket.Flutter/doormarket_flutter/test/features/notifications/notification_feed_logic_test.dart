import 'package:doormarket_flutter/core/models/orders.dart';
import 'package:doormarket_flutter/features/notifications/notification_feed_logic.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  group('notification_feed_logic', () {
    test('resolveClientNotificationKind maps payment and fulfillment states',
        () {
      final failed = _buildOrder(paymentStatus: 'Failed');
      final pending = _buildOrder(paymentStatus: 'Pending');
      final paid = _buildOrder(paymentStatus: 'Paid');
      final delivered = _buildOrder(
        paymentStatus: 'Paid',
        fulfillmentStatus: 'Delivered',
      );
      final inProgress = _buildOrder(
        paymentStatus: 'Created',
        fulfillmentStatus: 'Preparing',
      );

      expect(resolveClientNotificationKind(failed),
          ClientNotificationKind.paymentFailed);
      expect(resolveClientNotificationKind(pending),
          ClientNotificationKind.paymentPending);
      expect(resolveClientNotificationKind(paid),
          ClientNotificationKind.paymentPaid);
      expect(resolveClientNotificationKind(delivered),
          ClientNotificationKind.delivered);
      expect(resolveClientNotificationKind(inProgress),
          ClientNotificationKind.inProgress);
    });

    test(
        'buildClientNotificationEventId is stable and includes order + kind + timestamp',
        () {
      final order = _buildOrder(
        id: '11111111-1111-1111-1111-111111111111',
        paymentStatus: 'Failed',
        createdAtUtc: DateTime.parse('2026-01-01T00:00:00Z'),
      );
      final kind = resolveClientNotificationKind(order);

      final eventId1 = buildClientNotificationEventId(order, kind);
      final eventId2 = buildClientNotificationEventId(order, kind);

      expect(eventId1, eventId2);
      expect(eventId1, contains(order.id));
      expect(eventId1, contains('payment_failed'));
      expect(eventId1, contains('2026-01-01T00:00:00.000Z'));
    });
  });
}

OrderDto _buildOrder({
  String id = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  String paymentStatus = 'Paid',
  String fulfillmentStatus = 'Pending',
  DateTime? createdAtUtc,
}) {
  return OrderDto(
    id: id,
    status: 'Created',
    paymentStatus: paymentStatus,
    fulfillmentStatus: fulfillmentStatus,
    paymentProvider: 'PayPal',
    subtotal: 10,
    deliveryFee: 2,
    discount: 0,
    totalAmount: 12,
    currency: 'USD',
    createdAtUtc: createdAtUtc ?? DateTime.parse('2026-01-02T00:00:00Z'),
    paymentMethodSnapshot: null,
    items: const [],
  );
}
