# Deploiement DoorMarket sur SmarterASP + domaine Hostinger (pas a pas)

Ce guide publie:
- API: `http://api.door-market.com/`
- Web: `https://door-market.com`

Informations fournies:
- DB: `Data Source=SQL5106.site4now.net;Initial Catalog=db_ac4086_doormarketdb;User Id=db_ac4086_doormarketdb_admin;Password=YOUR_DB_PASSWORD;Encrypt=True;TrustServerCertificate=True;`
- Emails: `admin@door-market.com` et `contact@door-market.com`
- SMTP: `smtp.hostinger.com` port `465` SSL
- IMAP: `imap.hostinger.com` port `993` SSL
- POP: `pop.hostinger.com` port `995` SSL

## 0. Ce dont tu as besoin avant de commencer

- Mot de passe DB (remplacer `YOUR_DB_PASSWORD`).
- Mot de passe de `admin@door-market.com` (SMTP).
- Cles secretes fortes (JWT, AdminSetup, etc.).
- Acces SmarterASP + Hostinger (DNS).

## 1. Preparer la configuration production (API)

Fichier: `DoorMarket.Api/appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=SQL5106.site4now.net;Initial Catalog=db_ac4086_doormarketdb;User Id=db_ac4086_doormarketdb_admin;Password=YOUR_DB_PASSWORD;Encrypt=True;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Issuer": "DoorMarket",
    "Audience": "DoorMarket.Web",
    "Key": "CLE_ULTRA_LONGUE_ET_SECRETE_MIN_32_CHARS",
    "AccessTokenMinutes": 30,
    "RefreshTokenDays": 30
  },
  "Uploads": {
    "Root": "uploads",
    "PublicBaseUrl": "http://api.door-market.com/"
  },
  "Smtp": {
    "Host": "smtp.hostinger.com",
    "Port": 465,
    "EnableSsl": true,
    "Username": "admin@door-market.com",
    "Password": "YOUR_EMAIL_PASSWORD",
    "FromEmail": "admin@door-market.com"
  },
  "PayPal": {
    "ClientId": "",
    "ClientSecret": "",
    "Environment": "live",
    "CheckoutSuccessUrl": "https://door-market.com/checkout/success",
    "CheckoutCancelUrl": "https://door-market.com/checkout/cancel"
  },
  "Stripe": {
    "SecretKey": "",
    "WebhookSecret": "",
    "CheckoutSuccessUrl": "https://door-market.com/checkout/success",
    "CheckoutCancelUrl": "https://door-market.com/checkout/cancel"
  },
  "AdminSetup": {
    "Enabled": true,
    "Key": "CLE_SETUP_TEMPORAIRE_FORTE"
  },
  "UseHttpsRedirection": false
}
```

Notes importantes:
- `Uploads:PublicBaseUrl` doit etre `http://api.door-market.com/`.
- Si tu utilises l URL temporaire SmarterASP (http), mets `UseHttpsRedirection=false` dans l API.
- Choisis une cle JWT robuste (min 32 caracteres).
- Change `AdminSetup:Enabled` a `false` apres creation du premier admin.
- Le bloc `Cors:AllowedOrigins` doit contenir `https://door-market.com` (et `https://www.door-market.com` si tu l utilises).

Si ton API utilise une config CORS (ex: `Cors:AllowedOrigins`), ajoute:
- `https://door-market.com`

## 2. Preparer la configuration production (Web)

Fichier: `DoorMarket.Web/appsettings.Production.json`

```json
{
  "Api": {
    "BaseUrl": "http://api.door-market.com/"
  }
}
```

## 3. Publier en local (build release)

```powershell
dotnet publish DoorMarket.Api/DoorMarket.Api.csproj -c Release -o .\publish\api

dotnet publish DoorMarket.Web/DoorMarket.Web.csproj -c Release -o .\publish\web
```

## 4. Creer les sites sur SmarterASP

Dans le panneau SmarterASP:
1. Cree un site pour l API (ex: `apidoormarket`).
2. Cree un site pour le Web avec le domaine `door-market.com`.
3. Verifie que les deux sites pointent vers des dossiers IIS differents.
4. Pour chaque site:
   - .NET version: `8`
   - Pipeline: `Integrated`
   - App Pool: `No Managed Code`

## 5. Ajouter ASPNETCORE_ENVIRONMENT = Production

Deux options:

Option A (via SmarterASP UI):
- Ajoute la variable d environnement `ASPNETCORE_ENVIRONMENT=Production` pour chaque site.

Option B (via web.config):
Dans `publish\api\web.config` et `publish\web\web.config`, ajoute:

```xml
<environmentVariables>
  <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
</environmentVariables>
```

Note: j ai ajoute un `web.config` dans `DoorMarket.Web` avec une redirection HTTPS. Pour l API (URL temporaire http), pas de redirection HTTPS.

## 6. Deployer sur SmarterASP

Option A (Visual Studio Web Deploy):
- Publish API vers le site API (ex: `apidoormarket`).
- Publish Web vers le site `door-market.com`.

Option B (FTP):
- Upload `publish\api` -> dossier IIS de l API.
- Upload `publish\web` -> dossier IIS du Web.

## 7. DNS Hostinger (domaine door-market.com)

Dans Hostinger > DNS Zone:
1. Pour `@` (root):
   - Type A vers l IP fournie par SmarterASP.
2. Pour `api` (optionnel):
   - A utiliser seulement si tu veux un sous-domaine public `api.door-market.com`.
   - Type CNAME vers le host SmarterASP (ex: `xxxxx.smarterasp.net`).
3. Pour `www`:
   - Type CNAME vers `door-market.com`.

Attends la propagation DNS (quelques minutes a 24h).

## 8. Activer SSL (HTTPS)

Dans SmarterASP:
- Active le SSL gratuit pour `door-market.com`.
- Si tu actives aussi `api.door-market.com`, active le SSL pour ce sous-domaine.
- Force le HTTPS si option disponible.

## 9. Migrations base de donnees

Execute les migrations vers la base SmarterASP:

```powershell
dotnet ef database update `
  --project DoorMarket.Infrastructure `
  --startup-project DoorMarket.Api `
  --configuration Release
```

Assure-toi que la connection string pointe bien vers la DB prod.

## 10. Creation du premier admin

Endpoint:

```txt
POST http://api.door-market.com/api/setup/create-user
```

Header:

```txt
X-Setup-Key: <AdminSetup:Key>
```

Body:

```json
{
  "email": "admin@door-market.com",
  "password": "MotDePasseFort#2026",
  "role": 0,
  "phone": "+243..."
}
```

Apres creation:
1. Mettre `AdminSetup:Enabled` a `false`.
2. Republier l API ou recycler l App Pool.

## 11. Configuration des emails (Hostinger)

Dans l application, utilise SMTP:
- Host: `smtp.hostinger.com`
- Port: `465`
- SSL: `true`
- User: `admin@door-market.com`
- Password: mot de passe du compte

Sur Hostinger, verifie que les enregistrements MX sont bien actifs pour `door-market.com`.

## 12. Configuration mobile apres prod

Fichier: `DoorMarket.Mobile/appsettings.json`

```json
{
  "Api": {
    "BaseUrl": "http://api.door-market.com/"
  }
}
```

Puis rebuild mobile.

## 13. Verification post-deploiement

1. Web: `https://door-market.com` s ouvre.
2. API: `http://api.door-market.com/api/health/config` (si health active).
3. Upload image produit ok.
4. Connexion client/admin ok.
5. Checkout ok.
6. Emails sortants (SMTP) ok.

## 14. Checklist securite

1. Ne jamais committer les secrets.
2. HTTPS actif sur les deux domaines.
3. JWT key forte.
4. `AdminSetup:Enabled = false` apres bootstrap.
5. Sauvegardes DB regulieres.

## 15. Secrets via variables d environnement (recommande)

Pour eviter d ecrire les secrets dans les fichiers, tu peux les definir dans SmarterASP (Environment Variables).

Exemples:
- `ConnectionStrings__DefaultConnection`
- `Jwt__Key`
- `Smtp__Password`
- `AdminSetup__Key`

Note: les variables d environnement remplacent automatiquement les valeurs des fichiers JSON.

### Pas a pas (SmarterASP)

1. Ouvre le panneau SmarterASP.
2. Va dans ton site API (ex: `apidoormarket`) ou ton site Web.
3. Cherche la section **Environment Variables**.
4. Ajoute une variable par ligne avec la syntaxe suivante:
   - **Name** = `ConnectionStrings__DefaultConnection`
   - **Value** = `Data Source=SQL5106.site4now.net;Initial Catalog=db_ac4086_doormarketdb;User Id=db_ac4086_doormarketdb_admin;Password=YOUR_DB_PASSWORD;Encrypt=True;TrustServerCertificate=True;`
5. Ajoute ensuite:
   - `Jwt__Key` = `TA_CLE_JWT_TRES_LONGUE`
   - `Smtp__Password` = `MOT_DE_PASSE_ADMIN_EMAIL`
   - `AdminSetup__Key` = `TA_CLE_SETUP_FORTE`
6. Sauvegarde.
7. Redemarre le site (Recycle App Pool / Restart).

Astuce:
- Si tu veux separer les secrets de l API et du Web, ne mets que les variables necessaires sur chaque site.

### Tableau recapitulatif (API vs Web)

Site API (ex: `apidoormarket`):
- Publier: `DoorMarket.Api`
- Variables d environnement essentielles:
  - `ConnectionStrings__DefaultConnection`
  - `Jwt__Key`
  - `Smtp__Password`
  - `AdminSetup__Key`
- URL publique: `http://api.door-market.com/`

Site `door-market.com`:
- Publier: `DoorMarket.Web`
- Variables d environnement utiles:
  - `Api__BaseUrl` = `http://api.door-market.com/`
- URL publique: `https://door-market.com`


