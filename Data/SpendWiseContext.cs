using Microsoft.EntityFrameworkCore;
using SpendWise.Models;

namespace SpendWise.Data;

public class SpendWiseContext : DbContext
{
    public SpendWiseContext(DbContextOptions<SpendWiseContext> options)
        : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<RecurringExpense> RecurringExpenses => Set<RecurringExpense>();
    public DbSet<Budget> Budgets => Set<Budget>();
    public DbSet<Goal> Goals => Set<Goal>();
    public DbSet<ExpenseTag> ExpenseTags => Set<ExpenseTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // User Configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId)
                .HasMaxLength(36)
                .IsRequired();

            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsRequired();

            entity.HasIndex(e => e.Email)
                .IsUnique();

            entity.Property(e => e.LocationType)
                .HasConversion<string>()
                .HasMaxLength(50);
        });

        // Expense Configuration
        modelBuilder.Entity<Expense>(entity =>
        {
            entity.HasKey(e => e.ExpenseId);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Expenses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Category)
                .WithMany(c => c.Expenses)
                .HasForeignKey(e => e.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(e => e.Amount)
                .HasPrecision(15, 2);

            entity.HasIndex(e => e.ExpenseDate);
        });

        // Category Configuration
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.CategoryId);

            entity.Property(e => e.Name)
                .HasMaxLength(255)
                .IsRequired();
        });

        // Recurring Expense Configuration
        modelBuilder.Entity<RecurringExpense>(entity =>
        {
            entity.HasKey(e => e.RecurringId);

            entity.Property(e => e.Frequency)
                .HasConversion<string>();

            entity.Property(e => e.FrequencyUnit)
                .HasConversion<string>()
                .HasMaxLength(10);

            entity.HasOne(e => e.User)
                .WithMany(u => u.RecurringExpenses)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Budget Configuration
        modelBuilder.Entity<Budget>(entity =>
        {
            entity.HasKey(e => e.BudgetId);

            entity.Property(e => e.Amount)
                .HasPrecision(15, 2);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Budgets)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Goal Configuration
        modelBuilder.Entity<Goal>(entity =>
        {
            entity.HasKey(e => e.GoalId);

            entity.Property(e => e.TargetAmount)
                .HasPrecision(15, 2);

            entity.HasOne(e => e.User)
                .WithMany(u => u.Goals)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Expense Tag Configuration
        modelBuilder.Entity<ExpenseTag>(entity =>
        {
            entity.HasKey(e => e.TagId);

            entity.Property(e => e.TagName)
                .HasMaxLength(100)
                .IsRequired();

            entity.HasOne(e => e.Expense)
                .WithMany(e => e.Tags)
                .HasForeignKey(e => e.ExpenseId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}