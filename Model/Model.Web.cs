using System;
using System.Collections.Generic;
using System.Linq;
using store_service.App.Model;

namespace store_service.App.Model
{
    namespace Web
    {
        public class User
        {
            public string Id { get; set; }
            public string Email { get; set; }
            public bool IsEmailVerified { get; set; }
            public string Name { get; set; }
            public UserRole Role { get; set; }
        }        
        public class CartItem: CartItemMarketBase
        {
            public string ProductId { get; set; }
            public string VariationId { get; set; }
            public Product Product { get; set; }            
            //поля из вариации для ускорения
            public int Available { get; set; }
            public bool WithoutCount { get; set; }            
            public string Title { get; set; }
        }
        public class Cart
        {
            public string Id { get; set; }
            public List<CartItem> Items { get; set; } = new List<CartItem>();
            public decimal DeliveryPrice { get; set; }
            public decimal ItemsTotal { get; set; }
            public long Timestamp { get; set; }
        }
        public class Product
        {
            public string Id { get; set; }
            public string GroupId { get; set; }
            public Group Group { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            public string LongDescription { get; set; }
            public long Created { get; set; }
            public long? LastChange { get; set; }
            public ProductProperties Properties { get; set; } = new ProductProperties();
            public List<ImageDesc> Images { get; set; }
            public bool IsArchived { get; set; }
            public List<ProductVariation> Variations { get; set; } = new List<ProductVariation>();
            public int Order { get; set; }
        }
        public class ProductVariation
        {
            /// <summary>
            /// Для товара нет инвентаря, изготавливается под заказ
            /// </summary>
            /// <value></value>
            public bool WithoutCount { get; set; }
            public string Id { get; set; }
            /// <summary>
            /// Составное название варианта (Бабочка Детская/С застёжкой)
            /// </summary>
            /// <value></value>
            public string Title { get; set; }
            public bool IsArchived { get; set; }
            public bool CanBeOrdered { get; set; }
            public int InventoryCount { get; set; }
            public int Price { get; set; }
            /// <summary>
            /// Список идентификаторов варианта товара
            /// Если null, то у товара нет вариаций
            /// </summary>
            /// <typeparam name="ObjectId"></typeparam>
            /// <returns></returns>
            public List<VariationId> VariationIds { get; set; } = new List<VariationId>();
        }
        public class VariationId
        {
            public string ParameterId { get; set; }
            public string ParameterValueId { get; set; }
        }
        public class VariationParameterValue
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public bool IsArchived { get; set; }
        }
        public class VariationParameter
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public List<VariationParameterValue> Values { get; set; } = new List<VariationParameterValue>();
        }
        public class Group
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public int Order { get; set; }
            public bool Visible { get; set; }
            public ImageDesc Image { get; set; }
            public List<VariationParameter> VariationParameters { get; set; } = new List<VariationParameter>();
        }
        public class ImageDesc
        {
            public string Id { get; set; }
            public Dictionary<string, string> ThumbIds
            {
                get;
                set;
            } = new Dictionary<string, string>();
        }

        public class OrderItem
        {
            public string ProductId { get; set; }
            public string VariationId { get; set; }
            public int Count { get; set; }
            public int Price { get; set; }
        }

        public class Order
        {
            public string Id { get; set; }
            public string OrderId { get; set; }
            public decimal Total { get; set; }
            public decimal? DeliveryPrice { get; set; }
            public string UserId { get; set; }
            public OrderStatus Status { get; set; }
            public long Created { get; set; }
            public long ReservedUntil { get; set; }
            public List<OrderItem> Items { get; set; } = new List<OrderItem>();
            public CheckoutParameters Parameters { get; set; }
        }

        public class ResetTicket
        {
            public long ExpirationDate { get; set; }

        }
    }
}