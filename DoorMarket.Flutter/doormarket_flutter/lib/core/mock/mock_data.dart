class Category {
  final String name;
  final String emoji;

  const Category(this.name, this.emoji);
}

class Product {
  final String name;
  final String unit;
  final double price;
  final double? oldPrice;
  final String imageUrl;
  final int discount;
  final double rating;

  const Product({
    required this.name,
    required this.unit,
    required this.price,
    this.oldPrice,
    required this.imageUrl,
    required this.discount,
    required this.rating,
  });
}

class OrderSummary {
  final String code;
  final String status;
  final String subtitle;
  final double total;
  final String imageUrl;

  const OrderSummary({
    required this.code,
    required this.status,
    required this.subtitle,
    required this.total,
    required this.imageUrl,
  });
}

const categories = [
  Category('Fruits', '🍌'),
  Category('Legumes', '🥦'),
  Category('Boissons', '🥤'),
  Category('Viande', '🥩'),
  Category('Boulangerie', '🥖'),
  Category('Epices', '🧂'),
  Category('Desserts', '🧁'),
  Category('Poisson', '🐟'),
];

const promoProducts = [
  Product(
    name: 'Avocats',
    unit: '500 g',
    price: 2.39,
    oldPrice: 3.10,
    imageUrl: 'https://images.unsplash.com/photo-1524593656068-fbac72624f63?auto=format&fit=crop&w=600&q=80',
    discount: 30,
    rating: 4.8,
  ),
  Product(
    name: 'Tomates',
    unit: '1 kg',
    price: 1.89,
    oldPrice: 2.50,
    imageUrl: 'https://images.unsplash.com/photo-1567306226416-28f0efdc88ce?auto=format&fit=crop&w=600&q=80',
    discount: 20,
    rating: 4.6,
  ),
  Product(
    name: 'Bananes',
    unit: '1 kg',
    price: 1.20,
    oldPrice: 1.60,
    imageUrl: 'https://images.unsplash.com/photo-1574226516831-e1dff420e43e?auto=format&fit=crop&w=600&q=80',
    discount: 25,
    rating: 4.7,
  ),
];

const featuredProducts = [
  Product(
    name: 'Florida Strawberries',
    unit: '500 g',
    price: 2.99,
    oldPrice: 3.99,
    imageUrl: 'https://images.unsplash.com/photo-1464965911861-746a04b4bca6?auto=format&fit=crop&w=900&q=80',
    discount: 30,
    rating: 4.9,
  ),
  Product(
    name: 'Pain de campagne',
    unit: '400 g',
    price: 1.40,
    oldPrice: 1.80,
    imageUrl: 'https://images.unsplash.com/photo-1509440159596-0249088772ff?auto=format&fit=crop&w=900&q=80',
    discount: 15,
    rating: 4.5,
  ),
  Product(
    name: 'Poulet frais',
    unit: '1.2 kg',
    price: 6.80,
    oldPrice: 7.50,
    imageUrl: 'https://images.unsplash.com/photo-1604908554169-4417b42b6c73?auto=format&fit=crop&w=900&q=80',
    discount: 10,
    rating: 4.4,
  ),
];

const sampleOrders = [
  OrderSummary(
    code: 'DM83921',
    status: 'En cours',
    subtitle: 'Livraison rapide - 2 items',
    total: 10.37,
    imageUrl: 'https://images.unsplash.com/photo-1464965911861-746a04b4bca6?auto=format&fit=crop&w=300&q=80',
  ),
  OrderSummary(
    code: 'DM83914',
    status: 'Livre',
    subtitle: 'Marché Douala - 4 items',
    total: 22.80,
    imageUrl: 'https://images.unsplash.com/photo-1506806732259-39c2d0268443?auto=format&fit=crop&w=300&q=80',
  ),
  OrderSummary(
    code: 'DM83902',
    status: 'Livre',
    subtitle: 'Market Express - 1 item',
    total: 34.04,
    imageUrl: 'https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=300&q=80',
  ),
];
