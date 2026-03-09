import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:dio/dio.dart';

import '../../core/models/delivery.dart';
import '../../core/models/me.dart';
import '../../core/network/api_exception.dart';
import '../../core/providers.dart';
import '../../core/theme/colors.dart';
import '../../core/widgets/dm_card.dart';
import '../../core/widgets/dm_header.dart';
import '../../core/widgets/dm_primary_button.dart';
import 'addresses_provider.dart';

class AddressFormScreen extends ConsumerStatefulWidget {
  final String? addressId;

  const AddressFormScreen({super.key, this.addressId});

  @override
  ConsumerState<AddressFormScreen> createState() => _AddressFormScreenState();
}

class _AddressFormScreenState extends ConsumerState<AddressFormScreen> {
  final _labelController = TextEditingController();
  final _fullNameController = TextEditingController();
  final _phoneController = TextEditingController();
  final _countryController = TextEditingController();
  final _cityController = TextEditingController();
  final _districtController = TextEditingController();
  final _streetController = TextEditingController();
  final _landmarkController = TextEditingController();
  String? _selectedZoneId;
  late final Future<List<DeliveryZoneDto>> _zonesFuture;
  bool _isDefault = false;
  bool _loading = false;
  String? _error;
  bool _prefilledFromExisting = false;

  @override
  void initState() {
    super.initState();
    _zonesFuture = ref.read(deliveryApiProvider).getZones();
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (_prefilledFromExisting || widget.addressId == null) return;
    final addresses = ref.read(addressesProvider).value;
    if (addresses == null || addresses.isEmpty) {
      return;
    }

    AddressDto? address;
    for (final entry in addresses) {
      if (entry.id == widget.addressId) {
        address = entry;
        break;
      }
    }
    address ??= addresses.first;

    _labelController.text = address.label;
    _fullNameController.text = address.fullName;
    _phoneController.text = address.phone;
    _countryController.text = address.country;
    _cityController.text = address.city;
    _districtController.text = address.district;
    _streetController.text = address.street;
    _landmarkController.text = address.landmark ?? '';
    _isDefault = address.isDefault;
    _selectedZoneId = address.deliveryZoneId;
    _prefilledFromExisting = true;
  }

  @override
  void dispose() {
    _labelController.dispose();
    _fullNameController.dispose();
    _phoneController.dispose();
    _countryController.dispose();
    _cityController.dispose();
    _districtController.dispose();
    _streetController.dispose();
    _landmarkController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final t = _tr;
    final isDark = Theme.of(context).brightness == Brightness.dark;
    return Scaffold(
      body: Column(
        children: [
          DmHeader(
            title: widget.addressId == null
                ? t(context, 'Nouvelle adresse', 'New address')
                : t(context, 'Modifier adresse', 'Edit address'),
            subtitle:
                t(context, 'Informations de livraison', 'Delivery information'),
            showBack: true,
            onBack: () => Navigator.of(context).pop(),
          ),
          Expanded(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: Column(
                children: [
                  DmCard(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(t(context, 'Informations', 'Information'),
                            style: Theme.of(context).textTheme.headlineSmall),
                        const SizedBox(height: 16),
                        TextField(
                            controller: _labelController,
                            decoration: InputDecoration(
                                labelText: t(context, 'Libelle', 'Label'))),
                        const SizedBox(height: 12),
                        TextField(
                            controller: _fullNameController,
                            decoration: InputDecoration(
                                labelText:
                                    t(context, 'Nom complet', 'Full name'))),
                        const SizedBox(height: 12),
                        TextField(
                            controller: _phoneController,
                            decoration: InputDecoration(
                                labelText: t(context, 'Telephone', 'Phone'))),
                        const SizedBox(height: 12),
                        TextField(
                            controller: _countryController,
                            decoration: InputDecoration(
                                labelText: t(context, 'Pays', 'Country'))),
                        const SizedBox(height: 12),
                        TextField(
                            controller: _cityController,
                            decoration: InputDecoration(
                                labelText: t(context, 'Ville', 'City'))),
                        const SizedBox(height: 12),
                        TextField(
                            controller: _districtController,
                            decoration: InputDecoration(
                                labelText: t(context, 'Quartier', 'District'))),
                        const SizedBox(height: 12),
                        FutureBuilder<List<DeliveryZoneDto>>(
                          future: _zonesFuture,
                          builder: (context, snapshot) {
                            if (snapshot.connectionState ==
                                ConnectionState.waiting) {
                              return const Padding(
                                padding: EdgeInsets.symmetric(vertical: 8),
                                child: LinearProgressIndicator(minHeight: 2),
                              );
                            }

                            final zones = _normalizeZones(snapshot.data ?? []);
                            if (zones.isEmpty) {
                              return Text(
                                t(
                                  context,
                                  'Aucune zone de livraison disponible.',
                                  'No delivery zone available.',
                                ),
                                style: Theme.of(context).textTheme.bodySmall,
                              );
                            }

                            final zoneIds =
                                zones.map((zone) => zone.id).toSet();
                            final selectedValue = (_selectedZoneId != null &&
                                    zoneIds.contains(_selectedZoneId))
                                ? _selectedZoneId
                                : null;

                            return DropdownButtonFormField<String>(
                              key: ValueKey(
                                  '${zones.length}-${selectedValue ?? 'none'}'),
                              initialValue: selectedValue,
                              isExpanded: true,
                              menuMaxHeight: 360,
                              items: zones
                                  .map((zone) => DropdownMenuItem<String>(
                                        value: zone.id,
                                        child: Text(
                                          _zoneLabel(context, zone,
                                              withFee: true),
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                        ),
                                      ))
                                  .toList(),
                              selectedItemBuilder: (_) => zones
                                  .map(
                                    (zone) => Align(
                                      alignment: Alignment.centerLeft,
                                      child: Text(
                                        _zoneLabel(context, zone),
                                        maxLines: 1,
                                        overflow: TextOverflow.ellipsis,
                                      ),
                                    ),
                                  )
                                  .toList(),
                              onChanged: (value) =>
                                  setState(() => _selectedZoneId = value),
                              decoration: InputDecoration(
                                labelText: t(context, 'Zone de livraison',
                                    'Delivery zone'),
                                contentPadding: const EdgeInsets.symmetric(
                                    horizontal: 12, vertical: 14),
                              ),
                            );
                          },
                        ),
                        const SizedBox(height: 12),
                        TextField(
                            controller: _streetController,
                            decoration: InputDecoration(
                                labelText: t(context, 'Adresse', 'Address'))),
                        const SizedBox(height: 12),
                        TextField(
                            controller: _landmarkController,
                            decoration: InputDecoration(
                                labelText: t(context, 'Repere', 'Landmark'))),
                        const SizedBox(height: 12),
                        SwitchListTile.adaptive(
                          contentPadding: EdgeInsets.zero,
                          title: Text(t(context, 'Adresse par defaut',
                              'Default address')),
                          value: _isDefault,
                          onChanged: (value) =>
                              setState(() => _isDefault = value),
                        ),
                        if (_error != null) ...[
                          const SizedBox(height: 12),
                          Text(
                            _error!,
                            style: TextStyle(
                              color:
                                  isDark ? DmColors.errorDark : DmColors.errorLight,
                            ),
                          ),
                        ],
                        const SizedBox(height: 12),
                        DmPrimaryButton(
                          label: _loading
                              ? t(context, 'Sauvegarde...', 'Saving...')
                              : t(context, 'Enregistrer', 'Save'),
                          onPressed: _loading ? null : _save,
                        ),
                      ],
                    ),
                  ),
                  if (_loading) ...[
                    const SizedBox(height: 12),
                    const CircularProgressIndicator(),
                  ],
                ],
              ),
            ),
          ),
        ],
      ),
    );
  }

  static String _tr(BuildContext context, String fr, String en) {
    final isFr =
        Localizations.localeOf(context).languageCode.toLowerCase() == 'fr';
    return isFr ? fr : en;
  }

  Future<void> _save() async {
    final validationError = _validateForm();
    if (validationError != null) {
      setState(() => _error = validationError);
      return;
    }

    setState(() {
      _loading = true;
      _error = null;
    });
    final api = ref.read(addressApiProvider);
    final label = _labelController.text.trim();
    final fullName = _fullNameController.text.trim();
    final phone = _phoneController.text.trim();
    final country = _countryController.text.trim();
    final city = _cityController.text.trim();
    final district = _districtController.text.trim();
    final street = _streetController.text.trim();
    final landmark = _landmarkController.text.trim();
    try {
      if (widget.addressId == null) {
        final payload = CreateAddressRequest(
          label: label,
          fullName: fullName,
          phone: phone,
          country: country,
          city: city,
          district: district,
          deliveryZoneId: _selectedZoneId,
          street: street,
          landmark: landmark.isEmpty ? null : landmark,
          isDefault: _isDefault,
        );
        await api.create(payload);
      } else {
        final payload = UpdateAddressRequest(
          label: label,
          fullName: fullName,
          phone: phone,
          country: country,
          city: city,
          district: district,
          deliveryZoneId: _selectedZoneId,
          street: street,
          landmark: landmark.isEmpty ? null : landmark,
          isDefault: _isDefault,
        );
        await api.update(widget.addressId!, payload);
      }
      ref.invalidate(addressesProvider);
      if (mounted) Navigator.pop(context);
    } catch (e) {
      if (mounted) setState(() => _error = _toUserMessage(e));
    } finally {
      if (mounted) setState(() => _loading = false);
    }
  }

  String? _validateForm() {
    final t = _tr;
    if (_labelController.text.trim().isEmpty) {
      return t(context, 'Le libelle est requis.', 'Label is required.');
    }
    if (_fullNameController.text.trim().isEmpty) {
      return t(context, 'Le nom complet est requis.', 'Full name is required.');
    }
    if (_phoneController.text.trim().isEmpty) {
      return t(context, 'Le telephone est requis.', 'Phone is required.');
    }
    if (_countryController.text.trim().isEmpty) {
      return t(context, 'Le pays est requis.', 'Country is required.');
    }
    if (_cityController.text.trim().isEmpty) {
      return t(context, 'La ville est requise.', 'City is required.');
    }
    if (_districtController.text.trim().isEmpty) {
      return t(context, 'Le quartier est requis.', 'District is required.');
    }
    if (_selectedZoneId == null || _selectedZoneId!.trim().isEmpty) {
      return t(
        context,
        'La zone de livraison est requise.',
        'Delivery zone is required.',
      );
    }
    if (_streetController.text.trim().isEmpty) {
      return t(context, 'L adresse est requise.', 'Address is required.');
    }
    return null;
  }

  String _toUserMessage(Object error) {
    final t = _tr;
    if (error is ApiException) {
      final message = error.message.trim();
      if (message.isNotEmpty) {
        return message;
      }
      return t(
        context,
        'Impossible d enregistrer cette adresse pour le moment.',
        'Unable to save this address right now.',
      );
    }
    if (error is DioException) {
      final data = error.response?.data;
      if (data is Map<String, dynamic>) {
        final apiMessage = (data['error'] ?? data['title'] ?? '').toString();
        if (apiMessage.trim().isNotEmpty) {
          return apiMessage.trim();
        }
      }
      return t(
        context,
        'La validation de l adresse a echoue. Verifiez la zone de livraison et les champs requis.',
        'Address validation failed. Verify delivery zone and required fields.',
      );
    }

    return t(
      context,
      'Impossible d enregistrer cette adresse pour le moment.',
      'Unable to save this address right now.',
    );
  }

  List<DeliveryZoneDto> _normalizeZones(List<DeliveryZoneDto> zones) {
    final byId = <String, DeliveryZoneDto>{};
    for (final zone in zones) {
      byId[zone.id] = zone;
    }
    final list = byId.values.toList()
      ..sort((a, b) => a.name.toLowerCase().compareTo(b.name.toLowerCase()));
    return list;
  }

  String _zoneLabel(BuildContext context, DeliveryZoneDto zone,
      {bool withFee = false}) {
    if (!withFee) {
      return zone.name.trim();
    }
    final t = _tr;
    return '${zone.name.trim()} (${t(context, 'Frais', 'Fee')}: ${zone.feeUsd.toStringAsFixed(2)} USD)';
  }
}
