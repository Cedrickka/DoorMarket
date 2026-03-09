import 'package:flutter/material.dart';
import 'package:cached_network_image/cached_network_image.dart';

import '../theme/colors.dart';
import '../theme/radius.dart';

class DmNetworkImage extends StatelessWidget {
  final String? url;
  final double? height;
  final double? width;
  final BoxFit fit;
  final BorderRadius? borderRadius;
  final Alignment alignment;
  final Widget? overlay;
  final int? cacheWidth;
  final int? cacheHeight;
  final bool addRepaintBoundary;

  const DmNetworkImage({
    super.key,
    required this.url,
    this.height,
    this.width,
    this.fit = BoxFit.cover,
    this.borderRadius,
    this.alignment = Alignment.center,
    this.overlay,
    this.cacheWidth,
    this.cacheHeight,
    this.addRepaintBoundary = true,
  });

  @override
  Widget build(BuildContext context) {
    final isDark = Theme.of(context).brightness == Brightness.dark;
    final bg = isDark ? DmColors.surfaceAltDark : DmColors.surfaceAltLight;
    final radius = borderRadius ?? DmRadius.r16;

    final iconColor = isDark ? DmColors.textMutedDark : DmColors.textMutedLight;

    Widget fallback({bool loading = false}) {
      return Container(
        color: bg,
        alignment: Alignment.center,
        child: loading
            ? const SizedBox(
                height: 18,
                width: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              )
            : Icon(Icons.image_outlined, color: iconColor, size: 20),
      );
    }

    return LayoutBuilder(
      builder: (context, constraints) {
        final dpr = MediaQuery.of(context).devicePixelRatio;
        final resolvedCacheWidth = cacheWidth ??
            _resolveCachePx(
              explicitLogicalSize: width,
              constrainedLogicalSize: constraints.maxWidth,
              dpr: dpr,
            );
        final resolvedCacheHeight = cacheHeight ??
            _resolveCachePx(
              explicitLogicalSize: height,
              constrainedLogicalSize: constraints.maxHeight,
              dpr: dpr,
            );

        Widget image;
        if (url == null || url!.trim().isEmpty) {
          image = fallback();
        } else {
          image = CachedNetworkImage(
            imageUrl: url!,
            fit: fit,
            alignment: alignment,
            memCacheWidth: resolvedCacheWidth,
            memCacheHeight: resolvedCacheHeight,
            maxWidthDiskCache: resolvedCacheWidth,
            maxHeightDiskCache: resolvedCacheHeight,
            filterQuality: FilterQuality.low,
            fadeInDuration: Duration.zero,
            fadeOutDuration: Duration.zero,
            placeholder: (_, __) => fallback(loading: true),
            errorWidget: (_, __, ___) => fallback(),
          );
        }

        Widget composed = ClipRRect(
          borderRadius: radius,
          child: SizedBox(
            height: height,
            width: width,
            child: Stack(
              fit: StackFit.expand,
              children: [
                image,
                if (overlay != null) overlay!,
              ],
            ),
          ),
        );

        if (addRepaintBoundary) {
          composed = RepaintBoundary(child: composed);
        }
        return composed;
      },
    );
  }

  static int? _resolveCachePx({
    required double? explicitLogicalSize,
    required double constrainedLogicalSize,
    required double dpr,
  }) {
    double? logical = explicitLogicalSize;
    if (logical == null || !logical.isFinite || logical <= 0) {
      if (constrainedLogicalSize.isFinite && constrainedLogicalSize > 0) {
        logical = constrainedLogicalSize;
      }
    }
    if (logical == null || !logical.isFinite || logical <= 0) {
      return null;
    }

    final px = (logical * dpr).round();
    if (px <= 0) {
      return null;
    }
    return px.clamp(48, 1600);
  }
}
