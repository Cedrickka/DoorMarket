import 'package:flutter/material.dart';
import '../theme/colors.dart';
import '../theme/radius.dart';
import '../theme/theme.dart';

class DmCard extends StatelessWidget {
  final Widget child;
  final EdgeInsetsGeometry padding;
  final EdgeInsetsGeometry margin;
  final Color? backgroundColor;
  final Color? borderColor;
  final List<BoxShadow>? boxShadowOverride;

  const DmCard({
    super.key,
    required this.child,
    this.padding = const EdgeInsets.all(16),
    this.margin = const EdgeInsets.only(bottom: 16),
    this.backgroundColor,
    this.borderColor,
    this.boxShadowOverride,
  });

  @override
  Widget build(BuildContext context) {
    final brightness = Theme.of(context).brightness;
    final shadows = Theme.of(context).extension<DmShadowExtension>();
    final defaultShadow =
        brightness == Brightness.dark ? (shadows?.dark ?? const []) : (shadows?.light ?? const []);
    final resolvedShadow = boxShadowOverride ?? defaultShadow;
    final resolvedBg = backgroundColor ??
        (brightness == Brightness.dark ? DmColors.surfaceDark : DmColors.surfaceLight);
    final resolvedBorder =
        borderColor ?? (brightness == Brightness.dark ? DmColors.borderDark : DmColors.borderLight);

    return Container(
      margin: margin,
      padding: padding,
      decoration: BoxDecoration(
        color: resolvedBg,
        borderRadius: DmRadius.r16,
        border: Border.all(color: resolvedBorder),
        boxShadow: resolvedShadow,
      ),
      child: child,
    );
  }
}
