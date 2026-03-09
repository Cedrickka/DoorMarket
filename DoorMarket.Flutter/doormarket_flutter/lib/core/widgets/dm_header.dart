import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../theme/colors.dart';
import '../theme/radius.dart';
import 'dm_brand_mark.dart';
import 'dm_language_badge.dart';

class DmHeader extends ConsumerWidget {
  final String title;
  final String subtitle;
  final bool showBack;
  final VoidCallback? onBack;
  final bool showAction;
  final IconData? actionIcon;
  final VoidCallback? onAction;
  final bool showLanguageBadge;

  const DmHeader({
    super.key,
    required this.title,
    required this.subtitle,
    this.showBack = false,
    this.onBack,
    this.showAction = false,
    this.actionIcon,
    this.onAction,
    this.showLanguageBadge = true,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final isDark = Theme.of(context).brightness == Brightness.dark;

    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            DmColors.doorBlue,
            DmColors.doorBlue.withAlpha(isDark ? 235 : 245),
          ],
        ),
        borderRadius: const BorderRadius.only(
          bottomLeft: Radius.circular(26),
          bottomRight: Radius.circular(26),
        ),
      ),
      child: SafeArea(
        bottom: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(24, 20, 24, 20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  if (showBack)
                    _iconPill(
                      icon: Icons.arrow_back,
                      onTap: onBack,
                      semanticsLabel: 'Back',
                    ),
                  if (showBack) const SizedBox(width: 10),
                  _logoPill(),
                  const SizedBox(width: 10),
                  const Expanded(
                    child: Text(
                      'DoorMarket',
                      style: TextStyle(
                        color: Colors.white,
                        fontSize: 22,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  if (showLanguageBadge) const DmLanguageBadge(),
                  if (showAction) const SizedBox(width: 10),
                  if (showAction)
                    _iconPill(
                      icon: actionIcon ?? Icons.more_horiz,
                      onTap: onAction,
                      semanticsLabel: 'Action',
                    ),
                ],
              ),
              const SizedBox(height: 12),
              if (title.isNotEmpty)
                Text(
                  title,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 22,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              if (subtitle.isNotEmpty) const SizedBox(height: 4),
              if (subtitle.isNotEmpty)
                Text(
                  subtitle,
                  style:
                      const TextStyle(color: Color(0xFFD9E5F8), fontSize: 13),
                ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _logoPill() {
    return Semantics(
      image: true,
      label: 'DoorMarket logo',
      child: const ExcludeSemantics(
        child: DmBrandMark(size: 30, monochromeWhite: true),
      ),
    );
  }

  Widget _iconPill({
    required IconData icon,
    VoidCallback? onTap,
    String? semanticsLabel,
  }) {
    return Semantics(
      button: true,
      enabled: onTap != null,
      label: semanticsLabel,
      child: Material(
        color: Colors.transparent,
        child: InkWell(
          borderRadius: DmRadius.r12,
          onTap: onTap,
          child: Container(
            height: 40,
            width: 40,
            decoration: BoxDecoration(
              color: const Color(0x26FFFFFF),
              borderRadius: DmRadius.r12,
              border: Border.all(color: const Color(0x26FFFFFF)),
            ),
            child: Icon(icon, color: Colors.white, size: 18),
          ),
        ),
      ),
    );
  }
}
