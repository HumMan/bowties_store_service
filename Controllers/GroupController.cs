using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using store_service.App.Repository;
using Model = store_service.App.Model;
using MongoDB.Bson;
using store_service.App.Model.Converters;
using Omu.ValueInjecter;

namespace store_service.Controllers
{
    public class GroupChildDesc
    {
        public string Title { get; set; }
        public string Id { get; set; }
        public bool Visible { get; set; }
    }

    public class GroupDesc
    {
        public string Title { get; set; }
        public string Id { get; set; }
        public bool Visible { get; set; }
        public List<GroupChildDesc> Child { get; set; } = new List<GroupChildDesc>();
    }

    public class DeleteGroupRequest
    {
        public string GroupId { get; set; }
    }


    [Authorize()]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class GroupController : Controller
    {
        private readonly IConfiguration Configuration;
        private readonly IGroupsRepository Repository;
        private readonly IProductsRepository Products;
        public GroupController(IConfiguration configuration, IGroupsRepository repository,
            IProductsRepository products)
        {
            Configuration = configuration;
            Repository = repository;
            Products = products;
        }
        private static void GenerateIds(Model.Data.Group data)
        {
            foreach (var p in data.VariationParameters)
            {
                if (p.Id == ObjectId.Empty)
                    p.Id = ObjectId.GenerateNewId();
                foreach (var v in p.Values)
                {
                    if (v.Id == ObjectId.Empty)
                        v.Id = ObjectId.GenerateNewId();
                }
            }
        }
        [Authorize(Roles = "Manager")]
        [HttpPost("updateorder")]
        public async Task UpdateGroupsOrder([FromBody] UpdateOrderRequest request)
        {
            foreach (var p in request.Orders)
            {
                var id = ObjectId.Parse(p.Key);
                await Repository.SetGroupOrder(id, p.Value);
            }
        }
        [Authorize(Roles = "Manager")]
        [HttpPost]
        public async Task CreateGroup([FromBody] Model.Web.Group group)
        {
            var data = group.ToData();
            data.Id = ObjectId.GenerateNewId();
            GenerateIds(data);
            await Repository.CreateGroup(data);
        }      
        [Authorize(Roles = "Manager")]
        [HttpPost("update")]
        public async Task UpdateGroup([FromBody] Model.Web.Group group)
        {
            var newGroup = group.ToData();

            GenerateIds(newGroup);

            var originalGroup = await Repository.GetGroup(newGroup.Id);

            //проверяем изменение вариаций
            var newParameters = newGroup.VariationParameters
                .Where(i => !originalGroup.VariationParameters
                   .Any(k => k.Id == i.Id)).ToList();
            var deletedParameters = originalGroup.VariationParameters
                .Where(i => !newGroup.VariationParameters
                   .Any(k => k.Id == i.Id)).ToList();
            if (newParameters.Count() > 0 || deletedParameters.Count() > 0)
                throw new Exception("Нельзя изменить параметры группы после создания");

            if (newGroup.VariationParameters.Count > 0)
            {



                var originalVariationIds = Helpers.Variation.GenerateVariations(originalGroup)
                .Select(i => Helpers.Variation.ToVariationIds(originalGroup.VariationParameters, i)).ToArray();

                var newVariationIds = Helpers.Variation.GenerateVariations(newGroup)
                    .Select(i => Helpers.Variation.ToVariationIds(newGroup.VariationParameters, i)).ToArray();

                var idsToAdd = newVariationIds.Except(originalVariationIds, new Helpers.VariationIdsComparer()).ToList();

                var idsToArchive = originalVariationIds.Except(newVariationIds, new Helpers.VariationIdsComparer()).ToList();                

                var productsToUpdate = (await Products.GetAllProducts()).Where(i => i.GroupId == newGroup.Id).ToList();

                foreach (var p in productsToUpdate)
                {
                    var anyChangesInProduct = false;
                    foreach (var v in p.Variations)
                    {
                        var variationIds = v.VariationIds.Select(i => i.ToWeb()).ToList();
                        if (idsToArchive.Contains(variationIds, new Helpers.VariationIdsComparer()))
                        {
                            v.IsArchived = true;
                            anyChangesInProduct = true;
                        }
                    }

                    foreach (var variation in p.Variations)
                    {
                        var oldTitle = variation.Title;
                        var newTitle = Helpers.Variation.ConcatTitles(newGroup.VariationParameters, variation.VariationIds);
                        if(oldTitle!=newTitle)
                        {
                            variation.Title = newTitle;
                            anyChangesInProduct = true;
                        }
                    }

                    foreach (var i in idsToAdd)
                    {
                        p.Variations.Add(new Model.Data.ProductVariation
                        {
                            Id = ObjectId.GenerateNewId(),
                            Price = 500,
                            IsArchived = false,
                            CanBeOrdered = false,
                            WithoutCount = true,
                            VariationIds = i.Select(x => x.ToData()).ToList(),
                            Title = Helpers.Variation.ConcatTitles(newGroup.VariationParameters, i),
                        });
                        anyChangesInProduct = true;
                    }
                    
                    if(anyChangesInProduct)
                        await Products.UpdateProduct(p);
                }

            }
            else
            {

            }

            foreach (var p in originalGroup.VariationParameters)
            {
                var newParameter = newGroup.VariationParameters.First(i => i.Id == p.Id);
                foreach (var v in p.Values)
                {
                    var value = newParameter.Values.FirstOrDefault(i => i.Id == v.Id);
                    if (value == null)
                        v.IsArchived = true;
                    else
                    {
                        v.InjectFrom(value);
                    }
                }
            }

            foreach (var p in newGroup.VariationParameters)
            {
                var newParameter = originalGroup.VariationParameters.First(i => i.Id == p.Id);
                foreach (var v in p.Values)
                {
                    if (!newParameter.Values.Any(i => i.Id == v.Id))
                    {
                        newParameter.Values.Add(v);
                    }
                }
            }

            newGroup.VariationParameters = originalGroup.VariationParameters;
            originalGroup.InjectFrom(newGroup);

            await Repository.UpdateGroup(originalGroup);
        }
        [Authorize(Roles = "Manager")]
        [HttpPost("delete")]
        public async Task DeleteGroup([FromBody] DeleteGroupRequest request)
        {
            //TODO проверить что архивны все продукты группы
            await Repository.DeleteGroup(ObjectId.Parse(request.GroupId));
        }
        [AllowAnonymous]
        [HttpGet("all")]
        public async Task<List<Model.Web.Group>> AllGroups()
        {
            var data = (await Repository.GetAllGroups()).OrderBy(i => i.Order).Select(i => i.ToWeb()).ToList();
            return data;
        }
        [AllowAnonymous]
        [HttpGet("allDesc")]
        public async Task<List<GroupDesc>> AllGroupsDesc()
        {
            var data = (await Repository.GetAllGroups())
                .Where(i=>i.Visible)
                .OrderBy(i => i.Order)
                .Select(i => i.ToWeb()).ToList();

            var result = new List<GroupDesc>();

            foreach (var g in data.GroupBy(i => i.Title))
            {
                result.Add(new GroupDesc
                {
                    Title = g.Key,
                    Id = String.Join(';', g.Select(i => i.Id)),
                    Visible = g.Count() > 1 ? !g.All(i => !i.Visible) : g.First().Visible,
                    Child = g
                    .Where(i => !string.IsNullOrWhiteSpace(i.Subtitle))
                    .Select(i => new GroupChildDesc
                    {
                        Id = i.Id.ToString(),
                        Title = i.Subtitle,
                        Visible = i.Visible
                    }).ToList()
                });
            }

            return result;
        }
        
    }
}