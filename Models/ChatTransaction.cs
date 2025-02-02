using Newtonsoft.Json;
using System;

namespace SpendWise.Models
{
    public class ChatTransaction
    {
        public ChatTransaction(string botMessage, string userMessage)
        {
            BotMessage = botMessage;
            UserMessage = userMessage;
            ChatTime = DateTime.Now;
        }

        [JsonProperty("User")]
        public string UserMessage { get; set; }

        [JsonProperty("Assistant")]
        public string BotMessage { get; set; }

        public DateTime ChatTime { get; set; }
    }
}
