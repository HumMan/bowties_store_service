using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MihaZupan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace store_service.Services
{
    public interface ITelegramBot
    {
        Task<bool> Notify(string message);
    }

    public class TelegramBot: ITelegramBot
    {
        private readonly ITelegramBotClient botClient;
        private readonly ILogger<TelegramBot> _logger;
        private readonly string ChatId;
        public TelegramBot(IConfiguration configuration, ILogger<TelegramBot> logger)
        {
            _logger = logger;
            var proxy = new HttpToSocks5Proxy(
                configuration["Store:BotTelegram:ProxyServer"],
                int.Parse(configuration["Store:BotTelegram:ProxyPort"]));
            botClient = new TelegramBotClient(configuration["Store:BotTelegram:Token"], proxy);
            //botClient.OnMessage += Bot_OnMessage;
            //botClient.StartReceiving();

            ChatId = configuration["Store:BotTelegram:NotifyChatId"];
        }
        async void Bot_OnMessage(object sender, MessageEventArgs e)
        {
            if (e.Message.Text != null)
            {
                await botClient.SendTextMessageAsync(
                  chatId: e.Message.Chat,
                  text: "Pong:\n" + e.Message.Text
                );
            }
        }
        
        public async Task<bool> Notify(string message)
        {
            try
            {
                var result = await botClient.SendTextMessageAsync(ChatId, message, parseMode: ParseMode.Html);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке сообщения");
                return false;
            }
        }        
    }
}
