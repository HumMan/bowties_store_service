using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace store_service.Helpers
{
    public interface IMailer
    {
        Task<bool> Send(string toEmail, string subject, string body);
    }
    public class Mailer : IMailer
    {
        private readonly IConfiguration _config;
        private readonly ILogger<Mailer> _logger;
        private readonly string Host;
        private readonly int Port;
        private readonly string Login;
        private readonly string Password;
        private readonly string NotifyMail;
        public Mailer(IConfiguration configuration, ILogger<Mailer> logger)
        {
            _config = configuration;
            _logger = logger;

            Host = _config["Store:Email:SmtpServer"];
            Port = int.Parse(_config["Store:Email:SmtpPort"]);
            Login = _config["Store:Email:SmtpLogin"];
            Password = _config["Store:Email:SmtpPassword"];
            NotifyMail = _config["Store:Email:Notify"];
        }
        public async Task<bool> Send(string toEmail, string subject, string body)
        {
            try
            {
                using (var smtpClient = new SmtpClient
                {
                    Host = Host,
                    Port = Port,
                    EnableSsl = true,
                    Credentials = new NetworkCredential(Login, Password)
                })
                {

                    using (var message = new MailMessage(Login, toEmail)
                    {
                        Subject = subject,
                        Body = body,
                        IsBodyHtml = true
                    })
                    {
                        await smtpClient.SendMailAsync(message);
                    }
                }
                return true;
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Ошибка при отправке сообщения {0}, {1}", toEmail, subject);
                return false;
            }
        }
    }
}
