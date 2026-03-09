import 'package:flutter/material.dart';
import 'dm_ghost_button.dart';

class DmSecondaryButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final double height;
  final double? width;
  final String? semanticsLabel;
  final String? tooltip;

  const DmSecondaryButton({
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
    return DmGhostButton(
      label: label,
      onPressed: onPressed,
      height: height,
      width: width,
      semanticsLabel: semanticsLabel,
      tooltip: tooltip,
    );
  }
}
