import 'package:flutter/material.dart';
import 'colors.dart';
import 'radius.dart';
import 'shadows.dart';
import 'typography.dart';

class DmTheme {
  static ThemeData light() {
    final scheme = const ColorScheme.light(
      primary: DmColors.doorOrange,
      secondary: DmColors.doorBlue,
      surface: DmColors.surfaceLight,
      error: DmColors.errorLight,
      onPrimary: Colors.white,
      onSecondary: Colors.white,
      onSurface: DmColors.textPrimaryLight,
      onError: Colors.white,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.light,
      colorScheme: scheme,
      scaffoldBackgroundColor: DmColors.bgLight,
      textTheme: DmTypography.textTheme(isDark: false),
      appBarTheme: const AppBarTheme(
        backgroundColor: DmColors.surfaceLight,
        foregroundColor: DmColors.textPrimaryLight,
        elevation: 0,
        centerTitle: false,
      ),
      cardTheme: const CardThemeData(
        color: DmColors.surfaceLight,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(borderRadius: DmRadius.r16),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        filled: true,
        fillColor: DmColors.surfaceAltLight,
        border: OutlineInputBorder(
          borderRadius: DmRadius.r12,
          borderSide: BorderSide(color: DmColors.borderLight),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: DmRadius.r12,
          borderSide: BorderSide(color: DmColors.borderLight),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: DmRadius.r12,
          borderSide: BorderSide(color: DmColors.doorOrange, width: 1.5),
        ),
        hintStyle: TextStyle(color: DmColors.textMutedLight),
      ),
      chipTheme: const ChipThemeData(
        backgroundColor: DmColors.surfaceAltLight,
        shape: RoundedRectangleBorder(
          borderRadius: DmRadius.r16,
          side: BorderSide(color: DmColors.borderLight),
        ),
        labelStyle: TextStyle(color: DmColors.textPrimaryLight),
      ),
      dividerColor: DmColors.borderLight,
      shadowColor: DmColors.borderLight,
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: DmColors.doorOrange,
          foregroundColor: Colors.white,
          textStyle: const TextStyle(fontFamily: DmTypography.fontFamily, fontSize: 16, fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: DmRadius.r14),
          minimumSize: const Size.fromHeight(52),
          elevation: 0,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: DmColors.doorBlue,
          side: BorderSide(color: DmColors.borderLight),
          textStyle: const TextStyle(fontFamily: DmTypography.fontFamily, fontSize: 16, fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: DmRadius.r14),
          minimumSize: const Size.fromHeight(52),
        ),
      ),
      bottomNavigationBarTheme: BottomNavigationBarThemeData(
        backgroundColor: DmColors.surfaceLight,
        selectedItemColor: DmColors.doorOrange,
        unselectedItemColor: DmColors.textMutedLight,
        showSelectedLabels: true,
        showUnselectedLabels: true,
        type: BottomNavigationBarType.fixed,
      ),
      extensions: const <ThemeExtension<dynamic>>[
        DmShadowExtension(light: DmShadows.light, dark: DmShadows.dark),
      ],
    );
  }

  static ThemeData dark() {
    final scheme = const ColorScheme.dark(
      primary: DmColors.doorOrange,
      secondary: DmColors.doorBlue,
      surface: DmColors.surfaceDark,
      error: DmColors.errorDark,
      onPrimary: Colors.white,
      onSecondary: Colors.white,
      onSurface: DmColors.textPrimaryDark,
      onError: Colors.white,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: Brightness.dark,
      colorScheme: scheme,
      scaffoldBackgroundColor: DmColors.bgDark,
      textTheme: DmTypography.textTheme(isDark: true),
      appBarTheme: const AppBarTheme(
        backgroundColor: DmColors.surfaceDark,
        foregroundColor: DmColors.textPrimaryDark,
        elevation: 0,
        centerTitle: false,
      ),
      cardTheme: const CardThemeData(
        color: DmColors.surfaceDark,
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(borderRadius: DmRadius.r16),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        filled: true,
        fillColor: DmColors.surfaceAltDark,
        border: OutlineInputBorder(
          borderRadius: DmRadius.r12,
          borderSide: BorderSide(color: DmColors.borderDark),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: DmRadius.r12,
          borderSide: BorderSide(color: DmColors.borderDark),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: DmRadius.r12,
          borderSide: BorderSide(color: DmColors.doorOrange, width: 1.5),
        ),
        hintStyle: TextStyle(color: DmColors.textMutedDark),
      ),
      chipTheme: const ChipThemeData(
        backgroundColor: DmColors.surfaceAltDark,
        shape: RoundedRectangleBorder(
          borderRadius: DmRadius.r16,
          side: BorderSide(color: DmColors.borderDark),
        ),
        labelStyle: TextStyle(color: DmColors.textPrimaryDark),
      ),
      dividerColor: DmColors.borderDark,
      shadowColor: DmColors.borderDark,
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: DmColors.doorOrange,
          foregroundColor: Colors.white,
          textStyle: const TextStyle(fontFamily: DmTypography.fontFamily, fontSize: 16, fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: DmRadius.r14),
          minimumSize: const Size.fromHeight(52),
          elevation: 0,
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: DmColors.textSecondaryDark,
          side: BorderSide(color: DmColors.borderDark),
          textStyle: const TextStyle(fontFamily: DmTypography.fontFamily, fontSize: 16, fontWeight: FontWeight.w600),
          shape: RoundedRectangleBorder(borderRadius: DmRadius.r14),
          minimumSize: const Size.fromHeight(52),
        ),
      ),
      bottomNavigationBarTheme: BottomNavigationBarThemeData(
        backgroundColor: DmColors.surfaceDark,
        selectedItemColor: DmColors.doorOrange,
        unselectedItemColor: DmColors.textMutedDark,
        showSelectedLabels: true,
        showUnselectedLabels: true,
        type: BottomNavigationBarType.fixed,
      ),
      extensions: const <ThemeExtension<dynamic>>[
        DmShadowExtension(light: DmShadows.light, dark: DmShadows.dark),
      ],
    );
  }
}

class DmShadowExtension extends ThemeExtension<DmShadowExtension> {
  final List<BoxShadow> light;
  final List<BoxShadow> dark;

  const DmShadowExtension({required this.light, required this.dark});

  @override
  DmShadowExtension copyWith({List<BoxShadow>? light, List<BoxShadow>? dark}) {
    return DmShadowExtension(light: light ?? this.light, dark: dark ?? this.dark);
  }

  @override
  DmShadowExtension lerp(ThemeExtension<DmShadowExtension>? other, double t) {
    if (other is! DmShadowExtension) return this;
    return DmShadowExtension(light: light, dark: other.dark);
  }
}
