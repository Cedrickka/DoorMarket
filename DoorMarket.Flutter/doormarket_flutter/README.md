# DoorMarket Flutter UI Prototype

This is a **UI-only** Flutter prototype to compare the visual rendering with the existing .NET MAUI app.
No business logic, networking, or persistence is wired.

## Run
```bash
cd DoorMarket.Flutter/doormarket_flutter
flutter pub get
flutter run --dart-define=API_BASE_URL=http://api.door-market.com
```

## Structure
```
lib/
  core/config/
  core/theme/
  core/widgets/
  core/mock/
  features/
```

## Notes
- Inter fonts are loaded from `assets/fonts/` (copied from MAUI resources).
- Light/Dark themes are implemented and can be toggled in Profile.
- `UiGallery` showcases all UI components in both themes.
```

## Theme
Design tokens follow the DoorMarket UI kit:
- DoorOrange: #FF7A00
- DoorBlue:   #002D5E
- Light/Dark palettes, radius, spacing, shadows per spec.
