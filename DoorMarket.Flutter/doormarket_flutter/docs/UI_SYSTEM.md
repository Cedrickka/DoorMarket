# DoorMarket Flutter - UI System

## Theme foundation
- Colors: `lib/core/theme/colors.dart`
- Spacing: `lib/core/theme/spacing.dart`
- Radius: `lib/core/theme/radius.dart`
- Typography: `lib/core/theme/typography.dart`
- Global theme: `lib/core/theme/theme.dart`

## Shared widgets (use first)
- Header: `DmHeader`, `DmPageHeader`
- Search: `DmSearchBar`
- Cards: `DmCard`
- Buttons: `DmPrimaryButton`, `DmSecondaryButton`, `DmGhostButton`
- Bottom nav: `DmBottomNav`

## Consistency rules
1. Do not hardcode random colors; use `DmColors`.
2. Use `DmSpacing` scale for margins/paddings.
3. Keep card radius/shadow from theme + shared widgets.
4. Prefer shared header components for page top sections.
5. Keep empty/loading/error states explicit and readable.

## Performance rules
1. Use lazy lists/grids for long datasets.
2. Avoid heavy sync work in `build`.
3. Keep image placeholders and caching for network images.
