import 'package:flutter/material.dart';
import '../theme/colors.dart';
import '../theme/radius.dart';

class DmBadgePromo extends StatelessWidget {
  final String label;

  const DmBadgePromo({super.key, required this.label});

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: DmColors.doorOrange,
        borderRadius: DmRadius.r12,
      ),
      child: Text(
        label,
        style: const TextStyle(
          color: Colors.white,
          fontWeight: FontWeight.w600,
          fontSize: 12,
        ),
      ),
    );
  }
}
