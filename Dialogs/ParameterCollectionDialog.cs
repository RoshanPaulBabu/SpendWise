using SpendWise.Services;
using Microsoft.Bot.Builder.Dialogs;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.Bot.Builder;
using System;
using Microsoft.Bot.Schema;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SpendWise.Helpers;
using SpendWise.Models;

namespace SpendWise.Dialogs
{
    public class ParameterCollectionDialog : ComponentDialog
    {
        private readonly IStatePropertyAccessor<UserProfile> _userProfileAccessor;
        private readonly ExternalServiceHelper _externalServiceHelper;
        private const string UserProfileKey = "UserProfile";
        private readonly AdaptiveCardHelper _AdaptiveCardHelper;


        public ParameterCollectionDialog(
    ExternalServiceHelper externalServiceHelper,
    IStatePropertyAccessor<UserProfile> userProfileAccessor,
    AdaptiveCardHelper adaptiveCardHelper
)
    : base(nameof(ParameterCollectionDialog))
        {
            _userProfileAccessor = userProfileAccessor;
            _externalServiceHelper = externalServiceHelper;
            _AdaptiveCardHelper = adaptiveCardHelper;

            //AddDialog(new LeadCollectionDialog(externalServiceHelper, graphHelper));
            //AddDialog(new QnAHandlingDialog(externalServiceHelper));
            AddDialog(new WaterfallDialog(nameof(WaterfallDialog), new WaterfallStep[]
            {
                AskHelpQueryStepAsync,
                BeginParameterCollectionStepAsync,
            }));
            AddDialog(new TextPrompt(nameof(TextPrompt)));
            AddDialog(new TextPrompt("DatePrompt"));
            AddDialog(new TextPrompt("AttendeesPrompt"));

            InitialDialogId = nameof(WaterfallDialog);
        }


        private async Task<DialogTurnResult> AskHelpQueryStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var options = stepContext.Options as dynamic;

            if (stepContext.Options is string textResponse && !string.IsNullOrEmpty(textResponse))
            {

                await stepContext.Context.SendActivityAsync(MessageFactory.Text(textResponse), cancellationToken);


                return EndOfTurn;

            }

            else if (options != null && options.GetType().GetProperty("Action") != null)
            {
                // Pass the action value as a string to the next dialog
                return await stepContext.NextAsync(options?.Action, cancellationToken);
            }
            else if (options != null && options.GetType().GetProperty("Message") != null)
            {
                // Pass the action value as a string to the next dialog
                return await stepContext.NextAsync(options?.Message, cancellationToken);
            }

            else
            {
                var adaptiveCard = _AdaptiveCardHelper.CreateAdaptiveCardAttachment("welcomeCard.json", null);

                await stepContext.Context.SendActivityAsync(MessageFactory.Attachment(adaptiveCard), cancellationToken);

                return EndOfTurn;
            }
        }


        private async Task<DialogTurnResult> BeginParameterCollectionStepAsync(WaterfallStepContext stepContext, CancellationToken cancellationToken)
        {
            var cardData = stepContext.Context.Activity.Value;

            // Retrieve the user query (response from the previous step)
            string userMessage = (string)stepContext.Result;

            var userProfile = await _userProfileAccessor.GetAsync(stepContext.Context, () => new UserProfile(), cancellationToken);
            userProfile.ChatHistory ??= new List<ChatTransaction>();

            // Determine what to pass to HandleOpenAIResponseAsync
            string inputToAIService = cardData != null ? cardData.ToString() : userMessage;

            var (response, functionName, functionParams) = await _externalServiceHelper.HandleUserQueryAsync(inputToAIService, userProfile.ChatHistory, stepContext.Context);

            // If functionName is "get_expense_summary", render an Adaptive Card
            if (functionName == "expense_summary")
            {
                var summaryItems = functionParams as List<Expense>;
                var adaptiveCardJson = GenerateExpenseSummaryCard(summaryItems);
                var adaptiveCardAttachment = new Attachment
                {
                    ContentType = "application/vnd.microsoft.card.adaptive",
                    Content = JsonConvert.DeserializeObject(adaptiveCardJson)
                };
                await stepContext.Context.SendActivityAsync(MessageFactory.Attachment(adaptiveCardAttachment), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(functionName))
            {
                // For all other cases, send a text response
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            return await stepContext.ReplaceDialogAsync(InitialDialogId, response, cancellationToken);
        }

        // Update the GenerateExpenseSummaryCard method to accept List<Expense>
        private string GenerateExpenseSummaryCard(List<Expense> summaryItems)
        {
            bool showDate = summaryItems.Select(e => e.ExpenseDate).Distinct().Count() > 1;
            bool showCategory = summaryItems.Select(e => e.Category.Name).Distinct().Count() > 1;
            decimal totalAmount = summaryItems.Sum(e => e.Amount);

            var columns = new List<string>
    {
        @"{
            ""type"": ""Column"",
            ""width"": ""stretch"",
            ""items"": [
                {
                    ""type"": ""TextBlock"",
                    ""text"": ""Description"",
                    ""weight"": ""Bolder""
                }
            ]
        }",
        @"{
            ""type"": ""Column"",
            ""width"": ""stretch"",
            ""items"": [
                {
                    ""type"": ""TextBlock"",
                    ""text"": ""Amount"",
                    ""weight"": ""Bolder""
                }
            ]
        }"
    };

            if (showDate)
            {
                columns.Add(@"{
            ""type"": ""Column"",
            ""width"": ""stretch"",
            ""items"": [
                {
                    ""type"": ""TextBlock"",
                    ""text"": ""Date"",
                    ""weight"": ""Bolder""
                }
            ]
        }");
            }

            if (showCategory)
            {
                columns.Add(@"{
            ""type"": ""Column"",
            ""width"": ""stretch"",
            ""items"": [
                {
                    ""type"": ""TextBlock"",
                    ""text"": ""Category"",
                    ""weight"": ""Bolder""
                }
            ]
        }");
            }

            var columnSets = summaryItems.Select(item => $@"
    {{
        ""type"": ""ColumnSet"",
        ""columns"": [
            {{
                ""type"": ""Column"",
                ""width"": ""stretch"",
                ""items"": [
                    {{
                        ""type"": ""TextBlock"",
                        ""text"": ""{item.Description}"",
                        ""wrap"": true
                    }}
                ]
            }},
            {{
                ""type"": ""Column"",
                ""width"": ""stretch"",
                ""items"": [
                    {{
                        ""type"": ""TextBlock"",
                        ""text"": ""₹{item.Amount}"",
                        ""wrap"": true
                    }}
                ]
            }}
            {(showDate ? $@",
            {{
                ""type"": ""Column"",
                ""width"": ""stretch"",
                ""items"": [
                    {{
                        ""type"": ""TextBlock"",
                        ""text"": ""{item.ExpenseDate:MM/dd/yyyy}"",
                        ""wrap"": true
                    }}
                ]
            }}" : string.Empty)}
            {(showCategory ? $@",
            {{
                ""type"": ""Column"",
                ""width"": ""stretch"",
                ""items"": [
                    {{
                        ""type"": ""TextBlock"",
                        ""text"": ""{item.Category.Name}"",
                        ""wrap"": true
                    }}
                ]
            }}" : string.Empty)}
        ]
    }}").ToList();

            return $@"
    {{
        ""$schema"": ""http://adaptivecards.io/schemas/adaptive-card.json"",
        ""type"": ""AdaptiveCard"",
        ""version"": ""1.3"",
        ""body"": [
            {{
                ""type"": ""TextBlock"",
                ""text"": ""Expense Summary"",
                ""weight"": ""Bolder"",
                ""size"": ""Medium""
            }},
            {{
                ""type"": ""ColumnSet"",
                ""columns"": [
                    {string.Join(",", columns)}
                ]
            }},
            {string.Join(",", columnSets)},
            {{
                ""type"": ""TextBlock"",
                ""text"": ""Total Amount: ₹{totalAmount}"",
                ""weight"": ""Bolder"",
                ""size"": ""Medium""
            }}
        ]
    }}";
        }





    }

}