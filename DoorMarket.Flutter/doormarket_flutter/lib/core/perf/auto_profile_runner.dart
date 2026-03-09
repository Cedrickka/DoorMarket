import 'dart:async';
import 'dart:convert';
import 'dart:math' as math;

import 'package:flutter/foundation.dart';
import 'package:flutter/scheduler.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../router/app_router.dart';

const dmAutoProfileEnabled =
    bool.fromEnvironment('DM_AUTOPROFILE', defaultValue: false);

class AutoProfileRunner {
  AutoProfileRunner(this.ref);

  final WidgetRef ref;
  final Map<String, List<FrameTiming>> _framesByPhase =
      <String, List<FrameTiming>>{};
  final Map<String, int> _phaseDurationMs = <String, int>{};
  String _phase = 'bootstrap';
  bool _running = false;

  Future<void> start() async {
    if (_running) {
      return;
    }
    _running = true;
    final callback = _onTimings;
    SchedulerBinding.instance.addTimingsCallback(callback);
    try {
      await _runScenario();
      final report = _buildReport();
      _emitReport(report);
    } catch (e, st) {
      // ignore: avoid_print
      print('DM_AUTOPROFILE_ERROR:$e');
      // ignore: avoid_print
      print(st);
    } finally {
      SchedulerBinding.instance.removeTimingsCallback(callback);
      await Future<void>.delayed(const Duration(milliseconds: 250));
      await SystemNavigator.pop();
      _running = false;
    }
  }

  void _onTimings(List<FrameTiming> timings) {
    final bucket = _framesByPhase.putIfAbsent(_phase, () => <FrameTiming>[]);
    bucket.addAll(timings);
  }

  Future<void> _runScenario() async {
    await _phaseDelay('startup_idle', const Duration(seconds: 2));
    await _routePhase('home', '/', const Duration(seconds: 5));
    await _routePhase('categories', '/categories', const Duration(seconds: 4));
    await _routePhase('search', '/search', const Duration(seconds: 4));
    await _routePhase('shops', '/shops', const Duration(seconds: 4));
    await _routePhase('cart', '/cart', const Duration(seconds: 4));
    await _routePhase(
        'notifications', '/notifications', const Duration(seconds: 4));
    await _routePhase('home_return', '/', const Duration(seconds: 3));
  }

  Future<void> _routePhase(String phase, String route, Duration dwell) async {
    final started = DateTime.now().toUtc();
    _phase = phase;
    ref.read(appRouterProvider).go(route);
    await Future<void>.delayed(const Duration(milliseconds: 850));
    await Future<void>.delayed(dwell);
    _phaseDurationMs[phase] =
        DateTime.now().toUtc().difference(started).inMilliseconds;
  }

  Future<void> _phaseDelay(String phase, Duration delay) async {
    final started = DateTime.now().toUtc();
    _phase = phase;
    await Future<void>.delayed(delay);
    _phaseDurationMs[phase] =
        DateTime.now().toUtc().difference(started).inMilliseconds;
  }

  Map<String, dynamic> _buildReport() {
    final phases = <String, dynamic>{};
    _framesByPhase.forEach((name, frames) {
      phases[name] = _summarizePhase(frames);
      phases[name]['durationMs'] = _phaseDurationMs[name] ?? 0;
    });

    return <String, dynamic>{
      'generatedAtUtc': DateTime.now().toUtc().toIso8601String(),
      'platform': defaultTargetPlatform.name,
      'frameBudgetMs': 16.67,
      'phases': phases,
    };
  }

  Map<String, dynamic> _summarizePhase(List<FrameTiming> frames) {
    if (frames.isEmpty) {
      return <String, dynamic>{
        'frames': 0,
      };
    }

    final buildMs = frames
        .map((f) => f.buildDuration.inMicroseconds / 1000.0)
        .toList(growable: false);
    final rasterMs = frames
        .map((f) => f.rasterDuration.inMicroseconds / 1000.0)
        .toList(growable: false);
    final totalMs = List<double>.generate(
      frames.length,
      (i) => buildMs[i] + rasterMs[i],
      growable: false,
    );

    final slow16 = totalMs.where((v) => v > 16.67).length;
    final slow33 = totalMs.where((v) => v > 33.33).length;
    final maxTotal = totalMs.reduce(math.max);

    return <String, dynamic>{
      'frames': frames.length,
      'buildMs': _percentiles(buildMs),
      'rasterMs': _percentiles(rasterMs),
      'totalMs': _percentiles(totalMs),
      'slowFramesOver16msPct': _pct(slow16, frames.length),
      'slowFramesOver33msPct': _pct(slow33, frames.length),
      'maxFrameMs': maxTotal,
    };
  }

  static Map<String, double> _percentiles(List<double> values) {
    final sorted = [...values]..sort();
    return <String, double>{
      'p50': _percentile(sorted, 0.50),
      'p95': _percentile(sorted, 0.95),
      'p99': _percentile(sorted, 0.99),
      'avg': sorted.reduce((a, b) => a + b) / sorted.length,
    };
  }

  static double _percentile(List<double> sorted, double q) {
    if (sorted.isEmpty) return 0;
    final idx =
        (q * (sorted.length - 1)).clamp(0, sorted.length - 1).toDouble();
    final lower = idx.floor();
    final upper = idx.ceil();
    if (lower == upper) return sorted[lower];
    final weight = idx - lower;
    return sorted[lower] + (sorted[upper] - sorted[lower]) * weight;
  }

  static double _pct(int count, int total) {
    if (total <= 0) return 0;
    return (count * 100.0) / total;
  }

  void _emitReport(Map<String, dynamic> report) {
    final payload = jsonEncode(report);
    const chunkSize = 700;
    final total = (payload.length / chunkSize).ceil();
    for (var i = 0; i < total; i++) {
      final start = i * chunkSize;
      final end = math.min(start + chunkSize, payload.length);
      final chunk = payload.substring(start, end);
      // ignore: avoid_print
      print('DM_AUTOPROFILE_CHUNK:${i + 1}/$total:$chunk');
    }
    // ignore: avoid_print
    print('DM_AUTOPROFILE_DONE');
  }
}
