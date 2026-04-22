using CommonBrewPOS.Models;
using BCrypt.Net;
using System.Security.Cryptography;
using System.Text;

namespace CommonBrewPOS.Services;

public class AuthService
{
    private readonly SupabaseService _db;

    public AuthService(SupabaseService db) => _db = db;

    public async Task<User?> AuthenticateAsync(string username, string password)
    {
        var user = await _db.SelectSingleAsync<User>("users",
            $"username=eq.{Uri.EscapeDataString(username)}&is_active=eq.true&select=*");

        if (user == null) return null;

        bool valid = BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
        return valid ? user : null;
    }

    public async Task<bool> CreateUserAsync(User user, string plainPassword)
    {
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(plainPassword);
        var result = await _db.InsertAsync<User>("users", new
        {
            full_name = user.FullName,
            username = user.Username,
            password_hash = user.PasswordHash,
            role = user.Role,
            is_active = true
        });
        return result != null;
    }

    public async Task<bool> ChangePasswordAsync(string userId, string newPassword)
    {
        var hash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        await _db.UpdateAsync("users", "id", userId, new { password_hash = hash });
        return true;
    }

    public async Task<List<User>> GetAllUsersAsync()
        => await _db.SelectAsync<User>(
            "users",
            "is_active=eq.true&select=id,full_name,username,role,is_active,created_at&order=created_at.desc"
        );

    public async Task UpdateUserAsync(string id, string fullName, string username, string role)
        => await _db.UpdateAsync("users", "id", id, new
        {
            full_name = fullName,
            username,
            role
        });

    public async Task DeleteUserAsync(string id)
        => await _db.UpdateAsync("users", "id", id, new { is_active = false });

    public async Task<List<User>> GetArchivedUsersAsync()
        => await _db.SelectAsync<User>(
            "users",
            "is_active=eq.false&select=id,full_name,username,role,is_active,created_at&order=created_at.desc"
        );

    public async Task RestoreUserAsync(string id)
        => await _db.UpdateAsync("users", "id", id, new { is_active = true });
}