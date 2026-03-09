class CategoryDto {
  final String id;
  final String name;
  final String slug;
  final DateTime createdAtUtc;
  final String? nameEn;

  CategoryDto({
    required this.id,
    required this.name,
    required this.slug,
    required this.createdAtUtc,
    this.nameEn,
  });

  factory CategoryDto.fromJson(Map<String, dynamic> json) {
    return CategoryDto(
      id: json['id'] as String,
      name: json['name'] as String,
      slug: json['slug'] as String,
      createdAtUtc: DateTime.parse(json['createdAtUtc'] as String),
      nameEn: json['nameEn'] as String?,
    );
  }
}
