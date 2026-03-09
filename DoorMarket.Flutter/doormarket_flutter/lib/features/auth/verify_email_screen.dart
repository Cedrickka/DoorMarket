import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:go_router/go_router.dart';

import '../../core/models/auth.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/widgets/dm_primary_button.dart';

class VerifyEmailScreen extends ConsumerStatefulWidget {
  final String email;

  const VerifyEmailScreen({super.key, required this.email});

  @override
  ConsumerState<VerifyEmailScreen> createState() => _VerifyEmailScreenState();
}

class _VerifyEmailScreenState extends ConsumerState<VerifyEmailScreen> {
  final _codeController = TextEditingController();
  bool _loading = false;
  bool _resending = false;
  String? _error;
  Timer? _timer;
  int _remaining = 180;

  @override
  void initState() {
    super.initState();
    _startTimer();
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final strings = ref.watch(stringsProvider);
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final minutes = (_remaining ~/ 60).toString().padLeft(2, '0');
    final seconds = (_remaining % 60).toString().padLeft(2, '0');

    return Scaffold(
      appBar: AppBar(title: Text(strings.verifyTitle)),
      body: Stack(
        children: [
          SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              children: [
                Text(strings.verifySentTo(widget.email)),
                const SizedBox(height: 8),
                Text(strings.verifyExpires(minutes, seconds)),
                const SizedBox(height: 12),
                TextField(
                  controller: _codeController,
                  decoration: InputDecoration(labelText: strings.otpLabel),
                ),
                const SizedBox(height: 16),
                if (_error != null)
                  Text(
                    _error!,
                    style: TextStyle(
                      color: isDark ? DmColors.errorDark : DmColors.errorLight,
                    ),
                  ),
                const SizedBox(height: 8),
                DmPrimaryButton(
                  label: _loading ? strings.verifyLoading : strings.verifyAction,
                  onPressed: _loading ? null : _verify,
                ),
                const SizedBox(height: 12),
                TextButton(
                  onPressed: _resending ? null : _resend,
                  child: Text(_resending ? strings.resendLoading : strings.resendAction),
                ),
              ],
            ),
          ),
          if (_loading || _resending) _loadingOverlay(),
        ],
      ),
    );
  }

  Widget _loadingOverlay() {
    return Positioned.fill(
      child: AbsorbPointer(
        absorbing: true,
        child: Container(
          color: Colors.black.withAlpha(40),
          alignment: Alignment.center,
          child: const SizedBox(
            height: 48,
            width: 48,
            child: CircularProgressIndicator(strokeWidth: 3),
          ),
        ),
      ),
    );
  }

  void _startTimer() {
    _timer?.cancel();
    _remaining = 180;
    _timer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (!mounted) return;
      if (_remaining <= 0) {
        timer.cancel();
        setState(() {});
        return;
      }
      setState(() => _remaining -= 1);
    });
  }

  Future<void> _verify() async {
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).verifyEmail(
            VerifyEmailRequest(email: widget.email, code: _codeController.text.trim()),
          );
      if (mounted) context.go('/login');
    } catch (e) {
      final message = e.toString();
      setState(() => _error = message);
      await _speak(message);
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  Future<void> _resend() async {
    setState(() {
      _resending = true;
      _error = null;
    });
    try {
      await ref.read(authControllerProvider.notifier).resendVerification(
            ResendVerificationRequest(email: widget.email),
          );
      _startTimer();
      final strings = ref.read(stringsProvider);
      await _speak(strings.verifySentTo(widget.email));
    } catch (e) {
      final message = e.toString();
      setState(() => _error = message);
      await _speak(message);
    } finally {
      if (mounted) setState(() => _resending = false);
    }
  }

  Future<void> _speak(String message) async {
    if (!mounted) return;
    final locale = Localizations.localeOf(context);
    await ref.read(ttsProvider).speak(message, locale);
  }
}
