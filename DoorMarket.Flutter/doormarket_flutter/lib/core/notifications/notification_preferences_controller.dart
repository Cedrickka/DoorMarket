import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

class NotificationPreferencesController extends ChangeNotifier {
  static const _keyEnabled = 'dm_notif_enabled';
  static const _keyOrders = 'dm_notif_orders';
  static const _keyPayments = 'dm_notif_payments';
  static const _keyDelivery = 'dm_notif_delivery';
  static const _keyReadEvents = 'dm_notif_read_events';
  static const _maxReadEvents = 500;

  bool _loaded = false;
  bool _enabled = true;
  bool _ordersEnabled = true;
  bool _paymentsEnabled = true;
  bool _deliveryEnabled = true;
  List<String> _readEvents = const [];

  NotificationPreferencesController() {
    _load();
  }

  bool get loaded => _loaded;
  bool get enabled => _enabled;
  bool get ordersEnabled => _ordersEnabled;
  bool get paymentsEnabled => _paymentsEnabled;
  bool get deliveryEnabled => _deliveryEnabled;
  int get readEventsCount => _readEvents.length;

  bool allowEventKind(String kind) {
    if (!_enabled) return false;
    switch (kind) {
      case 'payment_failed':
      case 'payment_pending':
      case 'payment_paid':
        return _paymentsEnabled;
      case 'delivered':
        return _deliveryEnabled;
      case 'in_progress':
      default:
        return _ordersEnabled;
    }
  }

  bool isRead(String eventId) => _readEvents.contains(eventId);

  Future<void> setEnabled(bool value) async {
    _enabled = value;
    notifyListeners();
    await _saveBools();
  }

  Future<void> setOrdersEnabled(bool value) async {
    _ordersEnabled = value;
    notifyListeners();
    await _saveBools();
  }

  Future<void> setPaymentsEnabled(bool value) async {
    _paymentsEnabled = value;
    notifyListeners();
    await _saveBools();
  }

  Future<void> setDeliveryEnabled(bool value) async {
    _deliveryEnabled = value;
    notifyListeners();
    await _saveBools();
  }

  Future<void> markRead(String eventId) async {
    final normalized = eventId.trim();
    if (normalized.isEmpty) return;
    if (_readEvents.contains(normalized)) return;

    final updated = <String>[normalized, ..._readEvents];
    if (updated.length > _maxReadEvents) {
      updated.removeRange(_maxReadEvents, updated.length);
    }

    _readEvents = updated;
    notifyListeners();
    await _saveReadEvents();
  }

  Future<void> markManyRead(Iterable<String> eventIds) async {
    var changed = false;
    final seen = _readEvents.toSet();
    final updated = <String>[];

    for (final raw in eventIds) {
      final id = raw.trim();
      if (id.isEmpty) continue;
      if (seen.add(id)) {
        updated.add(id);
        changed = true;
      }
    }

    if (!changed) return;

    updated.addAll(_readEvents);
    if (updated.length > _maxReadEvents) {
      updated.removeRange(_maxReadEvents, updated.length);
    }
    _readEvents = updated;
    notifyListeners();
    await _saveReadEvents();
  }

  Future<void> clearReadEvents() async {
    _readEvents = const [];
    notifyListeners();
    final prefs = await SharedPreferences.getInstance();
    await prefs.remove(_keyReadEvents);
  }

  Future<void> _load() async {
    final prefs = await SharedPreferences.getInstance();
    _enabled = prefs.getBool(_keyEnabled) ?? true;
    _ordersEnabled = prefs.getBool(_keyOrders) ?? true;
    _paymentsEnabled = prefs.getBool(_keyPayments) ?? true;
    _deliveryEnabled = prefs.getBool(_keyDelivery) ?? true;
    _readEvents = prefs.getStringList(_keyReadEvents) ?? const [];
    _loaded = true;
    notifyListeners();
  }

  Future<void> _saveBools() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_keyEnabled, _enabled);
    await prefs.setBool(_keyOrders, _ordersEnabled);
    await prefs.setBool(_keyPayments, _paymentsEnabled);
    await prefs.setBool(_keyDelivery, _deliveryEnabled);
  }

  Future<void> _saveReadEvents() async {
    final prefs = await SharedPreferences.getInstance();
    await prefs.setStringList(_keyReadEvents, _readEvents);
  }
}
