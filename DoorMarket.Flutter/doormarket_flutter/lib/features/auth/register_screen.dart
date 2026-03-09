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

class RegisterScreen extends ConsumerStatefulWidget {
  const RegisterScreen({super.key});

  @override
  ConsumerState<RegisterScreen> createState() => _RegisterScreenState();
}

class _RegisterScreenState extends ConsumerState<RegisterScreen> {
  final _emailController = TextEditingController();
  final _phoneController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmController = TextEditingController();
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
            child: Row(
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
                        Text(strings.registerTitle, style: Theme.of(context).textTheme.headlineSmall),
                        const SizedBox(height: 16),
                        TextField(
                          controller: _emailController,
                          decoration: InputDecoration(labelText: strings.emailLabel),
                        ),
                        const SizedBox(height: 12),
                        TextField(
                          controller: _phoneController,
                          decoration: InputDecoration(labelText: strings.phoneOptionalLabel),
                        ),
                        const SizedBox(height: 12),
                        TextField(
                          controller: _passwordController,
                          obscureText: true,
                          decoration: InputDecoration(labelText: strings.passwordLabel),
                        ),
                        const SizedBox(height: 12),
                        TextField(
                          controller: _confirmController,
                          obscureText: true,
                          decoration: InputDecoration(labelText: strings.confirmPasswordLabel),
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
                          label: _loading ? strings.registerLoading : strings.registerAction,
                          onPressed: _loading ? null : _onRegister,
                        ),
                        const SizedBox(height: 10),
                        DmSecondaryButton(
                          label: strings.alreadyHaveAccount,
                          onPressed: () => context.pop(),
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

  Future<void> _onRegister() async {
    final strings = ref.read(stringsProvider);
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      if (_passwordController.text != _confirmController.text) {
        setState(() => _error = strings.passwordMismatch);
        return;
      }
      await ref.read(authControllerProvider.notifier).register(
            RegisterRequest(
              email: _emailController.text.trim(),
              phone: _phoneController.text.trim().isEmpty ? null : _phoneController.text.trim(),
              password: _passwordController.text,
            ),
          );
      if (mounted) {
        context.push('/verify-email?email=${Uri.encodeComponent(_emailController.text.trim())}');
      }
    } catch (e) {
      setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }
}
