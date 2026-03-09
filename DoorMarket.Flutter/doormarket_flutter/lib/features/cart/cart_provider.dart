import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/cart_api.dart';
import '../../core/models/cart.dart';
import '../../core/providers.dart';

class CartState {
  static const _noChange = Object();

  final CartDto? cart;
  final CheckoutReadinessDto? readiness;
  final CartRecoveryStatusDto? recoveryStatus;
  final bool isLoading;
  final String? error;
  final String? readinessError;

  const CartState({
    this.cart,
    this.readiness,
    this.recoveryStatus,
    this.isLoading = false,
    this.error,
    this.readinessError,
  });

  CartState copyWith({
    Object? cart = _noChange,
    Object? readiness = _noChange,
    Object? recoveryStatus = _noChange,
    bool? isLoading,
    Object? error = _noChange,
    Object? readinessError = _noChange,
  }) {
    return CartState(
      cart: identical(cart, _noChange) ? this.cart : cart as CartDto?,
      readiness: identical(readiness, _noChange)
          ? this.readiness
          : readiness as CheckoutReadinessDto?,
      recoveryStatus: identical(recoveryStatus, _noChange)
          ? this.recoveryStatus
          : recoveryStatus as CartRecoveryStatusDto?,
      isLoading: isLoading ?? this.isLoading,
      error: identical(error, _noChange) ? this.error : error as String?,
      readinessError: identical(readinessError, _noChange)
          ? this.readinessError
          : readinessError as String?,
    );
  }
}

class CartController extends StateNotifier<CartState> {
  CartController(this._api) : super(const CartState());

  final CartApi _api;

  Future<void> load() async {
    state = state.copyWith(isLoading: true, error: null, readinessError: null);
    try {
      final cart = await _api.getMyCart();
      CheckoutReadinessDto? readiness;
      CartRecoveryStatusDto? recoveryStatus;
      String? readinessError;
      try {
        readiness = await _api.preCheckout(_buildPreCheckoutRequest());
      } catch (err) {
        readinessError = err.toString();
      }
      try {
        recoveryStatus = await _api.getRecoveryStatus(lookbackDays: 30);
      } catch (_) {
        recoveryStatus = null;
      }
      state = state.copyWith(
        cart: cart,
        readiness: readiness,
        recoveryStatus: recoveryStatus,
        readinessError: readinessError,
        isLoading: false,
      );
    } catch (err) {
      state = state.copyWith(
          isLoading: false,
          error: err.toString(),
          readiness: null,
          recoveryStatus: null);
    }
  }

  Future<void> addItem(String productId, int qty) async {
    state = state.copyWith(isLoading: true, error: null, readinessError: null);
    try {
      final cart = await _api
          .addItem(AddCartItemRequest(productId: productId, qty: qty));
      CheckoutReadinessDto? readiness;
      CartRecoveryStatusDto? recoveryStatus;
      String? readinessError;
      try {
        readiness = await _api.preCheckout(_buildPreCheckoutRequest());
      } catch (err) {
        readinessError = err.toString();
      }
      try {
        recoveryStatus = await _api.getRecoveryStatus(lookbackDays: 30);
      } catch (_) {
        recoveryStatus = null;
      }
      state = state.copyWith(
        cart: cart,
        readiness: readiness,
        recoveryStatus: recoveryStatus,
        readinessError: readinessError,
        isLoading: false,
      );
    } catch (err) {
      state = state.copyWith(
          isLoading: false,
          error: err.toString(),
          readiness: null,
          recoveryStatus: null);
    }
  }

  Future<void> updateItem(String itemId, int qty) async {
    state = state.copyWith(isLoading: true, error: null, readinessError: null);
    try {
      final cart =
          await _api.updateItem(itemId, UpdateCartItemRequest(qty: qty));
      CheckoutReadinessDto? readiness;
      CartRecoveryStatusDto? recoveryStatus;
      String? readinessError;
      try {
        readiness = await _api.preCheckout(_buildPreCheckoutRequest());
      } catch (err) {
        readinessError = err.toString();
      }
      try {
        recoveryStatus = await _api.getRecoveryStatus(lookbackDays: 30);
      } catch (_) {
        recoveryStatus = null;
      }
      state = state.copyWith(
        cart: cart,
        readiness: readiness,
        recoveryStatus: recoveryStatus,
        readinessError: readinessError,
        isLoading: false,
      );
    } catch (err) {
      state = state.copyWith(
          isLoading: false,
          error: err.toString(),
          readiness: null,
          recoveryStatus: null);
    }
  }

  Future<void> removeItem(String itemId) async {
    state = state.copyWith(isLoading: true, error: null, readinessError: null);
    try {
      final cart = await _api.removeItem(itemId);
      CheckoutReadinessDto? readiness;
      CartRecoveryStatusDto? recoveryStatus;
      String? readinessError;
      try {
        readiness = await _api.preCheckout(_buildPreCheckoutRequest());
      } catch (err) {
        readinessError = err.toString();
      }
      try {
        recoveryStatus = await _api.getRecoveryStatus(lookbackDays: 30);
      } catch (_) {
        recoveryStatus = null;
      }
      state = state.copyWith(
        cart: cart,
        readiness: readiness,
        recoveryStatus: recoveryStatus,
        readinessError: readinessError,
        isLoading: false,
      );
    } catch (err) {
      state = state.copyWith(
          isLoading: false,
          error: err.toString(),
          readiness: null,
          recoveryStatus: null);
    }
  }

  Future<void> clear() async {
    state = state.copyWith(isLoading: true, error: null, readinessError: null);
    try {
      await _api.clear();
      state = state.copyWith(
          cart: null, readiness: null, recoveryStatus: null, isLoading: false);
    } catch (err) {
      state = state.copyWith(isLoading: false, error: err.toString());
    }
  }

  Future<void> refreshReadiness({
    String? promoCode,
    String paymentProvider = 'PayPal',
    String? prepaidCardCode,
    bool requireDeliveryZone = false,
  }) async {
    if (state.cart == null) {
      state = state.copyWith(readiness: null, readinessError: null);
      return;
    }

    try {
      final readiness = await _api.preCheckout(
        _buildPreCheckoutRequest(
          promoCode: promoCode,
          paymentProvider: paymentProvider,
          prepaidCardCode: prepaidCardCode,
          requireDeliveryZone: requireDeliveryZone,
        ),
      );
      state = state.copyWith(readiness: readiness, readinessError: null);
    } catch (err) {
      state = state.copyWith(readiness: null, readinessError: err.toString());
    }
  }

  PreCheckoutRequest _buildPreCheckoutRequest({
    String? promoCode,
    String paymentProvider = 'PayPal',
    String? prepaidCardCode,
    bool requireDeliveryZone = false,
  }) {
    return PreCheckoutRequest(
      deliveryZoneId: null,
      promoCode: promoCode,
      paymentProvider: paymentProvider,
      prepaidCardCode: prepaidCardCode,
      requireDeliveryZone: requireDeliveryZone,
    );
  }
}

final cartControllerProvider =
    StateNotifierProvider<CartController, CartState>((ref) {
  return CartController(ref.read(cartApiProvider));
});
