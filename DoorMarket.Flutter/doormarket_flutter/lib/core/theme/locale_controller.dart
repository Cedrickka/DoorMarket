import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:shared_preferences/shared_preferences.dart';

class LocaleController extends StateNotifier<Locale?> {
  static const _keyLocale = 'dm_locale';

  LocaleController() : super(null) {
    _load();
  }

  void setLocale(String? code) {
    if (code == null || code.isEmpty) {
      state = null;
      _save(null);
      return;
    }
    state = Locale(code);
    _save(code);
  }

  Future<void> _load() async {
    final prefs = await SharedPreferences.getInstance();
    final stored = prefs.getString(_keyLocale);
    if (stored == null || stored.isEmpty) {
      state = null;
    } else {
      state = Locale(stored);
    }
  }

  Future<void> _save(String? code) async {
    final prefs = await SharedPreferences.getInstance();
    if (code == null || code.isEmpty) {
      await prefs.remove(_keyLocale);
    } else {
      await prefs.setString(_keyLocale, code);
    }
  }
}
