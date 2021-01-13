using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using store_service.App.Repository;
using Model = store_service.App.Model;
using store_service.App.Model.Converters;
using store_service.App.Model.Web;
using store_service.Helpers;

namespace store_service.Controllers
{
    public class QuantityResult
    {
        public int NewQuantity { get; set; }
        public bool IsSuccess { get; set; }
        public int ErrorMesssage { get; set; }
    }

    public class AddRemoveProductResult
    {
        public bool IsSuccess { get; set; }
        public int ItemsCount { get; set; }
        public int Message { get; set; }
    }
    public class ChangeQuantityRequest
    {
        public string VariationId { get; set; }
        public int Quantity { get; set; }
    }
    public class IncDecRequest
    {
        public string ProductId { get; set; }
        public string VariationId { get; set; }
    }

    [Authorize()]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class CartController : Controller
    {
        private readonly ICartsRepository CartsRepository;
        private readonly IProductsRepository ProductsRepository;
        public CartController(ICartsRepository cartsRepository, IProductsRepository productsRepository)
        {
            CartsRepository = cartsRepository;
            ProductsRepository = productsRepository;
        }
        private async Task<Model.Web.Product> ProductToWeb(Model.Data.Product p)
        {
            var countList = await ProductsRepository.GetInventoryAvailable(p.Variations.Select(i => i.Id).ToArray());
            var product = p.ToWeb(countList);
            return product;
        }
        [Authorize(Roles = "Manager")]
        [HttpGet]
        public async Task<Model.Web.Cart[]> AllCarts()
        {
            var result = await CartsRepository.GetAllCarts();
            return result.Select(i => i.ToWeb()).ToArray();
        }
        [Authorize()]   
        [HttpGet("current")]
        public async Task<Model.Web.Cart> CurrentCart()
        {
            var userId = ObjectId.Parse(HttpContext.User.Identity.Name);
            var cart = await CartsRepository.GetUserCart(userId);

            var result = cart.ToWeb();
            foreach (var item in result.Items)
            {
                var variationId = ObjectId.Parse(item.VariationId);
                              
                item.Product = await ProductToWeb(await ProductsRepository.GetProduct(ObjectId.Parse(item.ProductId)));

                var variation = item.Product.Variations.First(i => i.Id == item.VariationId);
                item.WithoutCount = variation.WithoutCount;
                if (!item.WithoutCount)
                    item.Available = await ProductsRepository.GetInventoryAvailable(variationId);
                item.Price = variation.Price;
                if (variation.Title != null)
                    item.Title = $"{item.Product.Title} ({variation.Title})";
                else
                    item.Title = item.Product.Title;
            }
            result.DeliveryPrice = PriceCalc.CalcPostDeliveryPrice(result.Items);
            result.ItemsTotal = PriceCalc.CalcItemsPrice(result.Items);
            return result;
        }
        [Authorize] 
        [HttpPost()]
        public async Task<Model.Web.Cart> UpdateCart([FromBody] Model.Web.Cart cart)
        {
            var value = cart.ToData();
            value.UserId = ObjectId.Parse(HttpContext.User.Identity.Name);
            value.LastUpdate = DateTime.UtcNow;
            var success = await CartsRepository.UpdateCart(value);
            return await CurrentCart();
        }
        [Authorize] 
        [HttpPost("add")]
        public async Task<AddRemoveProductResult> Add([FromBody] IncDecRequest param)
        {
            var curr = await CurrentCart();
            var item = curr.Items.FirstOrDefault(i => i.VariationId == param.VariationId);
            if (item != null)
            {
                item.Count++;
            }else
            {
                curr.Items.Add(new Model.Web.CartItem
                {
                    Count = 1,
                    ProductId = param.ProductId,
                    VariationId = param.VariationId
                });
            }

            var newCart = await UpdateCart(curr);

            var result = new AddRemoveProductResult();
            result.IsSuccess = true;
            result.ItemsCount = newCart.Items.Count;
            return result;
        }
    }
}
