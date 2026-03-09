import 'package:flutter/material.dart';
import '../theme/colors.dart';
import '../theme/radius.dart';

class DmSearchBar extends StatelessWidget {
  final String hint;
  final TextEditingController? controller;
  final ValueChanged<String>? onSubmitted;
  final ValueChanged<String>? onChanged;
  final VoidCallback? onTap;
  final bool? readOnly;
  final IconData? trailingIcon;
  final VoidCallback? onTrailingTap;

  const DmSearchBar({
    super.key,
    required this.hint,
    this.controller,
    this.onSubmitted,
    this.onChanged,
    this.onTap,
    this.readOnly,
    this.trailingIcon,
    this.onTrailingTap,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = DmColors.surface(isDark);
    final border = DmColors.border(isDark);
    final iconColor = DmColors.iconFg(isDark);
    final resolvedReadOnly = readOnly ?? (onTap != null && onSubmitted == null);

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 6),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: DmRadius.r16,
        border: Border.all(color: border),
      ),
      child: Row(
        children: [
          Icon(Icons.search, size: 20, color: iconColor),
          const SizedBox(width: 8),
          Expanded(
            child: TextField(
              controller: controller,
              readOnly: resolvedReadOnly,
              onTap: onTap,
              onChanged: onChanged,
              onSubmitted: onSubmitted,
              decoration: InputDecoration(
                hintText: hint,
                border: InputBorder.none,
                isDense: true,
                hintStyle: TextStyle(
                  color: DmColors.mutedText(isDark),
                ),
              ),
            ),
          ),
          if (trailingIcon != null) ...[
            const SizedBox(width: 6),
            InkWell(
              onTap: onTrailingTap,
              borderRadius: DmRadius.r12,
              child: Container(
                height: 32,
                width: 32,
                decoration: BoxDecoration(
                  color: DmColors.iconBg(isDark),
                  borderRadius: DmRadius.r12,
                  border: Border.all(color: DmColors.border(isDark)),
                ),
                child: Icon(trailingIcon, size: 18, color: iconColor),
              ),
            ),
          ],
        ],
      ),
    );
  }
}

