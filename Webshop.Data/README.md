# Webshop.Data

Dette lag skal indeholde MongoDB-forbindelsen og repositories, når vi
kobler den rigtige database på (næste skridt efter frontend virker med
fake-data).

Kommer til at indeholde:

- `MongoDbContext.cs` - opsætning af forbindelse til MongoDB Atlas
- `ProductRepository.cs` - implementerer `IProductRepository` fra Core
- `CustomerRepository.cs`
- `OrderRepository.cs`

Ingen kode herinde endnu - det er med vilje. Vi bygger det, når
frontend + fake-data virker end-to-end.
