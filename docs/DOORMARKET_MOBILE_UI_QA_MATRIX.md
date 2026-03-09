# DoorMarket Mobile UI QA Matrix (Sprint 6)

## 1. Purpose
This matrix is used by support L1/L2 and QA to validate visual consistency and usability in light and dark modes.

## 2. Test dimensions
- Theme: Light / Dark
- Language: FR / EN
- Font scaling: default and large text
- Device width: small phone and large phone
- Network: online / intermittent

## 3. Screen checklist

### Home
- Header spacing remains stable while scrolling.
- Search bar remains visible and usable.
- Product cards have no overflow.
- Review count and muted text stay readable in dark mode.

### Categories
- Product grid remains compact without clipped text.
- Search filter updates list in place.
- Rating/review text keeps sufficient contrast.

### Shops / Shop products
- Fixed header and search remain usable.
- Category filters and product cards keep alignment.
- Action icons remain readable in dark mode.

### Cart
- No `BOTTOM OVERFLOWED` on long names.
- Quantity controls and remove button remain aligned.
- Sticky checkout bar remains visible.

### Checkout
- Address selection chips and labels remain readable in dark mode.
- Payment and summary blocks have consistent spacing.

### Orders / Order details
- Status chips are readable in dark mode.
- Payment action states (pending, failed, paid) are visually distinct.
- Timeline and amount blocks remain aligned.

### Returns
- Return form fields and status badges are readable in dark mode.
- Timeline modal has no clipped text.

### Notifications
- Empty state, unread state, and refresh state all render correctly.
- No layout exceptions after pull-to-refresh.
- Severity badges stay readable in dark mode.

### Profile
- Avatar fallback icon and menu icons are visible in dark mode.
- Error/success messages use accessible contrast.

### Addresses / Payments / Settings
- Secondary text is readable in dark mode.
- Error messages are not pure hardcoded red on dark backgrounds.

## 4. Exit criteria
- Zero Flutter layout assertions in smoke run.
- `flutter analyze` clean.
- No unreadable icon/text in dark mode across the screens above.

## 5. Branding smoke reference
- For logo/icon/favicons validation across Web + Flutter + MAUI, run:
- `docs/DM60_BRANDING_QA_SMOKE_WEB_FLUTTER_MAUI.md`
