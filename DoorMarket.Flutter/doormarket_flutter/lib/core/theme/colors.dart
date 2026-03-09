import 'package:flutter/material.dart';

class DmColors {
  static const doorOrange = Color(0xFFFF7A00);
  static const doorBlue = Color(0xFF002D5E);

  // Light
  static const bgLight = Color(0xFFF2F4F7);
  static const surfaceLight = Color(0xFFFFFFFF);
  static const surfaceAltLight = Color(0xFFF7F8FA);
  static const borderLight = Color(0xFFE6E8EC);
  static const textPrimaryLight = Color(0xFF0F172A);
  static const textSecondaryLight = Color(0xFF5B6572);
  static const textMutedLight = Color(0xFF8A94A6);
  static const iconBgLight = Color(0xFFEEF2F8);
  static const orangeSoftBgLight = Color(0xFFFFF1E6);
  static const successLight = Color(0xFF22C55E);
  static const warningLight = Color(0xFFF59E0B);
  static const errorLight = Color(0xFFEF4444);

  // Dark
  static const bgDark = Color(0xFF0B1220);
  static const surfaceDark = Color(0xFF121B2D);
  static const surfaceAltDark = Color(0xFF18223A);
  static const borderDark = Color(0xFF22304F);
  static const textPrimaryDark = Color(0xFFE9EEF6);
  static const textSecondaryDark = Color(0xFFAAB4C3);
  static const textMutedDark = Color(0xFF7E8AA0);
  static const iconBgDark = Color(0xFF1C2945);
  static const orangeSoftBgDark = Color(0xFF2A1A10);
  static const successDark = Color(0xFF22C55E);
  static const warningDark = Color(0xFFF59E0B);
  static const errorDark = Color(0xFFF87171);

  // Semantic surfaces
  static const infoSurfaceLight = Color(0xFFEAF2FF);
  static const infoSurfaceDark = Color(0xFF1A2741);
  static const successSurfaceLight = Color(0xFFEAF9EF);
  static const successSurfaceDark = Color(0xFF173326);
  static const warningSurfaceLight = Color(0xFFFFF6E6);
  static const warningSurfaceDark = Color(0xFF35250F);
  static const errorSurfaceLight = Color(0xFFFFEBEB);
  static const errorSurfaceDark = Color(0xFF3A1C22);

  static Color iconBg(bool isDark) => isDark ? iconBgDark : iconBgLight;
  static Color iconFg(bool isDark) => isDark ? textPrimaryDark : doorBlue;
  static Color border(bool isDark) => isDark ? borderDark : borderLight;
  static Color mutedText(bool isDark) =>
      isDark ? textMutedDark : textMutedLight;
  static Color secondaryText(bool isDark) =>
      isDark ? textSecondaryDark : textSecondaryLight;
  static Color surface(bool isDark) => isDark ? surfaceDark : surfaceLight;
  static Color altSurface(bool isDark) =>
      isDark ? surfaceAltDark : surfaceAltLight;
  static Color infoSurface(bool isDark) =>
      isDark ? infoSurfaceDark : infoSurfaceLight;
  static Color successSurface(bool isDark) =>
      isDark ? successSurfaceDark : successSurfaceLight;
  static Color warningSurface(bool isDark) =>
      isDark ? warningSurfaceDark : warningSurfaceLight;
  static Color errorSurface(bool isDark) =>
      isDark ? errorSurfaceDark : errorSurfaceLight;
}
