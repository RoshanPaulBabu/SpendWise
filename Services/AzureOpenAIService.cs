using Azure;
using Azure.AI.OpenAI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Recognizers.Text;
using SpendWise.Models;

namespace SpendWise.Services
{
    public class AzureOpenAIService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<AzureOpenAIService> _logger;
        private readonly IUserDataService _userDataService;
        private readonly ICategoryService _categoryService;
        private readonly IBudgetService _budgetService;
        private readonly IGoalService _goalService;

        public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger, IUserDataService userDataService, ICategoryService categoryService, IBudgetService budgetService, IGoalService goalService)
        {
            _configuration = configuration;
            _logger = logger;
            _userDataService = userDataService;
            _categoryService = categoryService;
            _budgetService = budgetService;
            _goalService = goalService;

        }

        public async Task<(object, string, string)> HandleOpenAIResponseAsync(string userQuestion, List<ChatTransaction> chatHistory, string userId)
        {
            try
            {
                // Initialize OpenAI client
                var apiKeyCredential = new System.ClientModel.ApiKeyCredential(_configuration["AzureOpenAIKey"]);
                var client = new AzureOpenAIClient(new Uri(_configuration["AzureOpenAIEndpoint"]), apiKeyCredential);
                var chatClient = client.GetChatClient("gpt-4o");
                var user = await _userDataService.GetUserDetailsAsync(userId);

                string jsonSchemaCreateUser = @"
                {
                    ""type"": ""object"",
                    ""properties"": {
                        ""salary"": { 
                            ""type"": ""number"", 
                            ""description"": ""User's monthly net income in their local currency""
                        },
                        ""location_type"": { 
                            ""type"": ""string"", 
                            ""enum"": [""city"", ""urban"", ""village""],
                            ""description"": ""Type of residential area for cost-of-living calculations""
                        },
                        ""currency"": { 
                            ""type"": ""string"", 
                            ""enum"": [""USD"", ""EUR"", ""INR"", ""GBP""],
                            ""description"": ""Primary currency for all financial transactions""
                        }
                    },
                    ""required"": [""salary"", ""location_type"", ""currency""]
                }";

                string jsonSchemaLogExpense = @"
                {
                    ""type"": ""object"",
                    ""properties"": {
                        ""amount"": { 
                            ""type"": ""number"", 
                            ""minimum"": 0.01,
                            ""description"": ""Expense amount in user's primary currency""
                        },
                        ""category_id"": { 
                            ""type"": ""integer"", 
                            ""description"": ""ID from predefined categories (1:Food, 2:Housing, 3:Transport, etc.)""
                        },
                        ""description"": { 
                            ""type"": ""string"", 
                            ""maxLength"": 255,
                            ""description"": ""Brief expense description""
                        },
                        ""expense_date"": { 
                            ""type"": ""string"", 
                            ""format"": ""date"",
                            ""description"": ""Transaction date in YYYY-MM-DD format""
                        }
                    },
                    ""required"": [""amount"", ""category_id"", ""expense_date""]
                }";

                string jsonSchemaCreateBudget = @"
                {
                    ""type"": ""object"",
                    ""properties"": {
                        ""category_id"": { 
                            ""type"": ""integer"", 
                            ""description"": ""Category ID for budget allocation""
                        },
                        ""amount"": { 
                            ""type"": ""number"", 
                            ""minimum"": 0.01,
                            ""description"": ""Monthly budget amount in user's currency""
                        },
                        ""start_date"": { 
                            ""type"": ""string"", 
                            ""format"": ""date"",
                            ""description"": ""Budget cycle start date (YYYY-MM-DD)""
                        },
                        ""end_date"": { 
                            ""type"": ""string"", 
                            ""format"": ""date"",
                            ""description"": ""Budget cycle end date (YYYY-MM-DD)""
                        }
                    },
                    ""required"": [""category_id"", ""amount"", ""start_date"", ""end_date""]
                }";

                string jsonSchemaSetGoal = @"
                {
                    ""type"": ""object"",
                    ""properties"": {
                        ""goal_name"": { 
                            ""type"": ""string"", 
                            ""description"": ""Name of the financial goal""
                        },
                        ""target_amount"": { 
                            ""type"": ""number"", 
                            ""minimum"": 0.01,
                            ""description"": ""Total amount to be saved for the goal""
                        },
                        ""target_date"": { 
                            ""type"": ""string"", 
                            ""format"": ""date"",
                            ""description"": ""Target date for achieving the goal (YYYY-MM-DD)""
                        }
                    },
                    ""required"": [""goal_name"", ""target_amount"", ""target_date""]
                }";

                string jsonSchemaAddRecurringExpenses = @"
                {
                   ""type"": ""object"",
                   ""properties"": {
                       ""amount"": { 
                           ""type"": ""number"", 
                           ""minimum"": 0.01,
                           ""description"": ""Expense amount in user's primary currency""
                       },
                       ""category_id"": { 
                           ""type"": ""integer"", 
                           ""description"": ""ID from predefined categories (1:Food, 2:Housing, 3:Transport, etc.)""
                       },
                       ""description"": { 
                           ""type"": ""string"", 
                           ""maxLength"": 255,
                           ""description"": ""Brief expense description""
                       },
                       ""next_due_date"": { 
                           ""type"": ""string"", 
                           ""format"": ""date"",
                           ""description"": ""Next due date for recurring expense (YYYY-MM-DD)""
                       },
                       ""frequency"": { 
                           ""type"": ""string"", 
                           ""enum"": [""daily"", ""weekly"", ""monthly"", ""yearly""],
                           ""description"": ""Frequency of the recurring expense""
                       }
                   },
                   ""required"": [""amount"", ""category_id"", ""next_due_date"", ""frequency""]
                }";

                string jsonSchemaGetExpenseSummary = @"
                {
                    ""type"": ""object"",
                    ""properties"": {
                        ""start_date"": { 
                            ""type"": ""string"", 
                            ""format"": ""date"",
                            ""description"": ""Start date for expense summary (YYYY-MM-DD)""
                        },
                        ""end_date"": { 
                            ""type"": ""string"", 
                            ""format"": ""date"",
                            ""description"": ""End date for expense summary (YYYY-MM-DD)""
                        },
                        ""category_ids"": { 
                            ""type"": ""array"",
                            ""items"": {
                                ""type"": ""integer"",
                                ""description"": ""ID from predefined categories (1:Food, 2:Housing, 3:Transport, etc.)""
                            },
                            ""description"": ""List of category IDs for which the expense summary is requested""
                        },
                        ""include_all_categories"": { 
                            ""type"": ""boolean"",
                            ""description"": ""Flag to include all categories in the expense summary""
                        }
                    },
                    ""required"": [""start_date"", ""end_date""]
                }";

                // Define the tools
                var createUserTool = ChatTool.CreateFunctionTool(
                    "create_user_profile",
                    "Creates a new user profile based on the provided details.",
                    BinaryData.FromString(jsonSchemaCreateUser)
                );

                var logExpenseTool = ChatTool.CreateFunctionTool(
                    "log_expense",
                    "Logs a new expense for the user.",
                    BinaryData.FromString(jsonSchemaLogExpense)
                );

                var createBudgetTool = ChatTool.CreateFunctionTool(
                    "create_budget",
                    "Creates a new budget for a specific category.",
                    BinaryData.FromString(jsonSchemaCreateBudget)
                );

                var setGoalTool = ChatTool.CreateFunctionTool(
                    "set_goal",
                    "Sets a new financial goal for the user.",
                    BinaryData.FromString(jsonSchemaSetGoal)
                );

                var addRecurringExpensesTool = ChatTool.CreateFunctionTool(
                    "add_recurring_expenses",
                    "Adds a new recurring expense for the user.",
                    BinaryData.FromString(jsonSchemaAddRecurringExpenses)
                );

                var getExpenseSummaryTool = ChatTool.CreateFunctionTool(
                    "get_expense_summary",
                    "Provides a summary of expenses for a given period.",
                    BinaryData.FromString(jsonSchemaGetExpenseSummary)
                );

                // Define chat options with the tools
                var chatOptions = new ChatCompletionOptions
                {
                    Tools = { createUserTool, logExpenseTool, createBudgetTool, setGoalTool, addRecurringExpensesTool, getExpenseSummaryTool }
                };

                // System message with clear role definition
                var currentDateTimeWithDay = $"{DateTime.Now:yyyy-MM-dd HH:mm} ({DateTime.Now.DayOfWeek})";

                var systemMessage = await BuildSystemMessageWithCategories(user);
                var chatMessages = new List<ChatMessage> { new SystemChatMessage(systemMessage) };

                // Add chat history
                foreach (var transaction in chatHistory)
                {
                    if (!string.IsNullOrEmpty(transaction.UserMessage))
                        chatMessages.Add(new UserChatMessage(transaction.UserMessage));
                    if (!string.IsNullOrEmpty(transaction.BotMessage))
                        chatMessages.Add(new AssistantChatMessage(transaction.BotMessage));
                }

                // Add the current user question
                chatMessages.Add(new UserChatMessage(userQuestion));
                var chat = chatMessages.ToArray();

                // Perform chat completion
                ChatCompletion completion = await chatClient.CompleteChatAsync(chatMessages.ToArray(), chatOptions);

                // Process tool calls
                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    foreach (var toolCall in completion.ToolCalls)
                    {
                        var inputData = toolCall.FunctionArguments.ToObjectFromJson<Dictionary<string, object>>();

                        return (inputData, toolCall.FunctionName, null);
                    }
                }

                // Default response
                var response = completion.Content[0]?.Text ?? "I'm unable to process your request at this time.";
                chatHistory.Add(new ChatTransaction(response, userQuestion));
                return (null, null, response);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in HandleOpenAIResponseAsync: {ex.Message}");
                return (null, null, "An error occurred while processing your request. Please try again.");
            }
        }

        public async Task<string> BuildSystemMessageWithCategories(User user)
        {
            var categories = await _categoryService.GetAllCategorysAsync();
            var categoriesString = string.Join(", ", categories);

            var currentDate = DateTime.Now.ToString("yyyy-MM-dd dddd");
            var baseMessage = $@"Current Date: {currentDate}
                Financial Management Guidelines:
                1. Always convert amounts to user's currency ({user?.Currency ?? "USD"})
                2. Validate expense dates against recurring payments
                3. Check budget limits before confirming expenses, if budgets are not set leave it , dont ask the user to set it
                4. Suggest realistic savings goals based on income
                5. Available Categories: {categoriesString}

                ## Core Financial Rules
                1. **Budget Allocation**:
                   - Essential Expenses: Max 50% of salary
                   - Savings: Minimum 20% of salary
                   - Discretionary: Remaining 30%

                2. **Expense Validation**:
                   - Reject expenses exceeding category budgets
                   - Flag duplicates (similar amount/merchant within 24h)
                   - Convert foreign currencies to {user?.Currency ?? "USD"}

                3. **Goal Management**:
                   - Suggest achievable timelines: TargetAmount / (Salary * 0.2)
                   - Warn when new expenses jeopardize goal progress

                ## Function Call Guidelines
                1. **Mandatory Invocations**:
                   - Use create_user_profile if ANY profile data missing
                   - Invoke categorize_expense when category not specified

                2. **Validation Requirements**:
                   - Verify date formats (YYYY-MM-DD)
                   - Confirm category_id exists before logging expenses
                   - Check budget remaining before confirming expenses

                3. **Error Handling**:
                   - Return structured errors for invalid inputs
                   - Suggest alternatives for budget overflows

                ## Response Formatting
                1. **User-Facing Messages**:
                   - Always include currency symbols
                   - Use relative dates (""3 days ago"" not ""2024-03-15"")
                   - Add progress emojis: ✅ for success, ⚠️ for warnings

                3. **Tone & Style**:
                   - Empathetic financial guidance
                   - Positive reinforcement for good habits
                   - Non-judgmental alerts for overspending";

            if (user == null)
                return baseMessage + "\nUSER PROFILE MISSING - COLLECT USING create_user_profile FUNCTION";

            var userBudgets = await _budgetService.GetBudgetsAsStringAsync(user.UserId);
            var userGoals = await _goalService.GetActiveGoalsAsStringAsync(user.UserId);

            return $@"{baseMessage}
                User Profile:
                - Salary: {user.Salary?.ToString("C") ?? "Not set"}
                - Location: {user.LocationType ?? "Not set"}
                - Currency: {user.Currency}
                - Budgets: {userBudgets}
                -Active user goals: {userGoals}";
        }

        public async Task<string> HandleUserQuery(string userquery, string sysMessage)
        {
            try
            {
                // Initialize OpenAI client
                var apiKeyCredential = new System.ClientModel.ApiKeyCredential(_configuration["AzureOpenAIKey"]);
                var client = new AzureOpenAIClient(new Uri(_configuration["AzureOpenAIEndpoint"]), apiKeyCredential);
                var chatClient = client.GetChatClient("gpt-4o");

                var chatMessages = new List<ChatMessage>
                {

                    new SystemChatMessage(sysMessage),
                    new UserChatMessage(userquery),
                };

                ChatCompletion completion = await chatClient.CompleteChatAsync(chatMessages.ToArray());
                var result = completion;

                string response = completion.Content[0]?.Text ?? "I'm unable to process your request at this time.";

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error in HandleQueryRefinement: {ex.Message}");
                return "An error occurred while processing your request. Please try again.";
            }
        }
    }
}
