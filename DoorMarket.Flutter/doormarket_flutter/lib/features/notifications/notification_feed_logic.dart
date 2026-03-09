import '../../core/models/orders.dart';

enum ClientNotificationKind {
  paymentFailed,
  paymentPending,
  paymentPaid,
  delivered,
  inProgress,
}

ClientNotificationKind resolveClientNotificationKind(OrderDto order) {
  final payment = order.paymentStatus.trim().toLowerCase();
  final fulfillment = order.fulfillmentStatus.trim().toLowerCase();

  if (payment == 'failed') return ClientNotificationKind.paymentFailed;
  if (payment == 'pending') return ClientNotificationKind.paymentPending;
  if (fulfillment.contains('deliver')) {
    return ClientNotificationKind.delivered;
  }
  if (payment == 'paid') return ClientNotificationKind.paymentPaid;
  return ClientNotificationKind.inProgress;
}

String clientNotificationKindKey(ClientNotificationKind kind) {
  return switch (kind) {
    ClientNotificationKind.paymentFailed => 'payment_failed',
    ClientNotificationKind.paymentPending => 'payment_pending',
    ClientNotificationKind.paymentPaid => 'payment_paid',
    ClientNotificationKind.delivered => 'delivered',
    ClientNotificationKind.inProgress => 'in_progress',
  };
}

String buildClientNotificationEventId(
    OrderDto order, ClientNotificationKind kind) {
  final kindKey = clientNotificationKindKey(kind);
  return '${order.id}|$kindKey|${order.createdAtUtc.toUtc().toIso8601String()}';
}
