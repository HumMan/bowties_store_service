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
using System.Xml.Linq;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using store_service.App.Model.Data;

namespace store_service.Controllers
{

    public class SitemapNode
    {
        public SitemapFrequency? Frequency { get; set; }
        public DateTime? LastModified { get; set; }
        public double? Priority { get; set; }
        public string Url { get; set; }
    }

    public enum SitemapFrequency
    {
        Never,
        Yearly,
        Monthly,
        Weekly,
        Daily,
        Hourly,
        Always
    }

    [Authorize()]
    [Route("api/[controller]")]
    public class SiteMapController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly IProductsRepository Products;
        private readonly IGroupsRepository Groups;
        public SiteMapController(
            IConfiguration configuration,
            IGroupsRepository groupsRepository,
            IProductsRepository productsRepository)
        {
            Groups = groupsRepository;
            Products = productsRepository;
            _configuration = configuration;
        }

        private async Task<IReadOnlyCollection<SitemapNode>> GetSitemapNodes()
        {
            List<SitemapNode> nodes = new List<SitemapNode>();

            var domain = _configuration["Store:SiteDomain"] + "/";

            var groups = (await Groups.GetAllGroups())
                .Where(i => i.Visible).ToArray();

            var products = (await Products.GetAllProducts());

            var grouped = products
                .Where(i => CanBeOrdered(i))
                .GroupBy(i => i.GroupId)
                .Where(i => groups.Any(g => g.Id == i.Key) && i.Count() > 0).ToArray();

            foreach (var group in grouped)
            {
                var groupInfo = groups.First(i => i.Id == group.Key);
                nodes.Add(
                new SitemapNode()
                {
                    Url = domain + "group/" + group.Key.ToString(),
                    Frequency = SitemapFrequency.Weekly,
                    Priority = 0.5,
                    LastModified = GetLatest(
                        GetLatest(
                            GetLatest(group.ToArray()),
                            groupInfo.Created),
                        groupInfo.LastChange).ToUniversalTime()
                });

                foreach (var product in group)
                {
                    nodes.Add(
                       new SitemapNode()
                       {
                           Url = domain + "product/" + product.Id.ToString(),
                           Frequency = SitemapFrequency.Weekly,
                           Priority = 0.8,
                           LastModified = GetLastModify(product)
                       });
                }
            }            

            return nodes;
        }

        private BsonDateTime GetLatest(Model.Data.Product[] products)
        {
            var latest = GetLatest(products[0].Created, products[0].LastChange);
            for (int i = 1; i < products.Length; i++)
                latest = GetLatest(latest, GetLatest(products[i].Created, products[i].LastChange));
            return latest;
        }

        private BsonDateTime GetLatest(BsonDateTime left, BsonDateTime right)
        {
            if (left == null)
                return right;
            if (right == null)
                return left;
            if (left > right)
                return left;
            else
                return right;
        }

        private DateTime? GetLastModify(Model.Data.Product product)
        {
            if (product.LastChange==null)
                return product.Created.ToUniversalTime();
            else
                return product.LastChange.ToUniversalTime();
        }

        private bool CanBeOrdered(Model.Data.Product product)
        {
            return !product.IsArchived &&
                product.Variations.Any(i => !i.IsArchived && i.CanBeOrdered);
        }

        private string GetSitemapDocument(IEnumerable<SitemapNode> sitemapNodes)
        {
            XNamespace xmlns = "http://www.sitemaps.org/schemas/sitemap/0.9";
            XElement root = new XElement(xmlns + "urlset");

            foreach (SitemapNode sitemapNode in sitemapNodes)
            {
                XElement urlElement = new XElement(
                    xmlns + "url",
                    new XElement(xmlns + "loc", Uri.EscapeUriString(sitemapNode.Url)),
                    sitemapNode.LastModified == null ? null : new XElement(
                        xmlns + "lastmod",
                        sitemapNode.LastModified.Value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:sszzz")),
                    sitemapNode.Frequency == null ? null : new XElement(
                        xmlns + "changefreq",
                        sitemapNode.Frequency.Value.ToString().ToLowerInvariant()),
                    sitemapNode.Priority == null ? null : new XElement(
                        xmlns + "priority",
                        sitemapNode.Priority.Value.ToString("F1", CultureInfo.InvariantCulture)));
                root.Add(urlElement);
            }

            XDocument document = new XDocument(root);
            return document.ToString();
        }

        [AllowAnonymous]
        [HttpGet("sitemap.xml")]
        [Produces("application/xml")]
        public async Task<IActionResult> GenerateSiteMap()
        {
            var sitemapNodes = await GetSitemapNodes();
            string xmlString = GetSitemapDocument(sitemapNodes);
            return Content(xmlString, "application/xml");
        }
    }
}
