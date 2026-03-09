import 'package:flutter/material.dart';
import '../theme/colors.dart';
import '../theme/radius.dart';

class DmBottomNav extends StatelessWidget {
  final int currentIndex;
  final ValueChanged<int> onTap;

  const DmBottomNav(
      {super.key, required this.currentIndex, required this.onTap});

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = isDark ? DmColors.surfaceDark : DmColors.surfaceLight;
    final border = isDark ? DmColors.borderDark : DmColors.borderLight;

    return Container(
      padding: const EdgeInsets.fromLTRB(18, 10, 18, 12),
      decoration: BoxDecoration(
        color: bg,
        border: Border(top: BorderSide(color: border)),
        boxShadow: const [
          BoxShadow(
              color: Color(0x14000000), blurRadius: 14, offset: Offset(0, -4)),
        ],
      ),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          _NavItem(
              icon: Icons.home_filled,
              label: 'Accueil',
              index: 0,
              currentIndex: currentIndex,
              onTap: onTap),
          _NavItem(
              icon: Icons.grid_view_rounded,
              label: 'Categories',
              index: 1,
              currentIndex: currentIndex,
              onTap: onTap),
          _PlusButton(onTap: () => onTap(2)),
          _NavItem(
              icon: Icons.receipt_long,
              label: 'Commandes',
              index: 3,
              currentIndex: currentIndex,
              onTap: onTap),
          _NavItem(
              icon: Icons.person,
              label: 'Profil',
              index: 4,
              currentIndex: currentIndex,
              onTap: onTap),
        ],
      ),
    );
  }
}

class _NavItem extends StatelessWidget {
  final IconData icon;
  final String label;
  final int index;
  final int currentIndex;
  final ValueChanged<int> onTap;

  const _NavItem({
    required this.icon,
    required this.label,
    required this.index,
    required this.currentIndex,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    final isActive = index == currentIndex;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final color = isActive
        ? DmColors.doorOrange
        : (isDark ? DmColors.textMutedDark : DmColors.textMutedLight);
    final bg = isActive ? DmColors.warningSurface(isDark) : Colors.transparent;

    return InkWell(
      borderRadius: DmRadius.r12,
      onTap: () => onTap(index),
      child: Semantics(
        button: true,
        selected: isActive,
        label: 'Navigation $label',
        child: Tooltip(
          message: label,
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  padding: const EdgeInsets.all(4),
                  decoration:
                      BoxDecoration(color: bg, borderRadius: DmRadius.r12),
                  child: Icon(icon, color: color, size: 22),
                ),
                const SizedBox(height: 4),
                Text(label,
                    style: TextStyle(
                        fontSize: 10,
                        color: color,
                        fontWeight: FontWeight.w600)),
              ],
            ),
          ),
        ),
      ),
    );
  }
}

class _PlusButton extends StatelessWidget {
  final VoidCallback onTap;

  const _PlusButton({required this.onTap});

  @override
  Widget build(BuildContext context) {
    return Semantics(
      button: true,
      label: 'Ajouter',
      child: GestureDetector(
      onTap: onTap,
        child: Tooltip(
          message: 'Ajouter',
          child: Container(
            height: 52,
            width: 52,
            decoration: BoxDecoration(
              color: DmColors.doorOrange,
              borderRadius: DmRadius.r20,
              boxShadow: const [
                BoxShadow(
                    color: Color(0x33000000),
                    blurRadius: 12,
                    offset: Offset(0, 6)),
              ],
            ),
            child: const Icon(Icons.add, color: Colors.white, size: 28),
          ),
        ),
      ),
    );
  }
}
