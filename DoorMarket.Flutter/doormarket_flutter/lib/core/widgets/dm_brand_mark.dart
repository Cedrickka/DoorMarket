import 'package:flutter/material.dart';

class DmBrandMark extends StatelessWidget {
  final double size;
  final bool monochromeWhite;
  final BoxFit fit;

  const DmBrandMark({
    super.key,
    this.size = 30,
    this.monochromeWhite = false,
    this.fit = BoxFit.contain,
  });

  @override
  Widget build(BuildContext context) {
    final logo = Image.asset(
      'assets/images/dm_logo_mark.png',
      fit: fit,
    );

    return SizedBox(
      height: size,
      width: size,
      child: monochromeWhite
          ? ColorFiltered(
              colorFilter: const ColorFilter.mode(
                Colors.white,
                BlendMode.srcIn,
              ),
              child: logo,
            )
          : logo,
    );
  }
}
