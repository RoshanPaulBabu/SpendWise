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
        private readonly IExpenseService _expenseService;

        public AzureOpenAIService(IConfiguration configuration, ILogger<AzureOpenAIService> logger, IUserDataService userDataService, ICategoryService categoryService, IBudgetService budgetService, IGoalService goalService, IExpenseService expenseService)
        {
            _configuration = configuration;
            _logger = logger;
            _userDataService = userDataService;
            _categoryService = categoryService;
            _budgetService = budgetService;
            _goalService = goalService;
            _expenseService = expenseService;

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
                            ""description"": ""ID from predefined categories""
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

                string jsonSchemaCreateBudgets = @"
                {
                    ""type"": ""object"",
                    ""properties"": {
                        ""budgets"": {
                            ""type"": ""array"",
                            ""items"": {
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
                            }
                        }
                    },
                    ""required"": [""budgets""]
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
                    BinaryData.FromString(jsonSchemaCreateBudgets)
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

            var baseMessage = $@"**Current Date:** {currentDate}

            ## **General Rules**
            - Keep responses **short and to the point**.
            - Ask for clarification **only when necessary**.
            - Prioritize the latest user request if topics shift.

            ## **Expense Management**
            - **Automatically categorize expenses**.  
              - If category is unclear, ask the user.  
              - Otherwise, do **not** ask for a category.  
            - Validate expenses **against recurring payments** and **budget limits**.
            - Convert all amounts to **{user?.Currency ?? "INR"}**.
            - Available Categories: {categoriesString}.

            ## **Reports & Insights**
            - If the user requests a **report** or **insight**, include:
              1. **Total Expenses** (sum of all expenses).
              2. **Breakdown by Category** (detailed expense distribution).
              3. **Remaining Salary** (after all deductions).
              4. **Recurring Expenses** (highlight key ongoing expenses).
              5. **Budget Status** (if available).
            - Format reports concisely, avoiding unnecessary details.

            ## **Expense Validation**
            - Reject expenses exceeding category budgets.
            - Flag **duplicates** (same amount & merchant within 24h).
            - Convert foreign currencies to **{user?.Currency ?? "USD"}**.
            - Default missing dates to **today**.

            ## **Logging Bill Expenses**
            - If an expense is **a bill or receipt-based**, ensure:
              - **Description** includes:
              - Store Name
              - List of Items & Prices
              - **Total Amount**
              - **Transaction Date**
               - Example format:  
                    `""SuperMart - Items: Apples ($2), Bread ($3), Milk ($4) | Total: $9 | Date: 2025-03-05""`

            ## **Budget Allocation Rules**
            - **Total Budget Allocation:**  
              - **Essential Expenses (Max 50% of Salary)**  
                - 🛒 **Food:** **20%**  
                - 🚗 **Transport:** **10%**  
                - ⚡ **Utilities:** **15%**  
                - 🚨 **Emergency:** **5%**  

              - **Savings (Min 20% of Salary)**  
                - 💰 **Savings/Investments:** **20%**  

              - **Discretionary Spending (30% of Salary)**  
                - 🎟️ **Entertainment:** **10%**  
                - 🛍️ **Shopping:** **20%**  

            - **Budget Validation:**  
              - **Reject** expenses that exceed category limits.  
              - **Warn** if discretionary spending exceeds 30%.  
              - **Suggest** adjustments if savings drop below 20%.  


            ## **Response Formatting**
            - **Concise & structured** responses.
            - Use **bold** for key figures and *italics* for hints.
            - Show progress indicators (**✔️ Success**, ⚠️ Warning)

            ## **Mandatory Function Calls**
            - `create_user_profile` → If any user data is missing.
            - `categorize_expense` → Only when automatic categorization is **uncertain**.


            ## **Error Handling**
            - Return **clear, structured errors** for invalid inputs.
            - Suggest **alternatives** for budget overflows.


            ## **Tone & Style**
            - Friendly, **non-judgmental** financial guidance.
            - Reinforce **good habits**, gently warn about overspending.";

            if (user == null)
                return baseMessage + "\nUSER PROFILE MISSING - COLLECT USING create_user_profile FUNCTION";

            var userBudgets = await _budgetService.GetBudgetsAsStringAsync(user.UserId);
            var userGoals = await _goalService.GetActiveGoalsAsStringAsync(user.UserId);
            var totalExpensesCat = await _expenseService.GetTotalAmountByCategoryForCurrentMonthAsync(user.UserId);
            var totalExpensesCatString = string.Join(", ", totalExpensesCat.Select(kvp => $"{kvp.Key}: {kvp.Value:C}"));
            var recurringExpenses = await _expenseService.GetRecurringExpensesAsStringAsync(user.UserId);
            var salaryBalance = await _expenseService.GetSalaryBalanceAsync(user.UserId);
            var totalExpense = await _expenseService.GetTotalExpensesTillDateAsync(user.UserId);

            return $@"{baseMessage}
                User Profile:
                - Salary: {user.Salary?.ToString("C") ?? "Not set"}
                - Location: {user.LocationType ?? "Not set"}
                - Currency: {user.Currency}
                - Budgets: {userBudgets}
                - Active user goals: {userGoals}
                - Total expenses by category: {totalExpensesCatString}
                - Recurring expenses: {recurringExpenses}
                - Total expenses till date: {totalExpense}
                - Salary Balanace: {salaryBalance}";
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
