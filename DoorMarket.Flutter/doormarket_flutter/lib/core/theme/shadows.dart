import 'package:flutter/material.dart';

class DmShadows {
  static const light = [
    BoxShadow(
      color: Color(0x0D000000),
      offset: Offset(0, 4),
      blurRadius: 10,
    ),
  ];

  static const dark = [
    BoxShadow(
      color: Color(0x4A000000),
      offset: Offset(0, 5),
      blurRadius: 12,
    ),
  ];
}
