import 'package:url_launcher/url_launcher.dart';

class CheckoutAttemptLog {
  final String provider;
  final String stage;
  final String detail;

  const CheckoutAttemptLog({
    required this.provider,
    required this.stage,
    required this.detail,
  });
}

class ExternalCheckoutLaunchResult {
  final bool launched;
  final String? providerUsed;
  final List<CheckoutAttemptLog> attempts;

  const ExternalCheckoutLaunchResult({
    required this.launched,
    required this.providerUsed,
    required this.attempts,
  });

  bool get usedFallback =>
      launched &&
      providerUsed != null &&
      attempts.isNotEmpty &&
      providerUsed != attempts.first.provider;
}

class CheckoutHardeningService {
  static const List<String> _externalProviders = <String>[
    'Stripe',
    'PayPal',
  ];

  static const List<String> mobileMoneyProviders = <String>[
    'AIRTEL',
    'ORANGE',
    'MPESA',
  ];

  static List<String> externalFallbackOrder(String preferredProvider) {
    final preferred = _normalizeExternalProvider(preferredProvider);
    final ordered = <String>[preferred];
    for (final provider in _externalProviders) {
      if (provider != preferred) {
        ordered.add(provider);
      }
    }
    return ordered;
  }

  static List<String> mobileMoneyFallbackOrder(String preferredProvider) {
    final normalized = preferredProvider.trim().toUpperCase();
    final base = mobileMoneyProviders.toList(growable: true);
    if (base.contains(normalized)) {
      base.remove(normalized);
      base.insert(0, normalized);
      return base;
    }
    return <String>[normalized, ...base];
  }

  static Future<ExternalCheckoutLaunchResult> openExternalCheckoutWithFallback({
    required String preferredProvider,
    required Future<String> Function(String provider) createCheckoutUrl,
    LaunchMode launchMode = LaunchMode.externalApplication,
  }) async {
    final attempts = <CheckoutAttemptLog>[];
    final providers = externalFallbackOrder(preferredProvider);

    for (final provider in providers) {
      try {
        final url = (await createCheckoutUrl(provider)).trim();
        if (url.isEmpty) {
          attempts.add(CheckoutAttemptLog(
            provider: provider,
            stage: 'create',
            detail: 'Empty checkout url.',
          ));
          continue;
        }

        final opened = await launchUrl(Uri.parse(url), mode: launchMode);
        if (opened) {
          attempts.add(CheckoutAttemptLog(
            provider: provider,
            stage: 'launch',
            detail: 'Opened.',
          ));
          return ExternalCheckoutLaunchResult(
            launched: true,
            providerUsed: provider,
            attempts: attempts,
          );
        }

        attempts.add(CheckoutAttemptLog(
          provider: provider,
          stage: 'launch',
          detail: 'launchUrl returned false.',
        ));
      } catch (error) {
        attempts.add(CheckoutAttemptLog(
          provider: provider,
          stage: 'exception',
          detail: error.toString(),
        ));
      }
    }

    return ExternalCheckoutLaunchResult(
      launched: false,
      providerUsed: null,
      attempts: attempts,
    );
  }

  static String summarizeAttempts(
    List<CheckoutAttemptLog> attempts, {
    int maxEntries = 2,
  }) {
    if (attempts.isEmpty) {
      return '';
    }
    final subset = attempts.take(maxEntries);
    return subset
        .map((entry) => '${entry.provider}/${entry.stage}: ${entry.detail}')
        .join(' | ');
  }

  static String _normalizeExternalProvider(String raw) {
    final normalized = raw.trim().toLowerCase();
    if (normalized.contains('stripe')) {
      return 'Stripe';
    }
    if (normalized.contains('paypal')) {
      return 'PayPal';
    }
    return 'PayPal';
  }
}
