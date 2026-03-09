import 'package:doormarket_flutter/core/models/orders.dart';
import 'package:doormarket_flutter/core/storage/notification_feed_cache.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('NotificationFeedCache', () {
    test('saves and loads orders + sync + unread count', () async {
      SharedPreferences.setMockInitialValues({});
      final cache = NotificationFeedCache();

      final order = OrderDto(
        id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
        status: 'Created',
        paymentStatus: 'Pending',
        fulfillmentStatus: 'PendingPayment',
        paymentProvider: 'PayPal',
        subtotal: 10,
        deliveryFee: 2,
        discount: 0,
        totalAmount: 12,
        currency: 'USD',
        createdAtUtc: DateTime.parse('2026-01-01T10:00:00Z'),
        paymentMethodSnapshot: PaymentMethodSnapshotDto(
          provider: 'PayPal',
          cardBrand: null,
          last4: null,
          expMonth: null,
          expYear: null,
          country: null,
          funding: null,
          providerPaymentIntentId: null,
          providerChargeId: null,
        ),
        items: [
          OrderItemDto(
            productId: 'p1',
            productName: 'Item 1',
            qty: 1,
            unitPrice: 10,
            lineTotal: 10,
            currency: 'USD',
          ),
        ],
      );
      final sync = DateTime.parse('2026-01-01T10:05:00Z');

      await cache.saveCachedOrders([order]);
      await cache.saveCachedSyncUtc(sync);
      await cache.saveCachedUnreadCount(7);

      final loadedOrders = await cache.loadCachedOrders();
      final loadedSync = await cache.loadCachedSyncUtc();
      final loadedUnread = await cache.loadCachedUnreadCount();

      expect(loadedOrders, hasLength(1));
      expect(loadedOrders.first.id, order.id);
      expect(loadedOrders.first.paymentStatus, 'Pending');
      expect(loadedSync?.toUtc().toIso8601String(), sync.toIso8601String());
      expect(loadedUnread, 7);
    });

    test('clear removes all cached values', () async {
      SharedPreferences.setMockInitialValues({});
      final cache = NotificationFeedCache();

      await cache.saveCachedUnreadCount(3);
      await cache.saveCachedSyncUtc(DateTime.parse('2026-01-01T10:00:00Z'));
      await cache.saveCachedOrders(const []);
      await cache.clear();

      expect(await cache.loadCachedUnreadCount(), 0);
      expect(await cache.loadCachedSyncUtc(), isNull);
      expect(await cache.loadCachedOrders(), isEmpty);
    });
  });
}
