using Webshop.Shared.Models;

namespace Webshop.Core.Interfaces;

public interface IAccountService
{
    AccountView? CurrentAccount { get; }

    event Action? OnChange;

    bool Register(string firstName, string lastName, string phone, string email, string password, string address, string postalCode, string city, out string errorMessage);

    bool Login(string email, string password, out string errorMessage);

    void Logout();

    bool RestoreSession(string email);

    bool AddOrder(OrderView order, out string errorMessage);

    bool UpdateProfile(
        string firstName,
        string lastName,
        string phone,
        string email,
        out string errorMessage,
        string address,
        string postalCode,
        string city,
        string newPassword);
}
