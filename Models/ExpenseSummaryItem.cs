using System;

namespace SpendWise.Models
{
    public class ExpenseItems
    {
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public DateTime ExpenseDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public SummaryCategory Category { get; set; }
        public string UserId { get; set; }
    }

    public class SummaryCategory
    {
        public int CategoryId { get; set; }
        public string Name { get; set; }
    }

}
