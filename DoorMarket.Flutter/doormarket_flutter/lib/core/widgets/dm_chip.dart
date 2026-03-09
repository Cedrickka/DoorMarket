import 'package:flutter/material.dart';
import '../theme/colors.dart';
import '../theme/radius.dart';

class DmChip extends StatelessWidget {
  final String label;
  final bool selected;
  final VoidCallback? onTap;

  const DmChip({super.key, required this.label, this.selected = false, this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight;
    final border = isDark ? DmColors.borderDark : DmColors.borderLight;

    return InkWell(
      borderRadius: DmRadius.r16,
      onTap: onTap,
      child: Container(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 6),
        decoration: BoxDecoration(
          color: selected ? DmColors.doorOrange : bg,
          borderRadius: DmRadius.r16,
          border: Border.all(color: selected ? DmColors.doorOrange : border),
        ),
        child: Text(
          label,
          style: TextStyle(
            color: selected ? Colors.white : (isDark ? DmColors.textPrimaryDark : DmColors.textPrimaryLight),
            fontWeight: FontWeight.w600,
            fontSize: 12,
          ),
        ),
      ),
    );
  }
}
