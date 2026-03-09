import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'core/perf/auto_profile_runner.dart';
import 'core/providers.dart';
import 'core/router/app_router.dart';
import 'core/services/push_device_service.dart';
import 'core/theme/theme.dart';

void main() {
  runApp(const ProviderScope(child: DoorMarketApp()));
}

class DoorMarketApp extends ConsumerStatefulWidget {
  const DoorMarketApp({super.key});

  @override
  ConsumerState<DoorMarketApp> createState() => _DoorMarketAppState();
}

class _DoorMarketAppState extends ConsumerState<DoorMarketApp> {
  StreamSubscription<dynamic>? _authSubscription;
  bool _pushBootstrapStarted = false;
  PushDeviceService? _pushService;
  Timer? _pushBootstrapTimer;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (dmAutoProfileEnabled) {
        unawaited(AutoProfileRunner(ref).start());
        return;
      }

      _pushBootstrapTimer?.cancel();
      _pushBootstrapTimer = Timer(const Duration(milliseconds: 650), () {
        if (!mounted) {
          return;
        }
        unawaited(_bootstrapPushModule());
      });
    });
  }

  @override
  void dispose() {
    _authSubscription?.cancel();
    _pushBootstrapTimer?.cancel();
    final pushService = _pushService;
    if (pushService != null) {
      unawaited(pushService.dispose());
    }
    super.dispose();
  }

  Future<void> _bootstrapPushModule() async {
    if (_pushBootstrapStarted || !mounted) {
      return;
    }
    _pushBootstrapStarted = true;

    final pushService = (_pushService ?? ref.read(pushDeviceServiceProvider))!;
    _pushService = pushService;

    await pushService.initialize(onDeepLink: _openDeepLinkFromPush);
    final currentAuth = ref.read(authControllerProvider);
    if (!currentAuth.isLoading) {
      await pushService.syncWithAuth(currentAuth.isAuthenticated);
    }

    _authSubscription =
        ref.read(authControllerProvider.notifier).stream.listen((state) {
      if (state.isLoading) {
        return;
      }
      unawaited(pushService.syncWithAuth(state.isAuthenticated));
    });
  }

  void _openDeepLinkFromPush(String route) {
    if (!mounted) {
      return;
    }

    final normalized = route.trim();
    if (normalized.isEmpty) {
      return;
    }

    final target = normalized.startsWith('/') ? normalized : '/$normalized';
    ref.read(appRouterProvider).go(target);
  }

  @override
  Widget build(BuildContext context) {
    final router = ref.watch(appRouterProvider);
    final themeController = ref.watch(themeControllerProvider);
    final locale = ref.watch(localeControllerProvider);

    return MaterialApp.router(
      debugShowCheckedModeBanner: false,
      routerConfig: router,
      theme: DmTheme.light(),
      darkTheme: DmTheme.dark(),
      themeMode: themeController.themeMode,
      locale: locale,
      localizationsDelegates: const [
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      supportedLocales: const [
        Locale('fr'),
        Locale('en'),
      ],
    );
  }
}
