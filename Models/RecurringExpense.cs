#nullable enable

using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendWise.Models;

[Table("Recurring_Expenses")]
public class RecurringExpense
{
    [Key]
    [Column("recurring_id")]
    public int RecurringId { get; set; }

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
    [Column("frequency")]
    public string Frequency { get; set; } = null!;

    [Column("custom_frequency")]
    public int? CustomFrequency { get; set; }

    [Column("frequency_unit")]
    [StringLength(10)]
    public string? FrequencyUnit { get; set; }

    [Required]
    [Column("next_due_date")]
    public DateOnly NextDueDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;

    [ForeignKey("CategoryId")]
    public Category Category { get; set; } = null!;
}