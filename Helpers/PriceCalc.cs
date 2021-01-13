using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using Model = store_service.App.Model;

namespace store_service.Helpers
{

    public static class PriceCalc
    {

        class TPriceItem
        {
            public int MinWeight { get; set; }
            public int MaxWeight { get; set; }
            public decimal ShippingPrice { get; set; }
        }

        public static decimal CalcTotal(IEnumerable<Model.CartItemMarketBase> items, Model.DeliveryType type)
        {
            return CalcItemsPrice(items) + (type == Model.DeliveryType.Mail ? CalcPostDeliveryPrice(items) : 0);
        }

        public static decimal CalcItemsPrice(IEnumerable<Model.CartItemMarketBase> items)
        {
            return items.Aggregate(0M, (current, item) => current + item.Price * item.Count);
        }

        public static decimal CalcPostDeliveryPrice(IEnumerable<Model.CartItemMarketBase> items)
        {
            var priceTable = new TPriceItem[]
                {
                    new TPriceItem { MinWeight = 0, MaxWeight = 100, ShippingPrice = 147.50M },
                    new TPriceItem { MinWeight = 101, MaxWeight = 200, ShippingPrice = 171.10M },
                    new TPriceItem { MinWeight = 201, MaxWeight = 500, ShippingPrice = 247.80M },
                    new TPriceItem { MinWeight = 501, MaxWeight = 1000, ShippingPrice = 389.40M },
                    new TPriceItem { MinWeight = 1001, MaxWeight = 1500, ShippingPrice = 513.30M },
                    new TPriceItem { MinWeight = 1501, MaxWeight = 2000, ShippingPrice = 636.02M },
                    new TPriceItem { MinWeight = 2001, MaxWeight = 2500, ShippingPrice = 759.92M },
                };

            decimal shippingPrice = 0;
            int weight = 0;
            int defaultWeight = 30;
            foreach (var i in items)
            {
                //if (i.Weight.HasValue)
                //    weight += i.Weight.Value * i.Quantity;
                //else
                weight += defaultWeight * i.Count;
            }
            foreach (var p in priceTable)
            {
                if (p.MinWeight <= weight && weight <= p.MaxWeight)
                {
                    shippingPrice = p.ShippingPrice;
                    break;
                }
            }

            //добавить ограничение на количество бабочек в заказе
            if (shippingPrice == 0)
            {
                shippingPrice = 800;
            }

            if (shippingPrice < 180)
                shippingPrice = 180;

            return shippingPrice;
        }
    }
}