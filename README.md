# Webshop

3-lags .NET-solution til webshop-projektet.

## Struktur

- **Webshop.Web** - Blazor Server frontend (præsentationslag)
- **Webshop.Core** - Forretningslogik og interfaces
- **Webshop.Data** - Kommer til at indeholde MongoDB-integration (tom for nu)
- **Webshop.Shared** - Fælles modeller (Product, Customer, Order, CartItem)

## Sådan kommer du i gang

1. Udpak zip-filen
2. Åbn `Webshop.sln` i Visual Studio 2022 (eller nyere)
   - Visual Studio spørger måske om at "restore" NuGet-pakker - lad den gøre det
3. Sæt **Webshop.Web** som startup-projekt (højreklik på projektet -> "Set as Startup Project")
4. Tryk F5 eller "Start" (grøn play-knap)
5. Browseren åbner automatisk på forsiden med de 3 test-produkter

## Krav

- .NET 8 SDK skal være installeret (Visual Studio 2022 v17.8+ installerer det normalt automatisk,
  men tjek under "Individuelle komponenter" hvis det fejler)

## Hvor er data fra?

Lige nu vises data fra `FakeProductService.cs` (Webshop.Core/Services).
Når MongoDB kobles på senere, skifter vi kun én linje i `Program.cs` ud -
resten af koden i frontend rører vi ikke.

## Næste skridt

- Sæt MongoDB Atlas op og byg `Webshop.Data`
- Byg kurv-funktionalitet
- Byg checkout-flow
- Integrér betaling (Quickpay/Stripe)
