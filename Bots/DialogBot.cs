using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using SpendWise.Services;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.Extensions.Configuration;

namespace SpendWise.Bots
{
    public class DialogBot<T> : ActivityHandler where T : Dialog
    {
        protected readonly Dialog Dialog;
        protected readonly BotState ConversationState;
        protected readonly BotState UserState;
        protected readonly ILogger Logger;
        private readonly IUserDataService _userDataService;
        private readonly IConfiguration _configuration;

        public DialogBot(
            ConversationState conversationState,
            UserState userState,
            T dialog,
            ILogger<DialogBot<T>> logger,
            IUserDataService userDataService,
            IConfiguration configuration)
        {
            ConversationState = conversationState;
            UserState = userState;
            Dialog = dialog;
            Logger = logger;
            _userDataService = userDataService;
            _configuration = configuration;
        }

        public override async Task OnTurnAsync(ITurnContext turnContext, CancellationToken cancellationToken = default)
        {
            await base.OnTurnAsync(turnContext, cancellationToken);
            await ConversationState.SaveChangesAsync(turnContext, false, cancellationToken);
            await UserState.SaveChangesAsync(turnContext, false, cancellationToken);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            Logger.LogInformation("Processing message activity");

            if (turnContext.Activity.Speak != null)
            {
                string recognizedText = await RecognizeSpeechAsync();
                turnContext.Activity.Text = recognizedText;
            }

            var userId = "tommy@gmail.com";
            var userName = "Tommy John";
            var userEmail = "tommy@gmail.com";

            await _userDataService.EnsureUserExistsAsync(userId, userName, userEmail);
            turnContext.TurnState.Add("UserId", userId);
            turnContext.TurnState.Add("UserName", userName);
            turnContext.TurnState.Add("UserEmail", userEmail);

            Logger.LogInformation("Starting dialog");
            await Dialog.RunAsync(
                turnContext,
                ConversationState.CreateProperty<DialogState>("DialogState"),
                cancellationToken);
        }

        private async Task<string> RecognizeSpeechAsync()
        {
            string speechKey = _configuration["AzureSpeech:Key"];
            string speechRegion = _configuration["AzureSpeech:Region"];
            var config = SpeechConfig.FromSubscription(speechKey, speechRegion);
            using var recognizer = new SpeechRecognizer(config);
            var result = await recognizer.RecognizeOnceAsync();
            return result.Text;
        }
    }
}
