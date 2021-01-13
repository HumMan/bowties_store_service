using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;

namespace YandexMoney.Checkout.Managers
{

    public enum PaymentType
    {
        ByCard,
        ByYandexWallet
    }

    public interface IYandexMoneyCheckoutPayment
    {
        bool Validate(Dictionary<string, string> parameters, decimal total, out string error);
        bool ValidateChecksum(string secret, Dictionary<string, string> parameters, out string orderId);
    }

    public class YandexMoneyCheckoutPayment: IYandexMoneyCheckoutPayment
    {
        const string paymentUrl = @"https://money.yandex.ru/quickpay/confirm.xml";
        private readonly ILogger<YandexMoneyCheckoutPayment> _logger;

        public YandexMoneyCheckoutPayment(ILogger<YandexMoneyCheckoutPayment> logger)
        {
            _logger = logger;
        }
        //string GetSubmitForm(string paymentUrl, string label, string receiver, string sum, string successUrl,
        //    string formComment, string shortDesc, string targets, string comment, PaymentType payMethod)
        //{
        //    var payMethodToString = new Dictionary<PaymentType, string>
        //    {
        //        {PaymentType.ByCard, "AC" },
        //        {PaymentType.ByYandexWallet, "PC" },
        //    };

        //    string form =
        //    $"<form id=\"submitform\" method=\"POST\" action=\"{paymentUrl}\"> " +
        //    $"<input type=\"hidden\" name=\"receiver\" value=\"{receiver}\"> " +
        //    $"<input type=\"hidden\" name=\"formcomment\" value=\"{formComment}\"> " +
        //    $"<input type=\"hidden\" name=\"short-dest\" value=\"{shortDesc}\"> " +
        //    $"<input type=\"hidden\" name=\"label\" value=\"{label}\"> " +
        //    $"<input type=\"hidden\" name=\"quickpay-form\" value=\"donate\"> " +
        //    $"<input type=\"hidden\" name=\"targets\" value=\"{targets}\"> " +
        //    $"<input type=\"hidden\" name=\"sum\" value=\"{sum}\" data-type=\"number\"> " +
        //    $"<input type=\"hidden\" name=\"comment\" value=\"{comment}\"> " +
        //    $"<input type=\"hidden\" name=\"successURL\" value=\"{successUrl}\">" +
        //    $"<input type=\"hidden\" name=\"need-fio\" value=\"false\">  " +
        //    $"<input type=\"hidden\" name=\"need-email\" value=\"false\"> " +
        //    $"<input type=\"hidden\" name=\"need-phone\" value=\"false\"> " +
        //    $"<input type=\"hidden\" name=\"need-address\" value=\"false\"> " +
        //    $"<input type=\"hidden\" name=\"paymentType\" value=\"{payMethodToString[payMethod]}\"> " +
        //    $"<input type=\"submit\" value=\"Перейти на страницу оплаты\"> " +
        //    $"</form>" +
        //    $"<script type=\"text/javascript\">" +
        //    $"    setTimeout(function() {{ document.getElementById('submitform').submit(); }}, 1000);" +
        //    $"</script>"
        //    ;

        //    return form;
        //}

        public bool Validate(Dictionary<string, string> parameters, decimal total, out string error)
        {
            error = null;
            try
            {
                if (parameters["codepro"] != "false")
                    throw new Exception("Не поддерживаются платежи защищённые кодом протекции");
                if (parameters["currency"] != "643")
                    throw new Exception("Поддерживается только RUB");
                if (parameters["unaccepted"] != "false")
                    throw new Exception("Платёж является unaccepted");
                Decimal withdraw_amount;
                if (!Decimal.TryParse(parameters["withdraw_amount"], NumberStyles.Any, CultureInfo.InvariantCulture, out withdraw_amount))
                    throw new Exception(string.Format("Ошибка формата withdraw_amount = {0}", parameters["withdraw_amount"]));
                if (withdraw_amount < total)
                    throw new Exception("Сумма платежа меньше суммы заказа");

                return true;
            }
            catch (System.Exception ex)
            {
                error = ex.ToString();
                return false;
            }           
        }

        private static string ByteArrayToString(byte[] data)
        {
            string hex = BitConverter.ToString(data);
            return hex.Replace("-", "");
        }
        /// <summary>
        /// Проверяем что у запроса правильная контрольная сумма и он не подделка
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="orderId"></param>
        /// <returns></returns>
        public bool ValidateChecksum(string secret, Dictionary<string, string> parameters, out string orderId)
        {
            orderId = null;
            try
            {
                var notification_type = parameters["notification_type"];
                var operation_id = parameters["operation_id"];
                var amount = parameters["amount"];
                var currency = parameters["currency"];
                var datetime = parameters["datetime"];
                var sender = parameters["sender"];
                var codepro = parameters["codepro"];
                var notification_secret = secret;
                var label = parameters["label"];

                var sha1_hash = parameters["sha1_hash"];               

                if (notification_type != null &&
                    operation_id != null &&
                    amount != null &&
                    currency != null &&
                    datetime != null &&
                    sender != null &&
                    codepro != null &&
                    notification_secret != null &&
                    label != null)
                {
                    var value = notification_type + "&" + operation_id + "&" + amount + "&" +
                        currency + "&" + datetime + "&" + sender + "&" +
                        codepro + "&" + notification_secret + "&" + label;

                    byte[] data = System.Text.Encoding.UTF8.GetBytes(value);
                    byte[] result;

                    SHA1 sha = new SHA1CryptoServiceProvider();

                    result = sha.ComputeHash(data);
                    var calculatedSha = ByteArrayToString(result).ToLower();
                    if (calculatedSha == sha1_hash)
                    {
                        orderId = label;
                        return true;
                    }
                }
            }catch(Exception ex)
            {
                _logger.LogError(ex, "Ошибка при проверке crc");
            }
            return false;
        }
    }
}