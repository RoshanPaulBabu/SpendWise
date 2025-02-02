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

            if (functionName == "refine_query")
            {
                await stepContext.Context.SendActivityAsync(MessageFactory.Text(response), cancellationToken);
                return await stepContext.EndDialogAsync(null, cancellationToken);
            }

            return await stepContext.ReplaceDialogAsync(InitialDialogId, response, cancellationToken);
        }

    }

}