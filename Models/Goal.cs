using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SpendWise.Models;

[Table("Goals")]
public class Goal
{
    [Key]
    [Column("goal_id")]
    public int GoalId { get; set; }

    [Required]
    [Column("user_id")]
    [StringLength(36)]
    public string UserId { get; set; } = null!;

    [Required]
    [Column("name")]
    [StringLength(255)]
    public string Name { get; set; } = null!;

    [Required]
    [Column("target_amount")]
    [Precision(15, 2)]
    public decimal TargetAmount { get; set; }

    [Column("current_amount")]
    [Precision(15, 2)]
    public decimal CurrentAmount { get; set; }

    [Required]
    [Column("start_date")]
    public DateOnly StartDate { get; set; }

    [Column("end_date")]
    public DateOnly? EndDate { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    [ForeignKey("UserId")]
    public User User { get; set; } = null!;
}