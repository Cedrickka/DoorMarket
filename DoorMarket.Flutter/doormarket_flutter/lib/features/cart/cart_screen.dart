import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shimmer/shimmer.dart';
import 'package:go_router/go_router.dart';
import 'dart:async';

import '../../core/models/cart.dart';
import '../../core/providers.dart';
import '../../core/services/user_error_service.dart';
import '../../core/theme/colors.dart';
import '../../core/theme/radius.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_network_image.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../../core/widgets/dm_secondary_button.dart';
import 'cart_provider.dart';

class CartScreen extends ConsumerStatefulWidget {
  const CartScreen({super.key});

  @override
  ConsumerState<CartScreen> createState() => _CartScreenState();
}

class _CartScreenState extends ConsumerState<CartScreen>
    with AutomaticKeepAliveClientMixin {
  final _promoController = TextEditingController();
  bool _promoApplying = false;
  double _promoDiscount = 0;
  String? _promoMessage;

  static const _addressIssueCodes = <String>{
    'delivery_zone_required',
    'delivery_zone_missing',
    'invalid_delivery_zone',
  };
  static const _paymentIssueCodes = <String>{
    'prepaid_code_required',
  };
  static const _catalogIssueCodes = <String>{
    'empty_cart',
    'product_missing',
    'product_inactive',
    'stock_insufficient',
    'shop_unverified',
    'mixed_currency',
  };

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      unawaited(ref.read(cartControllerProvider.notifier).load());
    });
  }

  @override
  void dispose() {
    _promoController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final state = ref.watch(cartControllerProvider);
    final t = _tr;
    final cart = state.cart;
    final readiness = state.readiness;
    final recovery = state.recoveryStatus;
    final items = cart?.items ?? [];
    final subtotal =
        readiness?.subtotal ?? (cart == null ? 0.0 : cart.subtotal);
    final deliveryFee = readiness?.deliveryFee ?? 0.0;
    final discount = readiness?.discount ?? _promoDiscount;
    final total =
        readiness?.totalEstimate ?? ((subtotal - discount) + deliveryFee);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final iconBg = DmColors.iconBg(isDark);
    final infoSurface = DmColors.infoSurface(isDark);
    final errorSurface = DmColors.errorSurface(isDark);
    final errorText = isDark ? DmColors.errorDark : DmColors.errorLight;
    final currency = readiness?.currency ?? cart?.currency ?? 'USD';
    final checkoutBlocked = (readiness?.blockingIssues.length ?? 0) > 0;
    final issueActions = _resolveIssueActions(
      readiness,
      hasReadinessError: state.readinessError != null,
    );

    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: t(context, 'Panier', 'Cart'),
            subtitle:
                t(context, 'Vos articles selectionnes', 'Your selected items'),
            showBack: false,
          ),
          Expanded(
            child: RefreshIndicator(
              onRefresh: () => ref.read(cartControllerProvider.notifier).load(),
              child: SingleChildScrollView(
                key: const PageStorageKey<String>('cart-scroll'),
                physics: const AlwaysScrollableScrollPhysics(),
                padding: const EdgeInsets.fromLTRB(24, 24, 24, 140),
                child: state.isLoading && cart == null
                    ? _cartSkeleton(context)
                    : Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                            if (state.error != null)
                              DmCard(
                                backgroundColor: errorSurface,
                                borderColor: errorText.withAlpha(100),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Icon(Icons.error_outline,
                                            color: errorText, size: 18),
                                        const SizedBox(width: 8),
                                        Expanded(
                                          child: Text(
                                            UserErrorService.message(
                                              context,
                                              state.error!,
                                              fallbackFr:
                                                  'Le chargement du panier a echoue.',
                                              fallbackEn:
                                                  'Failed to load your cart.',
                                            ),
                                            style: TextStyle(color: errorText),
                                            maxLines: 3,
                                            overflow: TextOverflow.ellipsis,
                                          ),
                                        ),
                                      ],
                                    ),
                                    const SizedBox(height: 10),
                                    DmSecondaryButton(
                                      height: 40,
                                      width: 150,
                                      label:
                                          UserErrorService.retryLabel(context),
                                      onPressed: () => ref
                                          .read(cartControllerProvider.notifier)
                                          .load(),
                                    ),
                                  ],
                                ),
                              ),
                            DmCard(
                              child: Row(
                                children: [
                                  Container(
                                    height: 36,
                                    width: 36,
                                    decoration: BoxDecoration(
                                      color: iconBg,
                                      borderRadius: DmRadius.r12,
                                    ),
                                    child: Icon(
                                      Icons.shopping_cart,
                                      color: isDark
                                          ? DmColors.textPrimaryDark
                                          : DmColors.doorBlue,
                                      size: 18,
                                    ),
                                  ),
                                  const SizedBox(width: 12),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          t(
                                              context,
                                              '${items.length} article(s)',
                                              '${items.length} item(s)'),
                                          style: Theme.of(context)
                                              .textTheme
                                              .bodyMedium,
                                        ),
                                        Text(
                                            t(context, 'Total actuel',
                                                'Current total'),
                                            style: Theme.of(context)
                                                .textTheme
                                                .bodySmall),
                                      ],
                                    ),
                                  ),
                                  Text(
                                    '$currency ${subtotal.toStringAsFixed(2)}',
                                    style: Theme.of(context)
                                        .textTheme
                                        .headlineSmall
                                        ?.copyWith(color: DmColors.doorOrange),
                                  ),
                                ],
                              ),
                            ),
                            if (recovery != null && recovery.hasActiveReminder)
                              DmCard(
                                backgroundColor:
                                    DmColors.warningSurface(isDark),
                                borderColor:
                                    DmColors.warningLight.withAlpha(120),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      t(context, 'Reprendre votre panier',
                                          'Resume your cart'),
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodyMedium
                                          ?.copyWith(
                                              fontWeight: FontWeight.w700),
                                    ),
                                    const SizedBox(height: 6),
                                    Text(
                                      recovery.message,
                                      style:
                                          Theme.of(context).textTheme.bodySmall,
                                    ),
                                    const SizedBox(height: 6),
                                    Text(
                                      t(
                                        context,
                                        'Articles: ${recovery.itemCount} | Sous-total: ${recovery.currency} ${recovery.subtotal.toStringAsFixed(2)}',
                                        'Items: ${recovery.itemCount} | Subtotal: ${recovery.currency} ${recovery.subtotal.toStringAsFixed(2)}',
                                      ),
                                      style:
                                          Theme.of(context).textTheme.bodySmall,
                                    ),
                                    const SizedBox(height: 10),
                                    SizedBox(
                                      width: 220,
                                      child: DmPrimaryButton(
                                        height: 42,
                                        label: t(context, 'Reprendre checkout',
                                            'Resume checkout'),
                                        onPressed: _openCheckoutAndRefresh,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            if (items.isNotEmpty)
                              Wrap(
                                spacing: 8,
                                runSpacing: 8,
                                children: [
                                  _badge(
                                      t(context, '${items.length} articles',
                                          '${items.length} items'),
                                      DmColors.doorBlue),
                                  if (discount > 0)
                                    _badge('-${discount.toStringAsFixed(2)}',
                                        DmColors.doorOrange),
                                  _badge(
                                      t(context, 'Livraison rapide',
                                          'Fast delivery'),
                                      DmColors.doorOrange),
                                ],
                              ),
                            const SizedBox(height: 12),
                            if (items.isEmpty && total > 0)
                              DmCard(
                                backgroundColor:
                                    DmColors.warningSurface(isDark),
                                borderColor:
                                    DmColors.warningLight.withAlpha(120),
                                child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Icon(Icons.sync_problem_outlined,
                                        color: DmColors.warningLight, size: 18),
                                    const SizedBox(width: 8),
                                    Expanded(
                                      child: Text(
                                        t(
                                          context,
                                          'Le panier semble desynchronise. Actualisez pour recuperer vos articles.',
                                          'Your cart looks out of sync. Refresh to recover your items.',
                                        ),
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodySmall,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            if (items.isEmpty)
                              DmCard(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                        t(context, 'Votre panier est vide',
                                            'Your cart is empty'),
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodyMedium),
                                    const SizedBox(height: 6),
                                    Text(
                                        t(
                                            context,
                                            'Ajoutez des produits depuis le catalogue.',
                                            'Add products from the catalog.'),
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodySmall),
                                    const SizedBox(height: 12),
                                    DmSecondaryButton(
                                      height: 44,
                                      label: t(context, 'Explorer le catalogue',
                                          'Browse catalog'),
                                      onPressed: () =>
                                          context.go('/categories'),
                                    ),
                                  ],
                                ),
                              ),
                            if (state.readinessError != null)
                              DmCard(
                                backgroundColor:
                                    DmColors.warningSurface(isDark),
                                borderColor:
                                    DmColors.warningLight.withAlpha(120),
                                child: Row(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    const Icon(Icons.info_outline,
                                        color: DmColors.warningLight, size: 18),
                                    const SizedBox(width: 8),
                                    Expanded(
                                      child: Text(
                                        t(
                                          context,
                                          'Verification pre-checkout indisponible. Vous pouvez continuer, mais verifiez adresse et paiement au checkout.',
                                          'Pre-checkout verification is unavailable. You can continue, but verify address and payment at checkout.',
                                        ),
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodySmall,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            if (readiness != null &&
                                readiness.blockingIssues.isNotEmpty)
                              DmCard(
                                backgroundColor:
                                    DmColors.warningSurface(isDark),
                                borderColor:
                                    DmColors.warningLight.withAlpha(120),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      t(context, 'Checkout bloque',
                                          'Checkout blocked'),
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodyMedium
                                          ?.copyWith(
                                              fontWeight: FontWeight.w700),
                                    ),
                                    const SizedBox(height: 8),
                                    for (final issue
                                        in readiness.blockingIssues)
                                      Padding(
                                        padding:
                                            const EdgeInsets.only(bottom: 6),
                                        child: Text(
                                          '- ${issue.message}',
                                          style: Theme.of(context)
                                              .textTheme
                                              .bodySmall,
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                            if (readiness != null &&
                                readiness.blockingIssues.isEmpty)
                              DmCard(
                                backgroundColor:
                                    DmColors.successSurface(isDark),
                                borderColor:
                                    DmColors.successLight.withAlpha(100),
                                child: Row(
                                  children: [
                                    const Icon(Icons.check_circle_outline,
                                        color: DmColors.successLight, size: 18),
                                    const SizedBox(width: 8),
                                    Expanded(
                                      child: Text(
                                        t(context, 'Panier pret pour checkout.',
                                            'Cart ready for checkout.'),
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodySmall,
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            if (readiness != null &&
                                readiness.warnings.isNotEmpty)
                              DmCard(
                                backgroundColor: infoSurface,
                                borderColor: DmColors.border(isDark),
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      t(context, 'Points a verifier',
                                          'Things to verify'),
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodyMedium
                                          ?.copyWith(
                                              fontWeight: FontWeight.w700),
                                    ),
                                    const SizedBox(height: 8),
                                    for (final warning in readiness.warnings)
                                      Padding(
                                        padding:
                                            const EdgeInsets.only(bottom: 6),
                                        child: Text(
                                          '- ${warning.message}',
                                          style: Theme.of(context)
                                              .textTheme
                                              .bodySmall,
                                        ),
                                      ),
                                  ],
                                ),
                              ),
                            if (issueActions.isNotEmpty)
                              DmCard(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(
                                      t(context, 'Actions recommandees',
                                          'Recommended actions'),
                                      style: Theme.of(context)
                                          .textTheme
                                          .bodyMedium
                                          ?.copyWith(
                                              fontWeight: FontWeight.w700),
                                    ),
                                    const SizedBox(height: 10),
                                    Wrap(
                                      spacing: 8,
                                      runSpacing: 8,
                                      children: [
                                        for (final action in issueActions)
                                          SizedBox(
                                            width: 170,
                                            child: DmSecondaryButton(
                                              height: 42,
                                              label: _issueActionLabel(
                                                  context, action),
                                              onPressed: () =>
                                                  _handleIssueAction(action),
                                            ),
                                          ),
                                      ],
                                    ),
                                  ],
                                ),
                              ),
                            for (final item in items) _cartItem(context, item),
                            if (items.isNotEmpty) ...[
                              DmCard(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(t(context, 'Code promo', 'Promo code'),
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodyMedium),
                                    const SizedBox(height: 8),
                                    Row(
                                      children: [
                                        Expanded(
                                          child: TextField(
                                            controller: _promoController,
                                            decoration: InputDecoration(
                                                labelText: t(
                                                    context,
                                                    'Code promo',
                                                    'Promo code')),
                                          ),
                                        ),
                                        const SizedBox(width: 8),
                                        SizedBox(
                                          width: 116,
                                          child: DmSecondaryButton(
                                            height: 48,
                                            width: 116,
                                            label: _promoApplying
                                                ? '...'
                                                : t(context, 'Appliquer',
                                                    'Apply'),
                                            onPressed: _promoApplying
                                                ? null
                                                : _applyPromo,
                                          ),
                                        ),
                                      ],
                                    ),
                                    if (_promoMessage != null) ...[
                                      const SizedBox(height: 8),
                                      Text(_promoMessage!,
                                          style: Theme.of(context)
                                              .textTheme
                                              .bodySmall,
                                          maxLines: 2,
                                          overflow: TextOverflow.ellipsis),
                                    ],
                                  ],
                                ),
                              ),
                              DmCard(
                                child: Row(
                                  children: [
                                    Container(
                                      height: 38,
                                      width: 38,
                                      decoration: BoxDecoration(
                                        color: iconBg,
                                        borderRadius: DmRadius.r12,
                                      ),
                                      child: Icon(
                                        Icons.timer,
                                        color: isDark
                                            ? DmColors.textPrimaryDark
                                            : DmColors.doorBlue,
                                        size: 18,
                                      ),
                                    ),
                                    const SizedBox(width: 12),
                                    Expanded(
                                      child: Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          Text(
                                              t(
                                                  context,
                                                  'Estimation de livraison',
                                                  'Delivery estimate'),
                                              style: Theme.of(context)
                                                  .textTheme
                                                  .bodyMedium),
                                          const SizedBox(height: 2),
                                          Text(
                                              t(
                                                  context,
                                                  '30-45 minutes apres confirmation',
                                                  '30-45 minutes after confirmation'),
                                              style: Theme.of(context)
                                                  .textTheme
                                                  .bodySmall),
                                        ],
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              DmCard(
                                child: Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Text(t(context, 'Resume', 'Summary'),
                                        style: Theme.of(context)
                                            .textTheme
                                            .bodyMedium),
                                    const SizedBox(height: 8),
                                    _summaryRow(
                                        t(context, 'Sous-total', 'Subtotal'),
                                        '$currency ${subtotal.toStringAsFixed(2)}'),
                                    _summaryRow(
                                        t(context, 'Remise', 'Discount'),
                                        '$currency ${discount.toStringAsFixed(2)}'),
                                    _summaryRow(
                                        t(context, 'Livraison', 'Delivery'),
                                        '$currency ${deliveryFee.toStringAsFixed(2)}'),
                                    const Divider(),
                                    _summaryRow(t(context, 'Total', 'Total'),
                                        '$currency ${total.toStringAsFixed(2)}',
                                        isBold: true),
                                  ],
                                ),
                              ),
                              if (state.isLoading)
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                      horizontal: 12, vertical: 8),
                                  decoration: BoxDecoration(
                                    color: infoSurface,
                                    borderRadius: DmRadius.r12,
                                    border: Border.all(
                                        color: DmColors.border(isDark)),
                                  ),
                                  child: Row(
                                    children: [
                                      const SizedBox(
                                        height: 16,
                                        width: 16,
                                        child: CircularProgressIndicator(
                                            strokeWidth: 2),
                                      ),
                                      const SizedBox(width: 8),
                                      Text(
                                        t(context, 'Mise a jour du panier...',
                                            'Updating cart...'),
                                        style: TextStyle(
                                          color: isDark
                                              ? DmColors.textPrimaryDark
                                              : DmColors.doorBlue,
                                          fontSize: 12,
                                        ),
                                      ),
                                    ],
                                  ),
                                ),
                              const SizedBox(height: 40),
                            ],
                          ]),
              ),
            ),
          ),
        ],
      ),
      bottomNavigationBar: items.isNotEmpty
          ? _checkoutBar(context, isDark, currency, total, checkoutBlocked)
          : null,
    );
  }

  Widget _checkoutBar(
    BuildContext context,
    bool isDark,
    String currency,
    double total,
    bool checkoutBlocked,
  ) {
    return SafeArea(
      top: false,
      child: Container(
        padding:
            const EdgeInsets.only(left: 24, right: 24, top: 12, bottom: 16),
        decoration: BoxDecoration(
          color: isDark ? DmColors.surfaceDark : DmColors.surfaceLight,
          border: Border(
              top: BorderSide(
                  color: isDark ? DmColors.borderDark : DmColors.borderLight)),
          boxShadow: const [
            BoxShadow(
                color: Color(0x14000000), blurRadius: 16, offset: Offset(0, -6))
          ],
        ),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.end,
          children: [
            Expanded(
              flex: 4,
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    _tr(context, 'Total', 'Total'),
                    style: TextStyle(
                      color: DmColors.mutedText(isDark),
                      fontSize: 12,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '$currency ${total.toStringAsFixed(2)}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                        fontSize: 18,
                        fontWeight: FontWeight.w700,
                        color: DmColors.doorOrange),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              flex: 6,
              child: DmPrimaryButton(
                height: 50,
                label: _tr(context, 'Passer la commande', 'Checkout'),
                onPressed: checkoutBlocked ? null : _openCheckoutAndRefresh,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _cartItem(BuildContext context, CartItemDto item) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final totalText = '${item.currency} ${item.lineTotal.toStringAsFixed(2)}';
    return RepaintBoundary(
      child: DmCard(
        margin: const EdgeInsets.only(bottom: 10),
        padding: const EdgeInsets.all(10),
        boxShadowOverride: const [],
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                DmNetworkImage(
                  url: item.mainImageUrl,
                  height: 82,
                  width: 82,
                  borderRadius: DmRadius.r12,
                  cacheWidth: 240,
                  cacheHeight: 240,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        item.productName,
                        style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                              fontWeight: FontWeight.w600,
                            ),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                      const SizedBox(height: 4),
                      Text(
                        '${item.currency} ${item.unitPrice.toStringAsFixed(2)}',
                        style: Theme.of(context).textTheme.bodySmall?.copyWith(
                              color: DmColors.mutedText(isDark),
                            ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(width: 8),
                ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 112),
                  child: Text(
                    totalText,
                    style: Theme.of(context).textTheme.bodyMedium?.copyWith(
                          fontWeight: FontWeight.w700,
                        ),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    textAlign: TextAlign.right,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 10),
            Row(
              children: [
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
                  decoration: BoxDecoration(
                    color: DmColors.iconBg(isDark),
                    borderRadius: DmRadius.r12,
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      _qtyButton(
                        context,
                        Icons.remove,
                        () => _updateQty(item, item.qty - 1),
                      ),
                      Padding(
                        padding: const EdgeInsets.symmetric(horizontal: 10),
                        child: Text(
                          '${item.qty}',
                          style:
                              Theme.of(context).textTheme.bodyMedium?.copyWith(
                                    fontWeight: FontWeight.w700,
                                  ),
                        ),
                      ),
                      _qtyButton(
                        context,
                        Icons.add,
                        () => _updateQty(item, item.qty + 1),
                      ),
                    ],
                  ),
                ),
                const Spacer(),
                InkWell(
                  onTap: () => _removeItem(item),
                  borderRadius: DmRadius.r12,
                  child: Container(
                    padding:
                        const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                    decoration: BoxDecoration(
                      color: DmColors.errorSurface(isDark),
                      borderRadius: DmRadius.r12,
                      border: Border.all(
                        color: DmColors.errorLight.withAlpha(80),
                      ),
                    ),
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Icon(
                          Icons.delete_outline,
                          size: 14,
                          color:
                              isDark ? DmColors.errorDark : DmColors.errorLight,
                        ),
                        const SizedBox(width: 4),
                        Text(
                          _tr(context, 'Retirer', 'Remove'),
                          style: TextStyle(
                            color: isDark
                                ? DmColors.errorDark
                                : DmColors.errorLight,
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }

  Widget _summaryRow(String label, String value, {bool isBold = false}) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Text(label),
          Text(value,
              style: TextStyle(
                  fontWeight: isBold ? FontWeight.w700 : FontWeight.w500)),
        ],
      ),
    );
  }

  Widget _qtyButton(BuildContext context, IconData icon, VoidCallback onTap) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return InkWell(
      onTap: onTap,
      borderRadius: BorderRadius.circular(10),
      child: Container(
        height: 28,
        width: 28,
        decoration: BoxDecoration(
          color: isDark ? DmColors.surfaceDark : DmColors.surfaceLight,
          borderRadius: BorderRadius.circular(10),
          border: Border.all(color: DmColors.border(isDark)),
        ),
        child: Icon(
          icon,
          size: 16,
          color: DmColors.iconFg(isDark),
        ),
      ),
    );
  }

  Widget _badge(String text, Color color) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: color.withAlpha(31),
        borderRadius: DmRadius.r12,
      ),
      child: Text(text,
          style: TextStyle(
              color: color, fontSize: 12, fontWeight: FontWeight.w600)),
    );
  }

  Widget _cartSkeleton(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final base = isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight;
    final highlight = isDark ? DmColors.surfaceDark : DmColors.surfaceLight;

    Widget line(double width, double height) {
      return Container(
        width: width,
        height: height,
        decoration: BoxDecoration(
          color: base,
          borderRadius: DmRadius.r12,
        ),
      );
    }

    Widget cardSkeleton() {
      return DmCard(
        margin: const EdgeInsets.only(bottom: 10),
        boxShadowOverride: const [],
        child: Row(
          children: [
            Container(
              height: 84,
              width: 84,
              decoration: BoxDecoration(
                color: base,
                borderRadius: DmRadius.r12,
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  line(double.infinity, 12),
                  const SizedBox(height: 8),
                  line(120, 10),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      line(26, 26),
                      const SizedBox(width: 6),
                      line(20, 12),
                      const SizedBox(width: 6),
                      line(26, 26),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(width: 12),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                line(60, 12),
                const SizedBox(height: 10),
                line(54, 20),
              ],
            ),
          ],
        ),
      );
    }

    return Shimmer.fromColors(
      baseColor: base,
      highlightColor: highlight,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          DmCard(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                line(140, 12),
                const SizedBox(height: 8),
                line(200, 10),
              ],
            ),
          ),
          cardSkeleton(),
          cardSkeleton(),
          cardSkeleton(),
        ],
      ),
    );
  }

  Future<void> _updateQty(CartItemDto item, int qty) async {
    if (qty < 1) return;
    await ref.read(cartControllerProvider.notifier).updateItem(item.id, qty);
  }

  Future<void> _removeItem(CartItemDto item) async {
    await ref.read(cartControllerProvider.notifier).removeItem(item.id);
  }

  Future<void> _applyPromo() async {
    final code = _promoController.text.trim();
    if (code.isEmpty) return;
    setState(() => _promoApplying = true);
    try {
      final quote = await ref
          .read(cartApiProvider)
          .applyPromo(ApplyPromoRequest(code: code));
      await ref.read(cartControllerProvider.notifier).refreshReadiness(
            promoCode: quote.applied ? code : null,
          );
      setState(() {
        _promoDiscount = quote.discount;
        _promoMessage = quote.message;
      });
    } catch (e) {
      await ref.read(cartControllerProvider.notifier).refreshReadiness(
            promoCode: null,
          );
      setState(() {
        _promoMessage = UserErrorService.message(
          context,
          e,
          fallbackFr: 'Le code promo est indisponible pour le moment.',
          fallbackEn: 'Promo code validation is currently unavailable.',
        );
      });
    } finally {
      if (mounted) setState(() => _promoApplying = false);
    }
  }

  Future<void> _openCheckoutAndRefresh() async {
    await context.push('/checkout');
    if (!mounted) return;
    await ref.read(cartControllerProvider.notifier).load();
  }

  Future<void> _handleIssueAction(_IssueActionType action) async {
    switch (action) {
      case _IssueActionType.addresses:
        await _openAndRefresh('/addresses');
        break;
      case _IssueActionType.payments:
        await _openAndRefresh('/payments');
        break;
      case _IssueActionType.catalog:
        if (!mounted) return;
        context.go('/categories');
        break;
      case _IssueActionType.retry:
        await ref.read(cartControllerProvider.notifier).load();
        break;
    }
  }

  Future<void> _openAndRefresh(String route) async {
    await context.push(route);
    if (!mounted) return;
    await ref.read(cartControllerProvider.notifier).load();
  }

  List<_IssueActionType> _resolveIssueActions(
    CheckoutReadinessDto? readiness, {
    required bool hasReadinessError,
  }) {
    final actions = <_IssueActionType>[];
    if (hasReadinessError) {
      actions.add(_IssueActionType.retry);
    }

    if (readiness == null) {
      return actions;
    }

    final codes = <String>{
      ...readiness.blockingIssues.map((e) => e.code.toLowerCase()),
      ...readiness.warnings.map((e) => e.code.toLowerCase()),
    };

    if (codes.any(_addressIssueCodes.contains)) {
      actions.add(_IssueActionType.addresses);
    }
    if (codes.any(_paymentIssueCodes.contains)) {
      actions.add(_IssueActionType.payments);
    }
    if (codes.any(_catalogIssueCodes.contains)) {
      actions.add(_IssueActionType.catalog);
    }

    const knownCodes = {
      ..._addressIssueCodes,
      ..._paymentIssueCodes,
      ..._catalogIssueCodes,
      'promo_not_applied',
    };
    if (codes.any((code) => !knownCodes.contains(code))) {
      actions.add(_IssueActionType.retry);
    }

    return actions.toSet().toList();
  }

  String _issueActionLabel(BuildContext context, _IssueActionType action) {
    switch (action) {
      case _IssueActionType.addresses:
        return _tr(context, 'Gerer adresses', 'Manage addresses');
      case _IssueActionType.payments:
        return _tr(context, 'Moyens de paiement', 'Payment methods');
      case _IssueActionType.catalog:
        return _tr(context, 'Voir catalogue', 'View catalog');
      case _IssueActionType.retry:
        return _tr(context, 'Reessayer verification', 'Retry validation');
    }
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }

  @override
  bool get wantKeepAlive => true;
}

enum _IssueActionType { addresses, payments, catalog, retry }
