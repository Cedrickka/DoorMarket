import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/auth.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/widgets/dm_brand_mark.dart';
import '../../core/widgets/dm_primary_button.dart';
import '../../core/widgets/dm_secondary_button.dart';
import '../../core/widgets/dm_language_badge.dart';
import '../../core/widgets/dm_card.dart';

class LoginScreen extends ConsumerStatefulWidget {
  const LoginScreen({super.key});

  @override
  ConsumerState<LoginScreen> createState() => _LoginScreenState();
}

class _LoginScreenState extends ConsumerState<LoginScreen> {
  final _loginController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _loading = false;
  String? _error;

  @override
  Widget build(BuildContext context) {
    final strings = ref.watch(stringsProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Scaffold(
      body: Column(
        children: [
          Container(
            padding: EdgeInsets.only(
              left: 24,
              right: 24,
              top: MediaQuery.of(context).padding.top + 24,
              bottom: 24,
            ),
            decoration: const BoxDecoration(
              gradient: LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  Color(0xFF002D5E),
                  Color(0xFF0C3F78),
                ],
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Expanded(
                      child: Row(
                        children: [
                          const DmBrandMark(size: 30, monochromeWhite: true),
                          const SizedBox(width: 10),
                          const Flexible(
                            child: Text(
                              'DoorMarket',
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                color: Colors.white,
                                fontSize: 26,
                                fontWeight: FontWeight.w800,
                                height: 1,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(width: 10),
                    const DmLanguageBadge(),
                  ],
                ),
                const SizedBox(height: 12),
                Text(
                  strings.welcome,
                  style: const TextStyle(color: Colors.white, fontSize: 14, fontWeight: FontWeight.w500),
                ),
              ],
            ),
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Column(
                children: [
                  DmCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(strings.loginTitle, style: Theme.of(context).textTheme.headlineSmall),
                        const SizedBox(height: 16),
                        TextField(
                          controller: _loginController,
                          decoration: InputDecoration(labelText: strings.loginSubtitle),
                        ),
                        const SizedBox(height: 12),
                        TextField(
                          controller: _passwordController,
                          obscureText: true,
                          decoration: InputDecoration(labelText: strings.passwordLabel),
                        ),
                        if (_error != null) ...[
                          const SizedBox(height: 12),
                          Text(
                            _error!,
                            style: TextStyle(
                              color:
                                  isDark ? DmColors.errorDark : DmColors.errorLight,
                            ),
                          ),
                        ],
                        const SizedBox(height: 16),
                        DmPrimaryButton(
                          label: _loading ? strings.loginLoading : strings.loginAction,
                          onPressed: _loading ? null : _onLogin,
                        ),
                        const SizedBox(height: 10),
                        DmSecondaryButton(
                          label: strings.createAccount,
                          onPressed: () => context.push('/register'),
                        ),
                      ],
                    ),
                  ),
                  if (_loading) ...[
                    const SizedBox(height: 16),
                    const CircularProgressIndicator(),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  Future<void> _onLogin() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final result = await ref.read(authControllerProvider.notifier).login(
            LoginRequest(
              login: _loginController.text.trim(),
              password: _passwordController.text,
            ),
          );
      if (result.otpRequired) {
        final strings = ref.read(stringsProvider);
        setState(() => _error = strings.otpLoginDisabled);
        await _speak(strings.otpLoginDisabled);
        return;
      }
      if (mounted) context.go('/');
    } catch (e) {
      final message = e.toString();
      setState(() => _error = message);
      await _speak(message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _speak(String message) async {
    if (!mounted) return;
    final locale = Localizations.localeOf(context);
    await ref.read(ttsProvider).speak(message, locale);
  }
}
