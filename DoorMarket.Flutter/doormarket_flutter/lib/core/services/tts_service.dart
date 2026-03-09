import 'package:flutter/widgets.dart';
import 'package:flutter_tts/flutter_tts.dart';

class TtsService {
  final FlutterTts _tts = FlutterTts();
  bool _initialized = false;
  String? _currentLang;

  Future<void> speak(String message, Locale locale) async {
    final text = message.trim();
    if (text.isEmpty) return;

    await _ensureInitialized(locale);
    await _tts.stop();
    await _tts.speak(text);
  }

  Future<void> _ensureInitialized(Locale locale) async {
    final lang = locale.languageCode.toLowerCase();
    final ttsLang = lang == 'fr' ? 'fr-FR' : 'en-US';

    if (!_initialized || _currentLang != ttsLang) {
      await _tts.setLanguage(ttsLang);
      await _tts.setSpeechRate(0.5);
      await _tts.setPitch(1.0);
      _currentLang = ttsLang;
      _initialized = true;
    }
  }
}
