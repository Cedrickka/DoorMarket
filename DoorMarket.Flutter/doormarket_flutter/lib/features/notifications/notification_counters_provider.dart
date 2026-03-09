import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/providers.dart';
import 'notification_feed_logic.dart';

final notificationRefreshTickProvider = StreamProvider<int>((ref) async* {
  yield 0;
  var tick = 1;
  while (true) {
    await Future<void>.delayed(const Duration(seconds: 45));
    yield tick++;
  }
});

final unreadNotificationsCountProvider = FutureProvider<int>((ref) async {
  ref.watch(notificationRefreshTickProvider);
  final prefs = ref.watch(notificationPreferencesProvider);
  final cache = ref.read(notificationFeedCacheProvider);
  if (!prefs.enabled) return 0;

  try {
    final api = ref.read(ordersApiProvider);
    final page = await api.getMine(page: 1, pageSize: 25);

    var unread = 0;
    for (final order in page.items) {
      final kind = resolveClientNotificationKind(order);
      final kindKey = clientNotificationKindKey(kind);
      if (!prefs.allowEventKind(kindKey)) {
        continue;
      }

      final eventId = buildClientNotificationEventId(order, kind);
      if (!prefs.isRead(eventId)) {
        unread++;
      }
    }

    await cache.saveCachedUnreadCount(unread);
    await cache.saveCachedOrders(page.items);
    await cache.saveCachedSyncUtc(DateTime.now().toUtc());
    return unread;
  } catch (_) {
    return cache.loadCachedUnreadCount();
  }
});
