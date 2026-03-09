import 'package:dio/dio.dart';
import 'package:doormarket_flutter/core/api/cart_api.dart';
import 'package:doormarket_flutter/core/api/orders_api.dart';
import 'package:doormarket_flutter/core/api/payments_api.dart';
import 'package:doormarket_flutter/core/models/cart.dart';
import 'package:doormarket_flutter/core/models/orders.dart';
import 'package:doormarket_flutter/core/network/api_client.dart';
import 'package:doormarket_flutter/core/storage/token_store.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  group('DM60-MOB-03 smoke e2e mobile commande/paiement/reprise', () {
    test('checkout mobile money retry ends with paid order and cleared cart',
        () async {
      SharedPreferences.setMockInitialValues({});
      final backend = _MobileSmokeBackend();
      final client = _buildApiClient(backend);

      final cartApi = CartApi(client);
      final ordersApi = OrdersApi(client);
      final paymentsApi = PaymentsApi(client);

      final cartBefore = await cartApi.getMyCart();
      expect(cartBefore.items.length, 2);
      expect(cartBefore.subtotal, 40);

      final readiness = await cartApi.preCheckout(
        PreCheckoutRequest(
          deliveryZoneId: backend.zoneId,
          paymentProvider: 'MobileMoney',
          requireDeliveryZone: true,
        ),
      );
      expect(readiness.isReady, isTrue);
      expect(readiness.paymentProvider, 'MobileMoney');
      expect(readiness.totalEstimate, 45);

      final createdOrder = await ordersApi.checkout(
        CheckoutRequest(
          deliveryName: 'Client DM60',
          deliveryPhone: '+243990000001',
          deliveryLine1: 'Avenue Test 1',
          deliveryCity: 'Kinshasa',
          deliveryCountry: 'CD',
          deliveryZoneId: backend.zoneId,
          deliveryNotes: 'Smoke DM60 mobile',
          promoCode: null,
          paymentProvider: 'MobileMoney',
          prepaidCardCode: null,
        ),
      );
      expect(createdOrder.id, backend.orderId);
      expect(createdOrder.paymentStatus, 'Unpaid');

      final recoveryBefore = await cartApi.getRecoveryStatus(lookbackDays: 30);
      expect(recoveryBefore.hasActiveReminder, isTrue);
      expect(recoveryBefore.checkoutPath, '/checkout');

      final initiated = await paymentsApi.initiateMobileMoney(
        orderId: backend.orderId,
        provider: 'AIRTEL',
        phoneNumber: '+243990000001',
      );
      expect(initiated.provider, 'AIRTEL');
      expect(initiated.status, 'PENDING');
      expect(initiated.transactionId, backend.transactionId);

      final firstConfirm = await paymentsApi.confirmMobileMoney(
        orderId: backend.orderId,
        provider: 'AIRTEL',
        transactionId: backend.transactionId,
      );
      expect(firstConfirm.paid, isFalse);
      expect(firstConfirm.paymentStatus, 'Failed');

      final orderAfterFailure = await ordersApi.getById(backend.orderId);
      expect(orderAfterFailure.paymentStatus, 'Failed');

      final secondConfirm = await paymentsApi.confirmMobileMoney(
        orderId: backend.orderId,
        provider: 'AIRTEL',
        transactionId: backend.transactionId,
      );
      expect(secondConfirm.paid, isTrue);
      expect(secondConfirm.paymentStatus, 'Paid');

      final paidOrder = await ordersApi.getById(backend.orderId);
      expect(paidOrder.paymentStatus, 'Paid');
      expect(paidOrder.status, 'Paid');
      expect(paidOrder.fulfillmentStatus, 'PaidPending');

      final cartAfter = await cartApi.getMyCart();
      expect(cartAfter.items, isEmpty);
      expect(cartAfter.subtotal, 0);

      final recoveryAfter = await cartApi.getRecoveryStatus(lookbackDays: 30);
      expect(recoveryAfter.hasActiveReminder, isFalse);
      expect(recoveryAfter.hasRecentOrder, isTrue);
    });
  });
}

ApiClient _buildApiClient(_MobileSmokeBackend backend) {
  final client = ApiClient(
    baseUrl: 'https://mobile-smoke.local/',
    tokenStore: TokenStore(),
    onRefresh: () async => false,
    onLogout: () async {},
    dio: Dio(),
  );

  client.raw.interceptors.add(
    InterceptorsWrapper(
      onRequest: (options, handler) {
        try {
          final result = backend.handle(options);
          handler.resolve(Response<dynamic>(
            requestOptions: options,
            statusCode: result.statusCode,
            data: result.body,
          ));
        } on _BackendHttpException catch (error) {
          handler.reject(
            DioException(
              requestOptions: options,
              response: Response<dynamic>(
                requestOptions: options,
                statusCode: error.statusCode,
                data: error.body,
              ),
              type: DioExceptionType.badResponse,
              error: error.body,
            ),
          );
        } catch (error) {
          handler.reject(
            DioException(
              requestOptions: options,
              type: DioExceptionType.unknown,
              error: error,
            ),
          );
        }
      },
    ),
  );

  return client;
}

class _MobileSmokeBackend {
  final String cartId = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
  final String zoneId = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
  final String orderId = '11111111-1111-1111-1111-111111111111';
  final String transactionId = 'MM-SMOKE-1';

  bool _orderCreated = false;
  bool _orderPaid = false;
  String _paymentStatus = 'Unpaid';
  int _confirmAttempts = 0;

  _BackendResult handle(RequestOptions options) {
    final method = options.method.toUpperCase();
    final path = _normalizePath(options.path);
    final body = _asMap(options.data);
    final query = _mergeQuery(options.path, options.queryParameters);

    if (method == 'GET' && path == 'api/cart') {
      return _BackendResult(200, _cartJson());
    }

    if (method == 'POST' && path == 'api/cart/pre-checkout') {
      return _BackendResult(200, _preCheckoutJson(body));
    }

    if (method == 'GET' && path == 'api/cart/recovery-status') {
      return _BackendResult(200, _recoveryStatusJson());
    }

    if (method == 'POST' && path == 'api/orders/checkout') {
      _orderCreated = true;
      _paymentStatus = 'Unpaid';
      return _BackendResult(200, _orderJson());
    }

    if (method == 'GET' && path == 'api/orders/$orderId') {
      if (!_orderCreated) {
        throw _BackendHttpException(404, {'error': 'Order not found'});
      }
      return _BackendResult(200, _orderJson());
    }

    if (method == 'POST' && path == 'api/payments/mobile-money/initiate') {
      if (!_orderCreated) {
        throw _BackendHttpException(400, {'error': 'Order is required'});
      }
      if ((body['orderId'] ?? '') != orderId) {
        throw _BackendHttpException(400, {'error': 'Invalid orderId'});
      }

      return _BackendResult(200, {
        'provider': (body['provider'] ?? 'AIRTEL').toString().toUpperCase(),
        'transactionId': transactionId,
        'status': 'PENDING',
        'checkoutUrl': null,
        'message': 'Awaiting customer confirmation',
      });
    }

    if (method == 'POST' && path == 'api/payments/mobile-money/confirm') {
      if (!_orderCreated) {
        throw _BackendHttpException(400, {'error': 'Order is required'});
      }
      if ((body['orderId'] ?? '') != orderId ||
          (body['transactionId'] ?? '') != transactionId) {
        throw _BackendHttpException(400, {'error': 'Invalid payload'});
      }

      _confirmAttempts++;
      if (_confirmAttempts == 1) {
        _paymentStatus = 'Failed';
        return _BackendResult(200, {
          'paid': false,
          'orderId': orderId,
          'paymentStatus': 'Failed',
          'provider': (body['provider'] ?? 'AIRTEL').toString().toUpperCase(),
          'transactionId': transactionId,
          'rawStatus': 'DECLINED',
          'message': 'Initial confirmation failed',
        });
      }

      _orderPaid = true;
      _paymentStatus = 'Paid';
      return _BackendResult(200, {
        'paid': true,
        'orderId': orderId,
        'paymentStatus': 'Paid',
        'provider': (body['provider'] ?? 'AIRTEL').toString().toUpperCase(),
        'transactionId': transactionId,
        'rawStatus': 'SUCCESS',
        'message': 'Payment confirmed',
      });
    }

    if (method == 'POST' && path == 'api/payments/paypal/create-checkout') {
      if (!_orderCreated) {
        throw _BackendHttpException(400, {'error': 'Order is required'});
      }
      if ((query['orderId'] ?? '') != orderId) {
        throw _BackendHttpException(400, {'error': 'Invalid orderId'});
      }
      return _BackendResult(200, {
        'url': 'https://paypal.example/checkout/$orderId',
        'payPalOrderId': 'PP-SMOKE-1',
      });
    }

    throw _BackendHttpException(404, {
      'error': 'Unhandled route',
      'method': method,
      'path': path,
    });
  }

  Map<String, dynamic> _cartJson() {
    if (_orderPaid) {
      return {
        'cartId': cartId,
        'items': <Map<String, dynamic>>[],
        'subtotal': 0.0,
        'currency': 'USD',
      };
    }

    return {
      'cartId': cartId,
      'items': <Map<String, dynamic>>[
        {
          'id': 'cccccccc-cccc-cccc-cccc-ccccccccccc1',
          'productId': 'dddddddd-dddd-dddd-dddd-dddddddddd01',
          'shopId': 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1',
          'productName': 'Produit Smoke 1',
          'unitPrice': 20.0,
          'qty': 1,
          'lineTotal': 20.0,
          'currency': 'USD',
          'mainImageUrl': null,
        },
        {
          'id': 'cccccccc-cccc-cccc-cccc-ccccccccccc2',
          'productId': 'dddddddd-dddd-dddd-dddd-dddddddddd02',
          'shopId': 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeee1',
          'productName': 'Produit Smoke 2',
          'unitPrice': 20.0,
          'qty': 1,
          'lineTotal': 20.0,
          'currency': 'USD',
          'mainImageUrl': null,
        },
      ],
      'subtotal': 40.0,
      'currency': 'USD',
    };
  }

  Map<String, dynamic> _preCheckoutJson(Map<String, dynamic> request) {
    final provider = (request['paymentProvider'] ?? 'PayPal').toString();
    final itemCount = _orderPaid ? 0 : 2;
    final subtotal = _orderPaid ? 0.0 : 40.0;
    final delivery = _orderPaid ? 0.0 : 5.0;
    return {
      'isReady': !_orderPaid,
      'currency': 'USD',
      'itemCount': itemCount,
      'subtotal': subtotal,
      'discount': 0.0,
      'deliveryFee': delivery,
      'totalEstimate': subtotal + delivery,
      'paymentProvider': provider,
      'blockingIssues': <Map<String, dynamic>>[],
      'warnings': <Map<String, dynamic>>[],
    };
  }

  Map<String, dynamic> _recoveryStatusJson() {
    final hasReminder = _orderCreated && !_orderPaid;
    return {
      'hasActiveReminder': hasReminder,
      'message':
          hasReminder ? 'Resume your checkout' : 'No active recovery reminder',
      'lastDetectedAtUtc':
          DateTime.parse('2026-03-05T20:00:00Z').toIso8601String(),
      'reminderStatus': hasReminder ? 'Sent' : 'Resolved',
      'experimentGroup': hasReminder ? 'A' : null,
      'itemCount': _orderPaid ? 0 : 2,
      'subtotal': _orderPaid ? 0.0 : 40.0,
      'currency': 'USD',
      'hasRecentOrder': _orderPaid,
      'checkoutPath': '/checkout',
    };
  }

  Map<String, dynamic> _orderJson() {
    final paymentStatus = _paymentStatus;
    final status = _orderPaid ? 'Paid' : 'Created';
    return {
      'id': orderId,
      'status': status,
      'paymentStatus': paymentStatus,
      'fulfillmentStatus': _orderPaid ? 'PaidPending' : 'PendingPayment',
      'paymentProvider': 'MobileMoney',
      'subtotal': 40.0,
      'deliveryFee': 5.0,
      'discount': 0.0,
      'totalAmount': 45.0,
      'currency': 'USD',
      'createdAtUtc': DateTime.parse('2026-03-05T19:00:00Z').toIso8601String(),
      'paymentMethodSnapshot': _orderPaid
          ? {
              'provider': 'MobileMoney:AIRTEL',
              'cardBrand': 'AIRTEL',
              'last4': null,
              'expMonth': null,
              'expYear': null,
              'country': null,
              'funding': 'mobile_money',
              'providerPaymentIntentId': transactionId,
              'providerChargeId': null,
            }
          : null,
      'items': <Map<String, dynamic>>[
        {
          'productId': 'dddddddd-dddd-dddd-dddd-dddddddddd01',
          'productName': 'Produit Smoke 1',
          'qty': 1,
          'unitPrice': 20.0,
          'lineTotal': 20.0,
          'currency': 'USD',
        },
        {
          'productId': 'dddddddd-dddd-dddd-dddd-dddddddddd02',
          'productName': 'Produit Smoke 2',
          'qty': 1,
          'unitPrice': 20.0,
          'lineTotal': 20.0,
          'currency': 'USD',
        },
      ],
    };
  }

  static String _normalizePath(String rawPath) {
    final source = rawPath.startsWith('http')
        ? Uri.parse(rawPath).path
        : Uri.parse('https://fake.local/$rawPath').path;
    return source.startsWith('/') ? source.substring(1) : source;
  }

  static Map<String, dynamic> _asMap(dynamic value) {
    if (value is Map<String, dynamic>) {
      return value;
    }

    if (value is Map) {
      return value.map((key, val) => MapEntry('$key', val));
    }

    return <String, dynamic>{};
  }

  static Map<String, String> _mergeQuery(
      String rawPath, Map<String, dynamic> queryParameters) {
    final uri = Uri.parse(
      rawPath.startsWith('http') ? rawPath : 'https://fake.local/$rawPath',
    );
    final merged = <String, String>{...uri.queryParameters};
    for (final entry in queryParameters.entries) {
      if (entry.value == null) {
        continue;
      }
      merged[entry.key] = entry.value.toString();
    }
    return merged;
  }
}

class _BackendResult {
  const _BackendResult(this.statusCode, this.body);

  final int statusCode;
  final dynamic body;
}

class _BackendHttpException implements Exception {
  const _BackendHttpException(this.statusCode, this.body);

  final int statusCode;
  final dynamic body;
}
