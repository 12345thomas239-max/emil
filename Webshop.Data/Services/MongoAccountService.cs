using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Webshop.Core.Interfaces;
using Webshop.Shared.Models;

namespace Webshop.Data.Services;

public sealed class MongoAccountService : IAccountService
{
    private const int PasswordIterations = 120_000;

    private readonly IMongoCollection<BsonDocument> _mongoCollection;
    private readonly string? _adminEmailConfig;
    private readonly string? _adminPasswordConfig;

    public AccountView? CurrentAccount { get; private set; }

    public event Action? OnChange;

    private void NotifyChange() => OnChange?.Invoke();

    public MongoAccountService(IConfiguration configuration)
    {
        _adminEmailConfig = configuration["AdminAccount:Email"];
        _adminPasswordConfig = configuration["AdminAccount:Password"];

        var mongoConnectionString = configuration.GetConnectionString("MongoDb")
            ?? configuration["MongoDb:ConnectionString"]
            ?? configuration["MongoDb__ConnectionString"]
            ?? Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING");

        var databaseName = configuration["MongoDb:DatabaseName"]
            ?? configuration["MongoDb__DatabaseName"]
            ?? "webshop";

        if (string.IsNullOrWhiteSpace(mongoConnectionString))
        {
            throw new InvalidOperationException("MongoDB connection string is missing. Set MongoDb:ConnectionString or MONGODB_CONNECTION_STRING.");
        }

        try
        {
            var client = new MongoClient(mongoConnectionString);
            var database = client.GetDatabase(databaseName);
            _mongoCollection = database.GetCollection<BsonDocument>("accounts");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Unable to connect to MongoDB. Check the connection string, network access, and MongoDB server status.", ex);
        }

        SeedDefaultAccounts();
    }

    public bool Register(string firstName, string lastName, string phone, string email, string password, string address, string postalCode, string city, out string errorMessage)
    {
        errorMessage = string.Empty;

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(postalCode) || string.IsNullOrWhiteSpace(city))
        {
            errorMessage = "Please fill out all required fields.";
            return false;
        }

        if (password.Length < 8)
        {
            errorMessage = "Password must be at least 8 characters.";
            return false;
        }

        var existingDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail)).FirstOrDefault();
        if (existingDoc is not null)
        {
            errorMessage = "That email is already in use.";
            return false;
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var account = new AccountRecord
        {
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Phone = phone.Trim(),
            Email = normalizedEmail,
            PasswordSaltBase64 = Convert.ToBase64String(salt),
            PasswordHash = HashPassword(password, salt, PasswordIterations),
            PasswordIterations = PasswordIterations,
            MemberSince = DateTime.UtcNow,
            IsAdmin = false,
            Address = address.Trim(),
            PostalCode = postalCode.Trim(),
            City = city.Trim(),
            Orders = new List<OrderView>()
        };

        PersistAccount(account);
        CurrentAccount = ToAccountView(account);
        NotifyChange();
        return true;
    }

    public bool Login(string email, string password, out string errorMessage)
    {
        errorMessage = string.Empty;
        var normalizedEmail = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(password))
        {
            errorMessage = "Enter your email and password.";
            return false;
        }

        var accountDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail)).FirstOrDefault();
        var account = accountDoc is null ? null : MapToAccountRecord(accountDoc);
        if (account is null)
        {
            errorMessage = "No account found for that email.";
            return false;
        }

        if (!VerifyPassword(password, account))
        {
            errorMessage = "Incorrect email or password.";
            return false;
        }

        CurrentAccount = ToAccountView(account);
        NotifyChange();
        return true;
    }

    public bool RequestPasswordReset(string email, out string errorMessage, out string resetToken)
    {
        errorMessage = string.Empty;
        resetToken = string.Empty;

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            errorMessage = "Enter your email address.";
            return false;
        }

        var accountDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail)).FirstOrDefault();
        if (accountDoc is null)
        {
            errorMessage = "If that email exists, a reset link will be sent.";
            return true;
        }

        var account = MapToAccountRecord(accountDoc);
        resetToken = GeneratePasswordResetToken();
        account.ResetToken = resetToken;
        account.ResetTokenExpiresAt = DateTime.UtcNow.AddHours(1);
        PersistAccount(account);

        if (CurrentAccount is not null && string.Equals(NormalizeEmail(CurrentAccount.Email), normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            CurrentAccount = ToAccountView(account);
            NotifyChange();
        }

        return true;
    }

    public bool ResetPassword(string email, string resetToken, string newPassword, out string errorMessage)
    {
        errorMessage = string.Empty;

        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(resetToken))
        {
            errorMessage = "Invalid reset link.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 8)
        {
            errorMessage = "Password must be at least 8 characters.";
            return false;
        }

        var accountDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail)).FirstOrDefault();
        if (accountDoc is null)
        {
            errorMessage = "Invalid reset link.";
            return false;
        }

        var account = MapToAccountRecord(accountDoc);
        if (!string.Equals(account.ResetToken, resetToken, StringComparison.Ordinal) || account.ResetTokenExpiresAt < DateTime.UtcNow)
        {
            errorMessage = "Invalid or expired reset link.";
            return false;
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        account.PasswordSaltBase64 = Convert.ToBase64String(salt);
        account.PasswordHash = HashPassword(newPassword, salt, PasswordIterations);
        account.PasswordIterations = PasswordIterations;
        account.ResetToken = string.Empty;
        account.ResetTokenExpiresAt = default;
        PersistAccount(account);

        if (CurrentAccount is not null && string.Equals(NormalizeEmail(CurrentAccount.Email), normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            CurrentAccount = ToAccountView(account);
            NotifyChange();
        }

        return true;
    }

    public void Logout()
    {
        CurrentAccount = null;
        NotifyChange();
    }

    public bool UpdateProfile(
        string firstName,
        string lastName,
        string phone,
        string email,
        out string errorMessage,
        string address,
        string postalCode,
        string city,
        string newPassword)
    {
        errorMessage = string.Empty;

        if (CurrentAccount is null)
        {
            errorMessage = "You need to be logged in first.";
            return false;
        }

        var currentKey = NormalizeEmail(CurrentAccount.Email);
        var accountDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", currentKey)).FirstOrDefault();
        var account = accountDoc is null ? null : MapToAccountRecord(accountDoc);
        if (account is null)
        {
            errorMessage = "Current account could not be found.";
            return false;
        }

        var newEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(newEmail))
        {
            errorMessage = "Please fill out all required fields.";
            return false;
        }

        // Address is stored as provided; no external verification required.

        if (!string.IsNullOrWhiteSpace(newEmail) && !string.Equals(newEmail, currentKey, StringComparison.OrdinalIgnoreCase))
        {
            var alreadyDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", newEmail)).FirstOrDefault();
            if (alreadyDoc is not null)
            {
                errorMessage = "That email is already in use.";
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(newPassword) && newPassword.Length < 8)
        {
            errorMessage = "Password must be at least 8 characters.";
            return false;
        }

        var updatedAccount = account with
        {
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Phone = phone.Trim(),
            Email = newEmail,
            Address = address.Trim(),
            PostalCode = postalCode.Trim(),
            City = city.Trim()
        };

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            updatedAccount.PasswordSaltBase64 = Convert.ToBase64String(salt);
            updatedAccount.PasswordHash = HashPassword(newPassword, salt, PasswordIterations);
            updatedAccount.PasswordIterations = PasswordIterations;
        }

        PersistAccount(updatedAccount, currentKey);
        CurrentAccount = ToAccountView(updatedAccount);
        NotifyChange();
        return true;
    }

    private void SeedDefaultAccounts()
    {
        var demoEmail = NormalizeEmail("demo@osarkiv.dk");
        var demoDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", demoEmail)).FirstOrDefault();
        if (demoDoc is null)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var demoAccount = new AccountRecord
            {
                FirstName = "Demo",
                LastName = "Customer",
                Phone = "+45 12 34 56 78",
                Email = demoEmail,
                PasswordSaltBase64 = Convert.ToBase64String(salt),
                PasswordHash = HashPassword("DemoPass8", salt, PasswordIterations),
                PasswordIterations = PasswordIterations,
                MemberSince = new DateTime(2026, 7, 1),
                IsAdmin = false,
                Orders = new List<OrderView>
                {
                    new(
                        "os-1048",
                        "Order #1048",
                        new DateTime(2026, 7, 17),
                        "Order received",
                        "Delivered",
                        2,
                        1840m,
                        new List<OrderItemView>
                        {
                            new("Reworked Track Jacket", 1, 890m),
                            new("Chain Detail Cap", 1, 340m)
                        },
                        new List<ReceiptLineView>
                        {
                            new("Subtotal", 1230m),
                            new("Shipping", 60m),
                            new("Tax", 550m),
                            new("Total", 1840m)
                        }),
                    new(
                        "os-1031",
                        "Order #1031",
                        new DateTime(2026, 6, 28),
                        "Dispatched",
                        "On the way",
                        1,
                        780m,
                        new List<OrderItemView>
                        {
                            new("Angel Wing Sweatshirt", 1, 780m)
                        },
                        new List<ReceiptLineView>
                        {
                            new("Subtotal", 780m),
                            new("Shipping", 60m),
                            new("Tax", 0m),
                            new("Total", 780m)
                        })
                }
            };

            PersistAccount(demoAccount);
        }

        var configuredAdminEmail = NormalizeEmail(_adminEmailConfig ?? "admin");
        var configuredAdminPassword = _adminPasswordConfig ?? "admin";
        var adminExistingDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", configuredAdminEmail)).FirstOrDefault();
        if (adminExistingDoc is null)
        {
            var adminSalt = RandomNumberGenerator.GetBytes(16);
            var adminAccount = new AccountRecord
            {
                FirstName = "Admin",
                LastName = "User",
                Phone = "+45 00 00 00 00",
                Email = configuredAdminEmail,
                PasswordSaltBase64 = Convert.ToBase64String(adminSalt),
                PasswordHash = HashPassword(configuredAdminPassword, adminSalt, PasswordIterations),
                PasswordIterations = PasswordIterations,
                MemberSince = new DateTime(2026, 7, 1),
                IsAdmin = true,
                Orders = new List<OrderView>()
            };

            PersistAccount(adminAccount);
        }

        var aliasEmail = NormalizeEmail("admin@admin.dk");
        var aliasExistingDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", aliasEmail)).FirstOrDefault();
        if (aliasExistingDoc is null)
        {
            var adminAccountDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", configuredAdminEmail)).FirstOrDefault();
            if (adminAccountDoc is not null)
            {
                var adminRec = MapToAccountRecord(adminAccountDoc);
                var aliasedAccount = adminRec with
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Email = aliasEmail
                };
                PersistAccount(aliasedAccount);
            }
        }
    }

    public bool RestoreSession(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var normalizedEmail = NormalizeEmail(email);
        var accountDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", normalizedEmail)).FirstOrDefault();
        var account = accountDoc is null ? null : MapToAccountRecord(accountDoc);
        if (account is null)
        {
            return false;
        }

        CurrentAccount = ToAccountView(account);
        NotifyChange();
        return true;
    }

    public bool AddOrder(OrderView order, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (CurrentAccount is null)
        {
            errorMessage = "You must be logged in to place an order.";
            return false;
        }

        var currentKey = NormalizeEmail(CurrentAccount.Email);
        var accountDoc = _mongoCollection.Find(Builders<BsonDocument>.Filter.Eq("Email", currentKey)).FirstOrDefault();
        var account = accountDoc is null ? null : MapToAccountRecord(accountDoc);
        if (account is null)
        {
            errorMessage = "Current account could not be found.";
            return false;
        }

        if (account.Orders is null)
        {
            account.Orders = new List<OrderView>();
        }

        account.Orders.Insert(0, order);
        PersistAccount(account, currentKey);

        CurrentAccount = ToAccountView(account);
        NotifyChange();
        return true;
    }

    private void PersistAccount(AccountRecord account, string? previousKey = null)
    {
        if (!string.IsNullOrWhiteSpace(previousKey) && !string.Equals(previousKey, account.Email, StringComparison.OrdinalIgnoreCase))
        {
            _mongoCollection.DeleteOne(Builders<BsonDocument>.Filter.Eq("Email", previousKey));
        }

        var doc = account.ToBsonDocument();
        _mongoCollection.ReplaceOne(
            Builders<BsonDocument>.Filter.Eq("Email", account.Email),
            doc,
            new ReplaceOptions { IsUpsert = true });
    }

    private static bool VerifyPassword(string password, AccountRecord account)
    {
        if (!string.IsNullOrWhiteSpace(account.PasswordHash) && account.PasswordHash.Contains(':'))
        {
            var parts = account.PasswordHash.Split(':', 3);
            if (parts.Length == 3 && int.TryParse(parts[0], out var iterations))
            {
                var salt = Convert.FromBase64String(parts[1]);
                var expectedHash = parts[2];
                var actualHash = ComputePasswordHash(password, salt, iterations);
                return string.Equals(actualHash, expectedHash, StringComparison.Ordinal);
            }
        }

        var legacyHash = ComputePasswordHash(password, Convert.FromBase64String(account.PasswordSaltBase64), 1);
        return string.Equals(legacyHash, account.PasswordHash, StringComparison.Ordinal);
    }

    private static AccountView ToAccountView(AccountRecord account)
        => new(
            account.FirstName,
            account.LastName,
            account.Phone,
            account.Email,
            account.MemberSince,
            account.IsAdmin,
            account.Orders,
            account.Address,
            account.PostalCode,
            account.City);

    private static string GeneratePasswordResetToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenBytes);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    private static AccountRecord MapToAccountRecord(BsonDocument doc)
    {
        try
        {
            return BsonSerializer.Deserialize<AccountRecord>(doc);
        }
        catch
        {
            var acc = new AccountRecord();

            if (doc.TryGetValue("_id", out var idVal))
            {
                acc.Id = idVal.ToString();
            }

            if (doc.TryGetValue("FirstName", out var fn) && fn.IsString) acc.FirstName = fn.AsString;
            if (doc.TryGetValue("LastName", out var ln) && ln.IsString) acc.LastName = ln.AsString;
            if (doc.TryGetValue("Phone", out var ph) && ph.IsString) acc.Phone = ph.AsString;
            if (doc.TryGetValue("Email", out var em) && em.IsString) acc.Email = em.AsString;
            if (doc.TryGetValue("PasswordSaltBase64", out var ps) && ps.IsString) acc.PasswordSaltBase64 = ps.AsString;
            if (doc.TryGetValue("PasswordHash", out var phash) && phash.IsString) acc.PasswordHash = phash.AsString;
            if (doc.TryGetValue("PasswordIterations", out var it) && it.IsInt32) acc.PasswordIterations = it.AsInt32;
            if (doc.TryGetValue("MemberSince", out var ms) && ms.IsValidDateTime) acc.MemberSince = ms.ToUniversalTime();
            if (doc.TryGetValue("IsAdmin", out var ia) && ia.IsBoolean) acc.IsAdmin = ia.AsBoolean;
            if (doc.TryGetValue("Address", out var a) && a.IsString) acc.Address = a.AsString;
            if (doc.TryGetValue("PostalCode", out var pc) && pc.IsString) acc.PostalCode = pc.AsString;
            if (doc.TryGetValue("City", out var c) && c.IsString) acc.City = c.AsString;
            if (doc.TryGetValue("ResetToken", out var rt) && rt.IsString) acc.ResetToken = rt.AsString;
            if (doc.TryGetValue("ResetTokenExpiresAt", out var rte) && rte.IsValidDateTime) acc.ResetTokenExpiresAt = rte.ToUniversalTime();

            acc.Orders = new List<OrderView>();
            return acc;
        }
    }

    private static string HashPassword(string password, byte[] salt, int iterations)
    {
        var hash = ComputePasswordHash(password, salt, iterations);
        return $"{iterations}:{Convert.ToBase64String(salt)}:{hash}";
    }

    private static string ComputePasswordHash(string password, byte[] salt, int iterations)
    {
        using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations, HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(32);
        return Convert.ToBase64String(hash);
    }
}
