# SmarterASP - Production API Config (DoorMarket)

Ce template est pret a copier dans SmarterASP (Environment Variables) ou dans `web.config`.

## 1) Variables obligatoires

```text
ASPNETCORE_ENVIRONMENT=Production

ConnectionStrings__DefaultConnection=Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;
Jwt__Key=METS_ICI_UNE_CLE_TRES_LONGUE_32_CHARS_MINIMUM

Cors__AllowedOrigins__0=https://door-market.com
Cors__AllowedOrigins__1=https://www.door-market.com
Cors__AllowedOrigins__2=https://api.door-market.com

Smtp__Host=smtp.hostinger.com
Smtp__Port=465
Smtp__EnableSsl=true
Smtp__TimeoutSeconds=12
Smtp__Username=admin@door-market.com
Smtp__Password=METS_ICI_LE_MDP_SMTP_ADMIN
Smtp__FromEmail=admin@door-market.com
Smtp__FromName=DoorMarket

Support__Email=contact@door-market.com
Support__Smtp__Host=smtp.hostinger.com
Support__Smtp__Port=465
Support__Smtp__EnableSsl=true
Support__Smtp__TimeoutSeconds=12
Support__Smtp__Username=contact@door-market.com
Support__Smtp__Password=METS_ICI_LE_MDP_SMTP_SUPPORT
Support__Smtp__FromEmail=contact@door-market.com

AdminSetup__Enabled=true
AdminSetup__Key=METS_ICI_UNE_CLE_SETUP_TRES_LONGUE_32_CHARS_MINIMUM
```

## 2) Paiements (si utilises)

```text
Stripe__SecretKey=sk_live_...
Stripe__WebhookSecret=whsec_...

PayPal__ClientId=...
PayPal__ClientSecret=...
PayPal__Environment=live
```

## 3) Uploads (recommande)

```text
Uploads__Root=uploads
Uploads__PublicBaseUrl=https://api.door-market.com
UseHttpsRedirection=false
```

## 4) Bloc web.config (alternative)

```xml
<aspNetCore processPath="dotnet" arguments="DoorMarket.Api.dll" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout">
  <environmentVariables>
    <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Production" />
    <environmentVariable name="ConnectionStrings__DefaultConnection" value="Server=...;Database=...;User Id=...;Password=...;Encrypt=True;TrustServerCertificate=True;" />
    <environmentVariable name="Jwt__Key" value="METS_ICI_UNE_CLE_TRES_LONGUE_32_CHARS_MINIMUM" />
    <environmentVariable name="Cors__AllowedOrigins__0" value="https://door-market.com" />
    <environmentVariable name="Cors__AllowedOrigins__1" value="https://www.door-market.com" />
    <environmentVariable name="Cors__AllowedOrigins__2" value="https://api.door-market.com" />

    <environmentVariable name="Smtp__Host" value="smtp.hostinger.com" />
    <environmentVariable name="Smtp__Port" value="465" />
    <environmentVariable name="Smtp__EnableSsl" value="true" />
    <environmentVariable name="Smtp__TimeoutSeconds" value="12" />
    <environmentVariable name="Smtp__Username" value="admin@door-market.com" />
    <environmentVariable name="Smtp__Password" value="METS_ICI_LE_MDP_SMTP_ADMIN" />
    <environmentVariable name="Smtp__FromEmail" value="admin@door-market.com" />
    <environmentVariable name="Smtp__FromName" value="DoorMarket" />

    <environmentVariable name="Support__Email" value="contact@door-market.com" />
    <environmentVariable name="Support__Smtp__Host" value="smtp.hostinger.com" />
    <environmentVariable name="Support__Smtp__Port" value="465" />
    <environmentVariable name="Support__Smtp__EnableSsl" value="true" />
    <environmentVariable name="Support__Smtp__TimeoutSeconds" value="12" />
    <environmentVariable name="Support__Smtp__Username" value="contact@door-market.com" />
    <environmentVariable name="Support__Smtp__Password" value="METS_ICI_LE_MDP_SMTP_SUPPORT" />
    <environmentVariable name="Support__Smtp__FromEmail" value="contact@door-market.com" />

    <environmentVariable name="AdminSetup__Enabled" value="true" />
    <environmentVariable name="AdminSetup__Key" value="METS_ICI_UNE_CLE_SETUP_TRES_LONGUE_32_CHARS_MINIMUM" />

    <environmentVariable name="Uploads__Root" value="uploads" />
    <environmentVariable name="Uploads__PublicBaseUrl" value="https://api.door-market.com" />
    <environmentVariable name="UseHttpsRedirection" value="false" />
  </environmentVariables>
</aspNetCore>
```

## 5) Verification apres deploy

1. Redemarrer App Pool / site SmarterASP.
2. Tester:
   - `/ping`
   - `/api/health/live`
   - `/api/health/ready`
3. Ouvrir Swagger et tester un endpoint auth.
