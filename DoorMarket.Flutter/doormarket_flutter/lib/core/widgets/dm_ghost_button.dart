import 'package:flutter/material.dart';
import '../theme/radius.dart';

class DmGhostButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final double height;
  final double? width;
  final String? semanticsLabel;
  final String? tooltip;

  const DmGhostButton({
    super.key,
    required this.label,
    this.onPressed,
    this.height = 52,
    this.width,
    this.semanticsLabel,
    this.tooltip,
  });

  @override
  Widget build(BuildContext context) {
    final normalizedLabel = (semanticsLabel ?? '').trim();
    final normalizedTooltip = (tooltip ?? '').trim();

    Widget button = OutlinedButton(
      style: OutlinedButton.styleFrom(
        shape: RoundedRectangleBorder(borderRadius: DmRadius.r14),
        textStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 16),
      ),
      onPressed: onPressed,
      child: Text(label),
    );

    if (normalizedTooltip.isNotEmpty) {
      button = Tooltip(message: normalizedTooltip, child: button);
    }

    return SizedBox(
      width: width ?? double.infinity,
      height: height,
      child: Semantics(
        button: true,
        enabled: onPressed != null,
        label: normalizedLabel.isEmpty ? label : normalizedLabel,
        child: button,
      ),
    );
  }
}
