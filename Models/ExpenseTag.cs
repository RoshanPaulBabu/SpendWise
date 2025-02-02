using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendWise.Models;

[Table("Expense_Tags")]
public class ExpenseTag
{
    [Key]
    [Column("tag_id")]
    public int TagId { get; set; }

    [Required]
    [Column("expense_id")]
    public int ExpenseId { get; set; }

    [Required]
    [Column("tag_name")]
    [StringLength(100)]
    public string TagName { get; set; } = null!;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("ExpenseId")]
    public Expense Expense { get; set; } = null!;
}