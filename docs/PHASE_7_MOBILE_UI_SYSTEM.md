# DoorMarket - Phase 7 (Mobile UI System)

Date: 2026-03-01

## Objectif
- Aligner l'experience mobile sur la coherence web recente.
- Uniformiser les tokens visuels (surfaces, bordures, top/bottom bars, typographie metrique).
- Corriger les ecrans critiques commerce: cart, checkout, product details, home, shops.

## 1) Design tokens mobile

### Couleurs semantic ajoutees
- `Resources/Styles/Colors.xaml`:
  - top bar / bottom bar (`DmTopBar*`, `DmBottomBar*`)
  - hero gradients (`DmHeroStart*`, `DmHeroEnd*`)
  - fonds semantiques soft (`DmSuccessSoft*`, `DmWarningSoft*`, `DmErrorSoft*`)

### Styles communs ajoutes
- `Resources/Styles/Styles.xaml`:
  - `TopSurfaceGridStyle`
  - `BottomSurfaceGridStyle`
  - `MetricValueLabelStyle`
  - `MetricLabelStyle`
  - `ChipFrameStyle`
  - `SummaryRowLabelStyle`
  - `SummaryValueLabelStyle`
- Ajustement `GhostButtonStyle` pour meilleur rendu en dark mode.

## 2) Ecrans mobile harmonises

### Cart
- `Pages/CartPage.xaml`:
  - total principal plus lisible (style metrique),
  - chips et summary homogenises,
  - suppression des couleurs fixes (`White`, `SoftGray`) vers palette dynamique,
  - footer action aligner sur style surface bas.
- `Pages/CartPage.xaml.cs`:
  - normalisation des couleurs message promo via helper semantic.

### Checkout
- `Pages/CheckoutPage.xaml`:
  - top bar + bottom bar harmonises,
  - section summary unifiee (labels/valeurs),
  - etapes et cartes paiement mieux contrastees en dark mode.
- `Pages/CheckoutPage.xaml.cs`:
  - migration des couleurs runtime vers tokens semantiques (helper `ResolveColor`),
  - suppression des dependances directes a `White/SoftGray/DarkGray`.

### Product details
- `Pages/ProductDetailsPage.xaml`:
  - top/bottom surfaces harmonisees,
  - carte media et texte adaptes light/dark.

### Home
- `Pages/HomePage.xaml`:
  - cartes promos/categories/shops converties vers surfaces semantiques,
  - gradients hero relies aux tokens,
  - boutons "View all" et textes secondaires mieux adaptes au dark mode.

### Shops
- `Pages/ShopsPage.xaml`:
  - chips de filtre et cards shop adaptees au theming dynamique.

### Composants partages
- `Components/DmBottomNav.xaml`: fond dynamique (light/dark).
- `Components/DmHeader.xaml`: subtitle mieux lisible en mode sombre.

## 3) Validation technique

- Build execute:
  - `dotnet build DoorMarket.Mobile/DoorMarket.Mobile.csproj -f net8.0-windows10.0.19041.0`
  - resultat: OK, 0 erreur.

## 4) Tests manuels recommandes (mobile)

1. Home (light + dark):
   - verifier rendu hero, cards promotion, categories, shops.
2. Shops:
   - verifier chips filtre (normal/selected) et contraste texte en dark mode.
3. Product details:
   - verifier top bar, card image, section quantite et footer action.
4. Cart:
   - verifier lisibilite du total, summary subtotal/discount/delivery/total.
5. Cart:
   - verifier boutons quantite/suppression sur themes light et dark.
6. Checkout:
   - verifier sections adresse/paiement/summary et etat bouton final.
7. Checkout:
   - tester selection PayPal vs Prepaid et verifier contraste des cards.
8. Global:
   - ouvrir/fermer l'app apres changement de theme (Settings) et verifier persistance.
