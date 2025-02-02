using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;
using SpendWise.Models;
using SpendWise.Services;
using System.Collections.Generic;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace SpendWise.Helpers
{
    public class ExternalServiceHelper
    {
        private readonly AzureOpenAIService _openAIService;
        private readonly IUserDataService _userService;
        private readonly IExpenseService _expenseService;
        private readonly IBudgetService _budgetService;
        private readonly IGoalService _goalService;

        public ExternalServiceHelper(
            AzureOpenAIService openAIService,
            IUserDataService userService,
            IExpenseService expenseService,
            IBudgetService budgetService,
            IGoalService goalService)
        {
            _openAIService = openAIService;
            _userService = userService;
            _expenseService = expenseService;
            _budgetService = budgetService;
            _goalService = goalService;
        }

        public async Task<(string response, string functionName, object parameters)> HandleUserQueryAsync(
            string query,
            List<ChatTransaction> history,
            ITurnContext context)
        {
            var userId = context.TurnState.Get<string>("UserId");
            var (data, functionName, directResponse) =
                await _openAIService.HandleOpenAIResponseAsync(query, history, userId);

            if (!string.IsNullOrEmpty(directResponse))
                return (directResponse, null, null);

            try
            {
                switch (functionName)
                {
                    case "create_user_profile":
                        return await HandleUserProfileCreation((Dictionary<string, object>)data, userId);

                    case "log_expense":
                        return await HandleExpenseLogging((Dictionary<string, object>)data, userId);

                    case "create_budget":
                        return await HandleBudgetCreation((Dictionary<string, object>)data, userId);

                    case "set_goal":
                        return await HandleGoalSetting((Dictionary<string, object>)data, userId);

                    case "add_recurring_expenses":
                        return await HandleRecurringExpense((Dictionary<string, object>)data, userId);

                    case "get_expense_summary":
                        return await HandleExpenseSummary((Dictionary<string, object>)data, userId);

                    default:
                        return ("Unknown operation requested.", functionName, null);
                }
            }
            catch (Exception ex)
            {
                return ($"Error processing request: {ex.Message}", "error", null);
            }
        }

        private async Task<(string, string, object)> HandleUserProfileCreation(Dictionary<string, object> data, string userId)
        {
            var user = await _userService.GetUserDetailsAsync(userId);
            user.Salary = Convert.ToDecimal(data["salary"]);
            user.LocationType = data["location_type"].ToString();
            user.Currency = data["currency"].ToString();

            await _userService.UpdateUserAsync(user);
            return ("Profile updated successfully!", "profile_updated", user);
        }

        private async Task<(string, string, object)> HandleExpenseLogging(Dictionary<string, object> data, string userId)
        {
            var expense = new Expense
            {
                UserId = userId,
                Amount = Convert.ToDecimal(data["amount"]),
                CategoryId = Convert.ToInt32(data["category_id"]),
                Description = data.TryGetValue("description", out var desc) ? desc.ToString() : null,
                ExpenseDate = DateOnly.Parse(data["expense_date"].ToString()),
                CreatedAt = DateTime.UtcNow
            };

            await _expenseService.AddExpenseAsync(expense);

            var budgetStatus = await _budgetService.CheckBudgetStatus(userId, expense.CategoryId);
            var message = $"Expense logged. {budgetStatus}";

            return (message, "expense_logged", expense);
        }

        private async Task<(string, string, object)> HandleBudgetCreation(Dictionary<string, object> data, string userId)
        {
            var budget = new Budget
            {
                UserId = userId,
                CategoryId = Convert.ToInt32(data["category_id"]),
                Amount = Convert.ToDecimal(data["amount"]),
                StartDate = DateOnly.Parse(data["start_date"].ToString()),
                EndDate = DateOnly.Parse(data["end_date"].ToString())
            };

            await _budgetService.CreateOrUpdateBudgetAsync(budget);
            return ("Budget updated successfully!", "budget_updated", budget);
        }

        private async Task<(string, string, object)> HandleGoalSetting(Dictionary<string, object> data, string userId)
        {
            var goal = new Goal
            {
                UserId = userId,
                Name = data["goal_name"].ToString(),
                TargetAmount = Convert.ToDecimal(data["target_amount"]),
                StartDate = DateOnly.FromDateTime(DateTime.Now),
                EndDate = DateOnly.Parse(data["target_date"].ToString())
            };

            await _goalService.AddGoalAsync(goal);
            return ("Goal set successfully!", "goal_set", goal);
        }

        private async Task<(string, string, object)> HandleRecurringExpense(Dictionary<string, object> data, string userId)
        {
            var recurringExpense = new RecurringExpense
            {
                UserId = userId,
                Amount = Convert.ToDecimal(data["amount"]),
                CategoryId = Convert.ToInt32(data["category_id"]),
                Description = data.TryGetValue("description", out var desc) ? desc.ToString() : null,
                NextDueDate = DateOnly.Parse(data["next_due_date"].ToString()),
                Frequency = data["frequency"].ToString()
            };

            await _expenseService.AddRecurringExpenseAsync(recurringExpense);
            return ("Recurring expense added successfully!", "recurring_expense_added", recurringExpense);
        }

        private async Task<(string, string, object)> HandleExpenseSummary(Dictionary<string, object> data, string userId)
        {
            var startDate = DateOnly.Parse(data["start_date"].ToString());
            var endDate = DateOnly.Parse(data["end_date"].ToString());
            var categoryIds = data.TryGetValue("category_ids", out var catIds) ?
                JsonSerializer.Deserialize<List<int>>(catIds.ToString()) : new List<int>();
            var includeAll = Convert.ToBoolean(data["include_all_categories"]);

            var summary = await _expenseService.GetExpenseSummaryAsync(userId, startDate.ToDateTime(TimeOnly.MinValue), endDate.ToDateTime(TimeOnly.MinValue), categoryIds, includeAll);
            return ($"Expense summary generated.", "expense_summary", summary);
        }


public async Task<string> IndentHandlingAsync(string response)
        {
            string sysMessage = """
            Classify user messages into two categories:
            1.Ending the conversation: Messages that indicate the user is concluding the interaction(e.g., "thank you," "okay," "bye," "got it").
            2.Service - related or other queries: Messages where the user is asking for services, making inquiries, or seeking further assistance.

            Respond with the following JSON format:
            -If the message indicates the end of the conversation: { "response": "YES"}
            -If the message is a service - related query or anything else: { "response": "SERVICE"}
            """;
            return await _openAIService.HandleUserQuery(response, sysMessage);
        }

        public async Task<string> RefineSearchResultsAsync(string originalQuery, List<string> searchResults)
        {
            string sysMessage = """
            You are Botangelos, a highly intelligent business assistant representing a leading AI and automation company. Your role is to refine and interpret search results retrieved from Azure Search to provide clear, concise, and actionable responses to the user according to the original query provided by the user. Always maintain a professional, client-focused tone and align with Botangelos's innovative ethos.Only include detail which is related to the original query.

            When processing search results:
            1. Prioritize clarity and relevance, summarizing the most important points from FAQs and company overviews according to the original query.
            2. Use structured formatting with bullet points, headings, and subheadings.
            3. Engage the user by relating responses to their potential business needs, emphasizing the value and expertise of Botangelos.

            Handle the following types of input:
            - **FAQs**: Provide direct answers or related insights based on frequently asked questions.
            - **User Engagement**: Suggest follow-up actions such as contacting the company, scheduling a consultation, or exploring specific services further.

            For any queries outside the scope of retrieved information, respond:
            "I couldn't find specific information related to your query. For further assistance, you can contact us at info@botangelos.com or schedule a consultation."
            
            """;

            var searchResultsString = string.Join("\n", searchResults);
            var response = await _openAIService.HandleUserQuery($"Original query: {originalQuery}\nSearch results: {searchResultsString}", sysMessage);
            return response;
        }
    }
}