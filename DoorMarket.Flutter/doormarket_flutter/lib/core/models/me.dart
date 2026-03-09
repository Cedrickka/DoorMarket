class MeDto {
  final String id;
  final String email;
  final String? phone;
  final String? profileImageUrl;
  final String role;
  final bool isActive;

  MeDto({
    required this.id,
    required this.email,
    this.phone,
    this.profileImageUrl,
    required this.role,
    required this.isActive,
  });

  factory MeDto.fromJson(Map<String, dynamic> json) {
    return MeDto(
      id: json['id'] as String,
      email: json['email'] as String,
      phone: json['phone'] as String?,
      profileImageUrl: json['profileImageUrl'] as String?,
      role: json['role'] as String? ?? '',
      isActive: json['isActive'] as bool? ?? false,
    );
  }
}

class UpdateMeRequest {
  final String? phone;

  UpdateMeRequest({this.phone});

  Map<String, dynamic> toJson() => {'phone': phone};
}

class RequestPhoneChangeRequest {
  final String? phone;

  RequestPhoneChangeRequest({this.phone});

  Map<String, dynamic> toJson() => {'phone': phone};
}

class ConfirmPhoneChangeRequest {
  final String code;

  ConfirmPhoneChangeRequest({required this.code});

  Map<String, dynamic> toJson() => {'code': code};
}

class RequestPasswordChangeRequest {
  final String password;

  RequestPasswordChangeRequest({required this.password});

  Map<String, dynamic> toJson() => {'password': password};
}

class ConfirmPasswordChangeRequest {
  final String code;

  ConfirmPasswordChangeRequest({required this.code});

  Map<String, dynamic> toJson() => {'code': code};
}

class ProfileImageUploadResponse {
  final String url;

  ProfileImageUploadResponse({required this.url});

  factory ProfileImageUploadResponse.fromJson(Map<String, dynamic> json) {
    return ProfileImageUploadResponse(url: json['url'] as String);
  }
}

class RegisterPushDeviceRequest {
  final String? platform;
  final String? token;
  final String? deviceId;
  final String? deviceModel;
  final String? appVersion;

  RegisterPushDeviceRequest({
    required this.platform,
    required this.token,
    this.deviceId,
    this.deviceModel,
    this.appVersion,
  });

  Map<String, dynamic> toJson() => {
        'platform': platform,
        'token': token,
        'deviceId': deviceId,
        'deviceModel': deviceModel,
        'appVersion': appVersion,
      };
}

class PushDeviceDto {
  final String id;
  final String platform;
  final String? deviceId;
  final String? deviceModel;
  final String? appVersion;
  final bool isActive;
  final DateTime? lastSeenAtUtc;

  PushDeviceDto({
    required this.id,
    required this.platform,
    required this.deviceId,
    required this.deviceModel,
    required this.appVersion,
    required this.isActive,
    required this.lastSeenAtUtc,
  });

  factory PushDeviceDto.fromJson(Map<String, dynamic> json) {
    return PushDeviceDto(
      id: json['id'] as String? ?? '',
      platform: json['platform'] as String? ?? '',
      deviceId: json['deviceId'] as String?,
      deviceModel: json['deviceModel'] as String?,
      appVersion: json['appVersion'] as String?,
      isActive: json['isActive'] as bool? ?? false,
      lastSeenAtUtc: DateTime.tryParse(json['lastSeenAtUtc']?.toString() ?? ''),
    );
  }
}

class AddressDto {
  final String id;
  final String label;
  final String fullName;
  final String phone;
  final String country;
  final String city;
  final String district;
  final String? deliveryZoneId;
  final String? deliveryZoneName;
  final String street;
  final String? landmark;
  final bool isDefault;

  AddressDto({
    required this.id,
    required this.label,
    required this.fullName,
    required this.phone,
    required this.country,
    required this.city,
    required this.district,
    this.deliveryZoneId,
    this.deliveryZoneName,
    required this.street,
    this.landmark,
    required this.isDefault,
  });

  factory AddressDto.fromJson(Map<String, dynamic> json) {
    return AddressDto(
      id: json['id'] as String,
      label: json['label'] as String,
      fullName: json['fullName'] as String,
      phone: json['phone'] as String,
      country: json['country'] as String,
      city: json['city'] as String,
      district: json['district'] as String,
      deliveryZoneId: json['deliveryZoneId'] as String?,
      deliveryZoneName: json['deliveryZoneName'] as String?,
      street: json['street'] as String,
      landmark: json['landmark'] as String?,
      isDefault: json['isDefault'] as bool? ?? false,
    );
  }
}

class CreateAddressRequest {
  final String label;
  final String fullName;
  final String phone;
  final String country;
  final String city;
  final String district;
  final String? deliveryZoneId;
  final String street;
  final String? landmark;
  final bool isDefault;

  CreateAddressRequest({
    required this.label,
    required this.fullName,
    required this.phone,
    required this.country,
    required this.city,
    required this.district,
    this.deliveryZoneId,
    required this.street,
    this.landmark,
    required this.isDefault,
  });

  Map<String, dynamic> toJson() => {
        'label': label,
        'fullName': fullName,
        'phone': phone,
        'country': country,
        'city': city,
        'district': district,
        'deliveryZoneId': deliveryZoneId,
        'street': street,
        'landmark': landmark,
        'isDefault': isDefault,
      };
}

class UpdateAddressRequest extends CreateAddressRequest {
  UpdateAddressRequest({
    required super.label,
    required super.fullName,
    required super.phone,
    required super.country,
    required super.city,
    required super.district,
    super.deliveryZoneId,
    required super.street,
    super.landmark,
    required super.isDefault,
  });
}
