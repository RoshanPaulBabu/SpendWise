// SpendWise/Bots/DialogBot.cs
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Logging;
using SpendWise.Services;
using System.Threading;
using System.Threading.Tasks;

namespace SpendWise.Bots
{
    public class DialogBot<T> : ActivityHandler where T : Dialog
    {
        protected readonly Dialog Dialog;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        protected readonly ILogger Logger;
        private readonly IUserDataService _userDataService;

        public DialogBot(
            ConversationState conversationState,
            UserState userState,
            T dialog,
            ILogger<DialogBot<T>> logger,
            IUserDataService userDataService)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
            _userDataService = userDataService;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);

            // Save state changes
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Processing message activity");

            // Dummy user info for testing
            var userId = "dummy-user-id";
            var userName = "Dummy User";
            var userEmail = "dummy.user@example.com";

            // Ensure user exists in database
            await _userDataService.EnsureUserExistsAsync(userId, userName, userEmail);

            // Store user context in turn state
            turnContext.TurnState.Add("UserId", userId);
            turnContext.TurnState.Add("UserName", userName);
            turnContext.TurnState.Add("UserEmail", userEmail);

            // Run dialog
            Logger.LogInformation("Starting dialog");
            await Dialog.RunAsync(
                turnContext,
                ConversationState.CreateProperty<DialogState>("DialogState"),
                cancellationToken);
        }
    }
}