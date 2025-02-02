using SpendWise.Data;
using SpendWise.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace SpendWise.Services;

public interface ICategoryService
{
    Task<List<(int Id, string Name)>> GetAllCategorysAsync();
}

public class CategoryService : ICategoryService
{
    private readonly SpendWiseContext _context;

    public CategoryService(SpendWiseContext context)
    {
        _context = context;
    }

    public async Task<List<(int Id, string Name)>> GetAllCategorysAsync()
    {
        return await _context.Categories
            .Select(c => new { c.CategoryId, c.Name })
            .ToListAsync()
            .ContinueWith(task => task.Result.Select(c => (c.CategoryId, c.Name)).ToList());
    }
}
