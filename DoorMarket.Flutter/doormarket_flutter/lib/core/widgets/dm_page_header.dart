import 'package:flutter/material.dart';

import '../theme/colors.dart';
import '../theme/radius.dart';

class DmPageHeader extends StatelessWidget {
  final String title;
  final String subtitle;
  final Color backgroundColor;
  final Color titleColor;
  final Color subtitleColor;
  final bool showBack;
  final VoidCallback? onBack;
  final Widget? trailing;

  const DmPageHeader({
    super.key,
    required this.title,
    required this.subtitle,
    this.backgroundColor = const Color(0xFF002D5E),
    this.titleColor = Colors.white,
    this.subtitleColor = const Color(0xFFD9E5F8),
    this.showBack = true,
    this.onBack,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    final useGradient = backgroundColor == DmColors.doorBlue;

    return Container(
      decoration: BoxDecoration(
        color: useGradient ? null : backgroundColor,
        gradient: useGradient
            ? LinearGradient(
                begin: Alignment.topLeft,
                end: Alignment.bottomRight,
                colors: [
                  DmColors.doorBlue,
                  DmColors.doorBlue.withAlpha(235),
                ],
              )
            : null,
        borderRadius: const BorderRadius.only(
          bottomLeft: Radius.circular(24),
          bottomRight: Radius.circular(24),
        ),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 18, 24, 20),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              if (showBack) _backButton(context),
              if (showBack) const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      title,
                      style: TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.w700,
                        color: titleColor,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Text(
                      subtitle,
                      style: TextStyle(fontSize: 13, color: subtitleColor),
                    ),
                  ],
                ),
              ),
              if (trailing != null) trailing!,
            ],
          ),
        ),
      ),
    );
  }

  Widget _backButton(BuildContext context) {
    final isLight = backgroundColor == Colors.white ||
        backgroundColor == DmColors.surfaceLight;
    final borderColor =
        isLight ? DmColors.borderLight : const Color(0x1FFFFFFF);

    return GestureDetector(
      onTap: onBack ?? () => Navigator.of(context).maybePop(),
      child: Container(
        height: 40,
        width: 40,
        decoration: BoxDecoration(
          color: isLight ? DmColors.surfaceAltLight : const Color(0x26FFFFFF),
          borderRadius: DmRadius.r14,
          border: Border.all(color: borderColor),
        ),
        child: Icon(
          Icons.arrow_back,
          color: isLight ? DmColors.doorBlue : Colors.white,
          size: 18,
        ),
      ),
    );
  }
}
