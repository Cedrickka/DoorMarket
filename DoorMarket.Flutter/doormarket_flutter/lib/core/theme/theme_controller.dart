import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class ThemeController extends ChangeNotifier {
  static const _keyThemeMode = 'dm_theme_mode';
  static const _keyUseSystem = 'dm_theme_system';

  ThemeMode _themeMode = ThemeMode.system;
  bool _useSystem = true;

  ThemeController() {
    _load();
  }

  ThemeMode get themeMode => _themeMode;
  bool get useSystem => _useSystem;

  void setUseSystem(bool value) {
    _useSystem = value;
    _themeMode = value ? ThemeMode.system : _themeMode == ThemeMode.system ? ThemeMode.light : _themeMode;
    _save();
    notifyListeners();
  }

  void setDarkMode(bool isDark) {
    _useSystem = false;
    _themeMode = isDark ? ThemeMode.dark : ThemeMode.light;
    _save();
    notifyListeners();
  }

  Future<void> _load() async {
    final prefs = await SharedPreferences.getInstance();
    _useSystem = prefs.getBool(_keyUseSystem) ?? true;
    final stored = prefs.getString(_keyThemeMode);
    if (_useSystem) {
      _themeMode = ThemeMode.system;
    } else if (stored == 'dark') {
      _themeMode = ThemeMode.dark;
    } else {
      _themeMode = ThemeMode.light;
    }
    notifyListeners();
  }

  Future<void> _save() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_keyUseSystem, _useSystem);
    await prefs.setString(_keyThemeMode, _themeMode == ThemeMode.dark ? 'dark' : 'light');
  }
}
