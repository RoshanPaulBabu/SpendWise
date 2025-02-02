#nullable enable

using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendWise.Models;

[Table("Expenses")]
public class Expense
{
    [Key]
    [Column("expense_id")]
    public int ExpenseId { get; set; }

    [Required]
    [Column("user_id")]
    [StringLength(36)]
    public string UserId { get; set; } = null!;

    [Required]
    [Column("amount")]
    [Precision(15, 2)]
    public decimal Amount { get; set; }

    [Required]
    [Column("category_id")]
    public int CategoryId { get; set; }

    [Column("description")]
    public string? Description { get; set; }

    [Required]
    [Column("expense_date")]
    public DateOnly ExpenseDate { get; set; }

    [Column("receipt_url")]
    [StringLength(500)]
    public string? ReceiptUrl { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [ForeignKey("CategoryId")]
    public Category Category { get; set; } = null!;

    public List<ExpenseTag> Tags { get; set; } = new();
}