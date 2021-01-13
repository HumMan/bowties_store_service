using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using store_service.App.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YandexMoney.Checkout.Managers;
using store_service.App.Model;
using Data = store_service.App.Model.Data;
using store_service.Helpers;
using store_service.Services;

namespace store_service.Controllers
{
    [Authorize()]
    [Route("api/yandexmoney")]
    public class YandexMoneyNotifyController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IMailer _mailer;
        private readonly IOrdersRepository _ordersRepository;
        private readonly IPaymentsRepository _paymentRepository;
        private readonly ILogger<YandexMoneyNotifyController> _logger;
        private readonly IYandexMoneyCheckoutPayment _paymentManager;
        private readonly IGlobalParameters _params;
        private readonly ITelegramBot _bot;

        public YandexMoneyNotifyController(
            IConfiguration configuration,
            IOrdersRepository ordersRepository,
            IPaymentsRepository paymentRepository,
            ILogger<YandexMoneyNotifyController> logger,
            IMailer mailer,
            IGlobalParameters parameters,
            ITelegramBot bot,
            IYandexMoneyCheckoutPayment paymentManager)
        {
            _configuration = configuration;
            _mailer = mailer;
            _ordersRepository = ordersRepository;
            _paymentRepository = paymentRepository;
            _logger = logger;
            _paymentManager = paymentManager;
            _params = parameters;
            _bot = bot;
        }
        [AllowAnonymous]
        [HttpPost]
        [Route("notify")]
        public async Task<IActionResult> YandexMoneyNotify()
        {
            try
            {
                var paymentRecord = new Data.PaymentNotify();
                var parameters = new Dictionary<string, string>();
                var parametersCollection = HttpContext.Request.Form;
                foreach (var key in parametersCollection.Keys)
                {
                    parameters.Add(key, parametersCollection[key]);
                }
                var request = String.Join(";", parameters.Select(i => i.Key + '=' + i.Value).ToArray());
                _logger.LogInformation("Payment notify received - {0}", request);
                paymentRecord.Request = request;
                paymentRecord.Timestamp = new BsonDateTime(DateTime.UtcNow);

                if (_paymentManager.ValidateChecksum(_configuration["Store:YandexMoney:Secret"], parameters, out var orderId))
                {
                    var orderIdParsed = ObjectId.Parse(orderId);
                    paymentRecord.ChecksumValid = true;
                    paymentRecord.OrderId = orderIdParsed;
                    await _paymentRepository.InsertPaymentNotify(paymentRecord);
                    var order = await _ordersRepository.FindOrder(orderIdParsed);
                    if (order != null)
                    {
                        if (_paymentManager.Validate(parameters, order.Total, out var error))
                        {
                            //пометить заказ оплаченным
                            var updateResult = await _ordersRepository.ChangeOrderStatus(orderIdParsed,
                                OrderStatus.WaitingPayment,
                                order.Parameters.DeliveryType == DeliveryType.Mail ?
                                OrderStatus.Packing : OrderStatus.Shipping);

                            var title = $"Заказ {order.OrderId} оплачен";
                            var message = string.Format(
                                    "Заказ <a href='{0}/success/{2}'>{1}</a> оплачен",
                                    _configuration["Store:SiteDomain"],
                                    order.OrderId,
                                    order.Id);

                            if(!await _bot.Notify(message))
                                await _mailer.Send(_configuration["Store:Email:Notify"], title, message);                            

                            if (!updateResult)
                                _logger.LogInformation("Заказ {0} найден, но имеет некорректный статус", orderId);
                        }
                        else
                        {
                            _logger.LogInformation("Ошибка при оплате заказа {0} - {1}", orderId, error);
                        }
                    }
                    else
                        _logger.LogInformation("Заказ не найден в базе {0}", orderId);
                }
                else
                {
                    _logger.LogInformation("Неверный формат сообщения");
                    paymentRecord.ChecksumValid = false;
                    //TODO тут в базу нежелательно писать, т.к. можно легко всё завалить записями
                    await _paymentRepository.InsertPaymentNotify(paymentRecord);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("Ошибка при обработке уведомления из YandexMoney " + ex.ToString());
            }
            return Ok();
        }
    }
}
