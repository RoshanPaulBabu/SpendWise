using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendWise.Models;

[Table("Budgets")]
public class Budget
{
    [Key]
    [Column("budget_id")]
    public int BudgetId { get; set; }

    [Required]
    [Column("user_id")]
    [StringLength(36)]
    public string UserId { get; set; } = null!;

    [Required]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Required]
    [Column("amount")]
    [Precision(15, 2)]
    public decimal Amount { get; set; }

    [Column("adjustments")]
    [Precision(15, 2)]
    public decimal Adjustments { get; set; }

    [Required]
    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Required]
    [Column("end_date")]
    public DateOnly EndDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [ForeignKey("CategoryId")]
    public Category Category { get; set; } = null!;
}