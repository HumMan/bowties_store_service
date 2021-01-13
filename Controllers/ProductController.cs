using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using store_service.App.Repository;
using Model = store_service.App.Model;
using System.Text;
using store_service.App.Model.Converters;
using Microsoft.AspNetCore.Http;

namespace store_service.Controllers
{
    public class ProductsResult
    {
        public Model.Web.Product[] Products { get; set; }
    }

    public class DeleteProductRequest
    {
        public string ProductId { get; set; }
    }
    public class MultipleProductsRequest
    {
        public List<string> Ids { get; set; }
    }

    public class UpdateOrderRequest
    {
        public Dictionary<string, int> Orders { get; set; }
    }

    [Authorize()]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ProductController : Controller
    {
        private readonly IProductsRepository Repository;
        private readonly IGroupsRepository Groups;
        private readonly IFilesRepository Files;
        public ProductController(IProductsRepository repository, IGroupsRepository groups, IFilesRepository files)
        {
            Repository = repository;
            Groups = groups;
            Files = files;
        }

        #region private
        private static void OrderProducts(Model.Data.Product[] data)
        {
            Array.Sort(data, (x, y) =>
            {
                if (x.GroupId == y.GroupId)
                {
                    if (x.Order == y.Order)
                        return 0;
                    else
                        return x.Order > y.Order ? 1 : -1;
                }
                else
                {
                    return x.GroupId > y.GroupId ? 1 : -1;
                }
            }
                            );
        }
        private static int Max(int value, int max)
        {
            if (value > max)
                return max;
            else
                return value;
        }
        private static void FilterNotAvailableVariations(Model.Data.Product item)
        {
            item.Variations = item.Variations.Where(k => !k.IsArchived && k.CanBeOrdered).ToList();
        }

        private async Task<Model.Web.Product> ProductToWeb(Model.Data.Product p)
        {
            var countList = new List<int>();
            var inventoryCounts = await Repository.GetInventoryAvailable(p.Variations.Select(i => i.Id).ToArray());
            var product = p.ToWeb(inventoryCounts);
            return product;
        }

        private int TryGetCount(Dictionary<ObjectId, int> dict, ObjectId key)
        {
            if (dict.TryGetValue(key, out int result))
                return result;
            else
                return 0;
        }

        private async Task<List<Model.Web.Product>> ProductsToWeb(Model.Data.Product[] products)
        {
            var countsArr = await Repository.GetAllInventoryAvailable();
            var variationCount = countsArr.ToDictionary(i => i.Id, i => i.Count);

            var resultProducts = new List<Model.Web.Product>();
            foreach (var p in products)
            {
                Model.Web.Product product = p.ToWeb(p.Variations.Select(i => i.WithoutCount ? 0 : TryGetCount(variationCount, i.Id)).ToArray());
                resultProducts.Add(product);
            }
            return resultProducts;
        } 
        #endregion

        [Authorize(Roles = "Manager")]
        [HttpGet("all")]
        public async Task<ProductsResult> AllProducts(string groupId)
        {

            Model.Data.Product[] data;
            if (groupId != null)
            {
                data = await Repository.GetAllProducts(ObjectId.Parse(groupId), false);   
                data = data.OrderBy(i => i.Order).ToArray();
            }
            else
            {
                data = await Repository.GetAllProducts(new Nullable<ObjectId>(), false);
                OrderProducts(data);
            }

            var result = new ProductsResult();
            result.Products = (await ProductsToWeb(data)).ToArray();
            return result;
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("allcolors")]
        public async Task<string[]> AllColors()
        {
            return await Repository.GetAllColors();
        }

        [Authorize(Roles = "Manager")]
        [HttpGet("update_thumb")]
        public async Task<IActionResult> UpdateThumb()
        {
            try
            {
                int updated = 0;
                var all = await Repository.GetAllProducts(null, false, false);
                foreach (var p in all)
                {
                    if (p.Images != null)
                    {
                        for (int i = 0; i < p.Images.Count; i++)
                        {
                            var img = p.Images[i];
                            var result = await Helpers.Images.UpdateThumb(img, Files);
                            p.Images[i] = result.Result;
                            updated += result.UpdatedCount;
                        }
                        await Repository.UpdateProductImages(p.Id, p.Images);
                    }
                }
                var allG = await Groups.GetAllGroups(false);
                foreach(var g in allG)
                {
                    if (g.Image != null)
                    {
                        var result = await Helpers.Images.UpdateThumb(g.Image, Files);
                        g.Image = result.Result;
                        updated += result.UpdatedCount;
                        await Groups.UpdateGroupImages(g.Id, g.Image);
                    }
                }
                return Json(new { Success = true, Updated=updated });
            }
            catch (System.Exception ex)
            {
                return Json(new { Success = false, Message = ex.Message });
            }
        }

        [Authorize(Roles = "Manager")]
        [HttpPost("updateorder")]
        public async Task UpdateProductsOrder([FromBody] UpdateOrderRequest request)
        {
            foreach (var p in request.Orders)
            {
                var id = ObjectId.Parse(p.Key);
                await Repository.SetProductOrder(id, p.Value);
            }
        }
        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task CreateProduct([FromBody] Model.Web.Product product)
        {
            var data = product.ToData();
            data.Id = ObjectId.GenerateNewId();
            await Repository.CreateProduct(data);
            foreach (var p in product.Variations)
            {
                if (!p.WithoutCount)
                    await Repository.UpdateVariationInventory(data.Id, ObjectId.Parse(p.Id), Max(p.InventoryCount, 1000));
            }
        }
        [Authorize(Roles = "Manager")]
        [HttpPost("delete")]
        public async Task DeleteProduct([FromBody] DeleteProductRequest productId)
        {
            await Repository.DeleteProduct(ObjectId.Parse(productId.ProductId));
        }
        [Authorize(Roles = "Manager")]
        [HttpPost("update")]
        public async Task UpdateProduct([FromBody] Model.Web.Product product)
        {
            var data = product.ToData();
            await Repository.UpdateProduct(data);
            foreach (var p in product.Variations)
            {
                if (!p.WithoutCount)
                    await Repository.UpdateVariationInventory(data.Id, ObjectId.Parse(p.Id), Max(p.InventoryCount, 1000));
            }
        }
        [Authorize(Roles = "Manager")]
        [HttpGet("default")]
        public async Task<Model.Web.Product> DefaultProduct(string groupId)
        {
            var group = await Groups.GetGroup(ObjectId.Parse(groupId));
            var result = new Model.Web.Product
            {
                Title = "Товар123",
                GroupId = groupId,
                IsArchived = false,
            };



            if (group.VariationParameters.Count > 0)
            {
                var variationIds = Helpers.Variation.GenerateVariations(group);
                foreach (var v in variationIds)
                {
                    result.Variations.Add(new Model.Web.ProductVariation
                    {
                        Id = ObjectId.GenerateNewId().ToString(),
                        Price = 400,
                        IsArchived = false,
                        CanBeOrdered = false,
                        WithoutCount = true,
                        VariationIds = Helpers.Variation.ToVariationIds(group.VariationParameters, v),
                        Title = Helpers.Variation.ConcatTitles(group.VariationParameters, v),
                    });
                }
            }
            else
            {
                result.Variations.Add(new Model.Web.ProductVariation
                {
                    Id = ObjectId.GenerateNewId().ToString(),
                    Price = 400,
                    IsArchived = false,
                    CanBeOrdered = false,
                    WithoutCount = true,
                    VariationIds = null,
                    Title = null,
                });
            }

            return result;
        }
        [AllowAnonymous]
        [HttpGet("available")]
        public async Task<ProductsResult> AvailableProducts(string groupId)
        {
            Model.Data.Product[] data;
            if (groupId != null && groupId.Contains(';'))
            {
                data = await Repository.GetAllProducts(
                    groupId.Split(';').Select(i => ObjectId.Parse(i)).ToArray(), true);
            }
            else
            {
                data = await Repository.GetAllProducts(
                    groupId != null ? ObjectId.Parse(groupId) : new ObjectId?(), true);
            }

            OrderProducts(data);

            foreach (var item in data)
            {
                FilterNotAvailableVariations(item);
            }
            var result = new ProductsResult();
            result.Products = (await ProductsToWeb(data)).ToArray();
            return result;
        }

        private async Task<Model.Web.Product> GetWebProduct(ObjectId id)
        {
            var data = await Repository.GetProduct(id);
            if (data != null)
            {
                FilterNotAvailableVariations(data);

                Model.Web.Product product = await ProductToWeb(data);
                product.Group = (await Groups.GetGroup(data.GroupId)).ToWeb();
                return product;
            }
            else
                return null;
        }

        [AllowAnonymous]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(Model.Web.Product))]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        //TODO во все остальные методы проверку параметров и возврат статусов
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProduct(string id)
        {
            if (ObjectId.TryParse(id, out var objectId))
            {
                var product = await GetWebProduct(objectId);
                if (product != null)
                    return Ok(product);
                else
                    return NotFound();
            }
            else
            {
                return BadRequest();
            }
        }

        [AllowAnonymous]
        [HttpPost("multiple")]
        public async Task<ProductsResult> GetMultipleProducts([FromBody] MultipleProductsRequest request)
        {
            var result = new List<Model.Web.Product>();
            foreach (var p in request.Ids)
            {
                if (ObjectId.TryParse(p, out var objectId))
                {
                    var product = await GetWebProduct(objectId);
                    if (product != null)
                        result.Add(product);
                }
            }
            return new ProductsResult { Products = result.ToArray() };
        }
        
    }
}