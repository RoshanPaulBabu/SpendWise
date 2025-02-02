using Microsoft.EntityFrameworkCore;
using SpendWise.Data;
using SpendWise.Models;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace SpendWise.Services;

public interface IUserDataService
{
    Task EnsureUserExistsAsync(string userId, string name, string email);
    Task<User> GetUserDetailsAsync(string userId);

    Task UpdateUserAsync(User user);
}

public class UserDataService : IUserDataService
{
    private readonly SpendWiseContext _context;

    public UserDataService(SpendWiseContext context, ICategoryService categoryService)
    {
        _context = context;
    }

    public async Task EnsureUserExistsAsync(string userId, string name, string email)
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (existingUser == null)
        {
            var newUser = new User
            {
                UserId = userId,
                Name = name,
                Email = email ?? $"{userId}@teams.user", // Fallback email
                CreatedAt = DateTime.UtcNow
            };

            await _context.Users.AddAsync(newUser);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<User> GetUserDetailsAsync(string userId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }


    public async Task UpdateUserAsync(User user)  // Implementation of the new method
    {
        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.UserId == user.UserId);

        if (existingUser != null)
        {
            existingUser.Name = user.Name ?? existingUser.Name;
            existingUser.Email = user.Email ?? existingUser.Email;

            // Update any other properties as needed
            _context.Users.Update(existingUser);
            await _context.SaveChangesAsync();
        }
        else
        {
            throw new InvalidOperationException("User not found.");
        }
    }
}