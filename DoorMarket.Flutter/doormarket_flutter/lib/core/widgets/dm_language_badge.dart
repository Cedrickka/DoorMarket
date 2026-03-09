import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../providers.dart';

class DmLanguageBadge extends ConsumerWidget {
  final Color background;
  final Color textColor;
  final double height;
  final double width;

  const DmLanguageBadge({
    super.key,
    this.background = const Color(0x26FFFFFF),
    this.textColor = Colors.white,
    this.height = 34,
    this.width = 58,
  });

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final locale = ref.watch(localeControllerProvider);
    final code = locale?.languageCode;
    final label = code == 'en' ? 'EN' : 'FR';
    final flag = code == 'en' ? '🇬🇧' : '🇫🇷';

    return GestureDetector(
      onTap: () => _showLanguageSheet(context, ref),
      child: Container(
        height: height,
        width: width,
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(10),
        ),
        child: Row(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            Text(flag, style: const TextStyle(fontSize: 12)),
            const SizedBox(width: 6),
            Text(
              label,
              style: TextStyle(fontSize: 11, fontWeight: FontWeight.w600, color: textColor),
            ),
            const SizedBox(width: 2),
            Icon(Icons.keyboard_arrow_down_rounded, size: 14, color: textColor),
          ],
        ),
      ),
    );
  }

  Future<void> _showLanguageSheet(BuildContext context, WidgetRef ref) async {
    final strings = ref.read(stringsProvider);
    final result = await showModalBottomSheet<String>(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) {
        return SafeArea(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ListTile(
                title: Text(strings.languageFrench),
                onTap: () => Navigator.pop(ctx, 'fr'),
              ),
              ListTile(
                title: Text(strings.languageEnglish),
                onTap: () => Navigator.pop(ctx, 'en'),
              ),
              ListTile(
                title: Text(strings.languageSystem),
                onTap: () => Navigator.pop(ctx, 'system'),
              ),
            ],
          ),
        );
      },
    );

    if (result == null) return;
    if (result == 'system') {
      ref.read(localeControllerProvider.notifier).setLocale(null);
    } else {
      ref.read(localeControllerProvider.notifier).setLocale(result);
    }
  }
}
