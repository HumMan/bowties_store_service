using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using Newtonsoft.Json;
using store_service.App.Model.Data;
using store_service.App.Model.Web;
using store_service.App.Repository;
using store_service.Helpers;
using store_service.Services;
using Model = store_service.App.Model;

namespace store_service.Controllers
{
    public class CreateOrderResult
    {
        public bool Success { get; set; }
        public bool NotEnoughtInventory { get; set; }
        public Model.Web.Order Order { get; set; }
        public string Message { get; set; }
    }

    public class CreateOrderRequest
    {
        public Model.CheckoutParameters Parameters { get; set; }
        public Model.Web.Cart Cart { get; set; }
    }

    public class ChangeStatusRequest
    {
        public string OrderId { get; set; }
        public Model.OrderStatus From { get; set; }
        public Model.OrderStatus To { get; set; }
    }

    [Authorize()]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class OrderController : Controller
    {
        private readonly IOrdersRepository Repository;
        private readonly ICartsRepository CartsRepository;
        private readonly IUsersRepository Users;
        private readonly IProductsRepository Products;

        private readonly IGlobalParameters _params;

        private readonly IMailer Mailer;
        private readonly IConfiguration _config;

        private readonly ITelegramBot _bot;

        public OrderController(IOrdersRepository repository, ICartsRepository cartsRepository,
            IUsersRepository usersRepository,
            IGlobalParameters parameters,
            IConfiguration config,
            ITelegramBot bot,
            IProductsRepository products, IMailer mailer)
        {
            Repository = repository;
            CartsRepository = cartsRepository;
            Users = usersRepository;
            Products = products;
            Mailer = mailer;
            _params = parameters;
            _config = config;
            _bot = bot;
        }
        private bool IsEqualCarts(Model.Data.Cart cart1, Model.Web.Cart cart2)
        {
            if (cart1.Items.Count != cart2.Items.Count)
                return false;
            for (int i = 0; i < cart1.Items.Count; i++)
            {
                var left = cart1.Items[i];
                var right = cart2.Items[i];
                if (left.Count != right.Count
                    || left.ProductId != ObjectId.Parse(right.ProductId)
                    || left.VariationId != ObjectId.Parse(right.VariationId))
                    return false;
            }
            return true;
        }
        private bool CheckRole(string role)
        {
            var claims = HttpContext.User.Claims.Where(i => i.Type == "Role");
            return claims.Any(i => i.Value == role);
        }
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<Model.Web.Order[]> GetAll()
        {
            var result = await Repository.GetAll();
            return result.OrderByDescending(i => i.Created).Select(i => i.ToWeb()).ToArray();
        }

        [Authorize(Roles = "Manager")]
        [HttpPost("changestatus")]
        public async Task<IActionResult> ChangeStatus([FromBody] ChangeStatusRequest request)
        {
            if (ObjectId.TryParse(request.OrderId, out var id))
            {
                await Repository.ChangeOrderStatus(id, request.From, request.To);
                return Ok();
            }
            else
                return BadRequest();

        }
        [Authorize] 
        [Produces("application/json")]
        [HttpPost]
        public async Task<CreateOrderResult> CreateOrderFromCart([FromBody] CreateOrderRequest request)
        {
            var userId = ObjectId.Parse(HttpContext.User.Identity.Name);

            await Users.UpdateCheckoutParams(userId, request.Parameters);

            //проверям что корзина на сервере и у пользователя одна и та же (вдруг он изменил кол-во на другой вкладке)
            var cart = await CartsRepository.GetUserCart(userId);
            if(!IsEqualCarts(cart, request.Cart))
            {
                return new CreateOrderResult()
                {
                    Success = false,
                    Message = "Корзина была изменена, проверьте её содержимое перед подтверждением заказа"
                };
            }

            if(cart.Items.Count==0)
            {
                return new CreateOrderResult()
                {
                    Success = false,
                    Message = "Невозможно создать пустой заказ, добавте товары в корзину"
                };
            }

            //if (кол-во неподтверждённых заказов большое)
            //{
            //    return new CreateOrderResult()
            //    {
            //        Success = false,
            //        NeedCaptcha = true,
            //        Message = "Вы создали очень мноого заказов, подтвердите что вы не робот"
            //    };
            //}

            //получаем инфу о товарах
            foreach (var i in cart.Items)
            {
                var product = await Products.GetProduct(i.ProductId);
                var variation = product.Variations.First(k => k.Id == i.VariationId);
                i.WithoutCount = variation.WithoutCount;
                i.Price = variation.Price;
                if(!variation.CanBeOrdered || variation.IsArchived || product.IsArchived)
                {
                    return new CreateOrderResult()
                    {
                        Success = false,
                        NotEnoughtInventory = true,
                        Message = "Некоторые товары недоступны для заказа"
                    };
                }
            }

            var result = await Repository.CreateOrderFromCart(cart, userId, request.Parameters);
            if (result != null)
            {
                cart.Items.Clear();
                await CartsRepository.UpdateCart(cart);

                var newOrder = (await Repository.FindOrder(result.Value));

                if (newOrder.Status == Model.OrderStatus.WaitingApprove)
                {
                    //TODO если данный пользователь за последнее время создал много заказов, то показываем ему капчу
                    //рядом с кнопкой создать заказ
                    var title = $"Заказ {newOrder.OrderId} ждёт подтверждения";
                    var message = string.Format(
                            "Заказ <a href=\"{0}/success/{2}\">{1}</a> ждёт подтверждения",
                            _config["Store:SiteDomain"],
                            newOrder.OrderId,
                            newOrder.Id);

                    if (!await _bot.Notify(message))
                        await Mailer.Send(_config["Store:Email:Notify"], title, message);
                }

                return new CreateOrderResult()
                {
                    Success = true,                    
                    Order = newOrder.ToWeb()
                };
            }
            else
            {
                return new CreateOrderResult()
                {
                    Success = false,
                    NotEnoughtInventory = true,
                };
            }
        }

       

        [Authorize] 
        [HttpGet("find")] 
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Model.Web.Order))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        //TODO во все остальные методы проверку параметров и возврат статусов
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetUserOrder(string orderId)
        {
            if (CheckRole("Manager"))
            {
                if (ObjectId.TryParse(orderId, out var objectId))
                {
                    var order = await Repository.FindOrder(objectId);
                    if (order == null)
                        return NotFound();
                    else
                        return Ok(order.ToWeb());
                }
                else
                    return BadRequest();
            }
            else
            {
                var userId = ObjectId.Parse(HttpContext.User.Identity.Name);
                if (ObjectId.TryParse(orderId, out var objectId))
                {
                    var order = await Repository.GetUserOrder(userId, objectId);
                    if (order == null)
                        return NotFound();
                    else
                        return Ok(order.ToWeb());
                }
                else
                    return BadRequest();
            }
        }

        [Authorize] 
        [HttpGet("findall")]
        public async Task<Model.Web.Order[]> GetAllUserOrders()
        {
            var userId = ObjectId.Parse(HttpContext.User.Identity.Name);
            var order = await Repository.GetAllUserOrders(userId);
            return order.Select(i => i.ToWeb()).ToArray();
        }
        [Authorize] 
        [HttpGet("checkoutparams")]
        public async Task<Model.CheckoutParameters> GetUserCheckoutParams()
        {
            var userId = ObjectId.Parse(HttpContext.User.Identity.Name);
            var user = await Users.FindUser(userId);
            return user.CheckoutParameters;
        }
        [AllowAnonymous] // тут в авторизации смысла нет, т.к. кэшируется в nginx
        [HttpGet("postalinfo")]
        public async Task<IActionResult> GetPostalInfo(int i)
        {
            using (var httpClient = new HttpClient())
            {
                var response = await httpClient.GetAsync("https://api.print-post.com/api/index/v2/?index=" + i);
                var result = await response.Content.ReadAsStringAsync();
                return Ok(JsonConvert.DeserializeObject(response.Content.ReadAsStringAsync().Result));
            }
        }
    }
}
