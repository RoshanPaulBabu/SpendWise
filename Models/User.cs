#nullable enable

using System.Collections.Generic;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendWise.Models;

[Table("Users")]
public class User
{
    [Key]
    [Column("user_id")]
    public string UserId { get; set; } = null!;

    [Required]
    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    [Column("email")]
    [StringLength(255)]
    public string Email { get; set; } = null!;

    [Column("currency")]
    [StringLength(10)]
    public string Currency { get; set; } = "INR";

    [Column("salary")]
    public decimal? Salary { get; set; }

    [Column("location_type")]
    [StringLength(50)]
    public string? LocationType { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public List<Expense> Expenses { get; set; } = new();
    public List<RecurringExpense> RecurringExpenses { get; set; } = new();
    public List<Budget> Budgets { get; set; } = new();
    public List<Goal> Goals { get; set; } = new();
}