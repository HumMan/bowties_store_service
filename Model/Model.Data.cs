using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using Omu.ValueInjecter;
using store_service.App.Model.Converters;

namespace store_service.App.Model {

    namespace Data {       

        public class UserProfile {
            //public string VKAccount { get; set; }
            public string Phone { get; set; }
            public List<ProfileAddress> Address { get; set; }
        }

        /// <summary>
        /// Данные для зарегистрированных пользователей
        /// Для TempSession = null
        /// </summary>
        public class RegisteredUser
        {
            public UserRole Role { get; set; }
            /// <summary>
            /// Рассылка оповещений/уведомлений на почту происходит только для
            /// польз. с верифицированной почтой.
            /// 
            /// После регистрации польз. приходит письмо со ссылкой для потверждения.
            /// В профиле есть кнопка для верификации почты + вкл./выкл. почтовых оповещений
            /// без верификации оповещения на email включить нельзя
            /// 
            /// Так же если пользователь "Забыл пароль", после восстановления пароля
            /// можно считать его верифицированным
            /// 
            /// </summary>
            public bool IsEmailVerified { get; set; }
            public string Name { get; set; }
            public string PasswordHash { get; set; }
            public string PasswordSalt { get; set; }
            //public UserProfile UserProfile { get; set; }
            public BsonDateTime PasswordLastChange { get; set; }
        }

        public class User {
            public ObjectId Id { get; set; }
            public string Email { get; set; }
            
            public RegisteredUser RegisteredUser { get; set; }
            /// <summary>
            /// Запоминаем что пользователь вводил на странице оформления заказа
            /// </summary>
            public CheckoutParameters CheckoutParameters { get; set; }

            public BsonDateTime Created { get; set; }            

            public Web.User ToWeb () {
                var result = new Web.User ();
                result.InjectFrom (this);
                if(RegisteredUser!=null)
                {
                    result.IsEmailVerified = RegisteredUser.IsEmailVerified;
                    result.Name = RegisteredUser.Name;
                }
                if (Id != null)
                    result.Id = Converter.ToWeb (Id);
                return result;
            }
        }

        public class CartItem : CartItemMarketBase
        {
            public ObjectId ProductId { get; set; }
            public ObjectId VariationId { get; set; }
            /// <summary>
            /// Для данной вариации нет кол-ва, можно не резервировать (устанавливается отдельно)
            /// </summary>
            public bool WithoutCount { get; set; }
            public Web.CartItem ToWeb () {
                var result = new Web.CartItem ();
                result.InjectFrom (this);
                result.ProductId = Converter.ToWeb(ProductId);
                result.VariationId = Converter.ToWeb (VariationId);
                return result;
            }
        }

        public class Cart {
            [BsonElement("_id")]
            public ObjectId UserId { get; set; }
            public BsonDateTime LastUpdate { get; set; }
            public List<CartItem> Items { get; set; } = new List<CartItem> ();
            public Web.Cart ToWeb () {
                var result = new Web.Cart ();
                result.InjectFrom (this);
                result.Id = Converter.ToWeb (UserId);
                if (Items != null)
                    result.Items = Items.Select (i => i.ToWeb ()).ToList ();
                if (LastUpdate != null)
                {
                    result.Timestamp = Converter.ToWeb(LastUpdate);
                }
                return result;
            }
        }

        public class Inventory {
            public ObjectId Id { get; set; }
            public ObjectId ProductId { get; set; }
            public ObjectId VariationId { get; set; }
            public ObjectId OrderId { get; set; }
            public InventoryState State { get; set; }
        }


        public class Product {
            public ObjectId Id { get; set; }
            //TODO - ReadableId - транслтерация названия
            public ObjectId GroupId { get; set; }
            public string Title { get; set; }
            public string Description { get; set; }
            /// <summary>
            /// Подробное описание на странице товара
            /// </summary>
            public string LongDescription { get; set; }

            public BsonDateTime Created { get; set; }
            public BsonDateTime LastChange { get; set; }
            public ProductProperties Properties { get; set; } = new ProductProperties();
            public List<ImageDesc> Images { get; set; }
            public bool IsArchived {get;set;}
            public List<ProductVariation> Variations { get; set; } = new List<ProductVariation> ();
            public int Order { get; set; }

            public Web.Product ToWeb (int[] inventoryCount) {
                var result = new Web.Product ();
                result.InjectFrom (this);
                result.Id = Converter.ToWeb (Id);
                result.GroupId = Converter.ToWeb (GroupId);
                result.Created = Converter.ToWeb(Created);
                if(LastChange!=null)
                    result.LastChange = Converter.ToWeb(LastChange);
                if (Properties != null)
                    result.Properties.InjectFrom (Properties);
                if (Images != null)
                    result.Images = Images.Select (x => x.ToWeb ()).ToList ();
                    
                for(int i=0;i<inventoryCount.Length;i++)
                {
                    result.Variations.Add(Variations[i].ToWeb(inventoryCount[i]));
                }
                return result;
            }

        }

        public class ProductVariation {
            /// <summary>
            /// Для товара нет инвентаря, изготавливается под заказ
            /// </summary>
            /// <value></value>
            public bool WithoutCount { get; set; }
            public ObjectId Id { get; set; }
            /// <summary>
            /// Составное название варианта (Бабочка Детская/С застёжкой)
            /// </summary>
            /// <value></value>
            public string Title { get; set; }
            public bool IsArchived { get; set; } = false;
            public bool CanBeOrdered { get; set; } = false;
            public int Price { get; set; }
            /// <summary>
            /// Список идентификаторов варианта товара в соответствии с настрой
            /// </summary>
            /// <typeparam name="ObjectId"></typeparam>
            /// <returns></returns>
            public List<VariationId> VariationIds { get; set; } = new List<VariationId> ();

            internal Web.ProductVariation ToWeb (int inventoryCount) {
                var result = new Web.ProductVariation ();
                result.InjectFrom (this);
                result.Id = Converter.ToWeb (Id);
                result.VariationIds = VariationIds.Select (x => x.ToWeb ()).ToList ();
                result.InventoryCount = inventoryCount;
                return result;
            }
        }
        public class VariationId {
            public ObjectId ParameterId { get; set; }
            public ObjectId ParameterValueId { get; set; }

            internal Web.VariationId ToWeb () {
                var result = new Web.VariationId ();
                result.InjectFrom (this);
                result.ParameterId = Converter.ToWeb (ParameterId);
                result.ParameterValueId = Converter.ToWeb (ParameterValueId);
                return result;
            }
        }
        public class VariationParameterValue {
            public ObjectId Id { get; set; }
            public string Title { get; set; }
            public bool IsArchived { get; set; }
            internal Web.VariationParameterValue ToWeb () {
                var result = new Web.VariationParameterValue ();
                result.InjectFrom (this);
                result.Id = Converter.ToWeb (Id);
                return result;
            }
        }
        public class VariationParameter {
            public ObjectId Id { get; set; }
            public string Title { get; set; }            
            public List<VariationParameterValue> Values { get; set; } = new List<VariationParameterValue> ();
            internal Web.VariationParameter ToWeb () {
                var result = new Web.VariationParameter ();
                result.InjectFrom (this);
                result.Id = Converter.ToWeb (Id);
                result.Values = Values.Select (x => x.ToWeb ()).ToList ();
                return result;
            }
        }
        public class Group {
            public ObjectId Id { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public bool IsArchived{get;set;}
            public bool Visible { get; set; }
            public int Order { get; set; }
            public ImageDesc Image{get;set;}
            public BsonDateTime Created { get; set; }
            public BsonDateTime LastChange { get; set; }
            public List<VariationParameter> VariationParameters { get; set; } = new List<VariationParameter> ();
            internal Web.Group ToWeb () {
                var result = new Web.Group ();
                result.InjectFrom (this);
                result.Id = Converter.ToWeb (Id);
                if(Image!=null)
                    result.Image = Image.ToWeb();
                result.VariationParameters = VariationParameters.Select (x => x.ToWeb ()).ToList ();
                return result;
            }
        }

        public class ImageDesc {
            public ObjectId Id { get; set; }
            public Dictionary<string, ObjectId> ThumbIds { get; set; }
            public Web.ImageDesc ToWeb () {
                var result = new Web.ImageDesc ();
                result.InjectFrom (this);
                result.Id = Converter.ToWeb (Id);
                result.ThumbIds = ThumbIds.ToDictionary (x => x.Key, x => Converter.ToWeb (x.Value));
                return result;
            }
        }

        public class PaymentNotify {
            public ObjectId Id { get; set; }
            public string Request { get; set; }
            public BsonDateTime Timestamp { get; set; }
            public bool ChecksumValid { get; set; }
            public ObjectId OrderId { get; set; }
        }

        //public class ProductInfo
        //{
        //    public string Title { get; set; }
        //    public string Description { get; set; }
        //    public ImageDesc Image { get; set; }
        //    public string VariationTitle { get; set; }
        //}

        public class OrderItem: CartItemMarketBase
        {
            public ObjectId ProductId { get; set; }
            public ObjectId VariationId { get; set; }
            //public ProductInfo ProductInfo { get; set; }
            public Web.OrderItem ToWeb()
            {
                var result = new Web.OrderItem();
                result.InjectFrom(this);
                result.ProductId = Converter.ToWeb(ProductId);
                result.VariationId = Converter.ToWeb(VariationId);
                return result;
            }
        }

        public class Order {
            public ObjectId Id { get; set; }
            /// <summary>
            /// Читабельный номер заказа на основе даты и времени (BT-MMDD-hhmmss)
            /// </summary>
            public string OrderId { get; set; }
            public decimal Total { get; set; }
            public decimal? DeliveryPrice { get; set; }
            public ObjectId UserId { get; set; }
            public OrderStatus Status { get; set; }
            public BsonDateTime Created { get; set; }
            public BsonDateTime ReservedUntil { get; set; }
            public List<OrderItem> Items { get; set; } = new List<OrderItem>();
            public CheckoutParameters Parameters { get; set; }
            public Web.Order ToWeb () {
                var result = new Web.Order ();
                result.InjectFrom (this);
                result.Id = Converter.ToWeb (Id);
                result.UserId = Converter.ToWeb(UserId);
                result.Created = Converter.ToWeb(Created);
                result.ReservedUntil = Converter.ToWeb(ReservedUntil);
                result.Items = Items.Select(i=>i.ToWeb()).ToList();
                return result;
            }
        }

        /// <summary>
        /// Токен для подтверждения почты или сброса пароля
        /// </summary>
        public class UserTicket {
            public ObjectId Id { get; set; }
            /// <summary>
            /// почта на которую было отправлено уведомление
            /// </summary>
            public string Email { get; set; }
            public string TokenHash { get; set; }
            public BsonDateTime ExpirationDate { get; set; }
            public bool IsUsed { get; set; }
            public Web.ResetTicket ToWeb () {
                var result = new Web.ResetTicket ();
                result.InjectFrom (this);
                //result.UserId = Converter.ToWeb(UserId);
                result.ExpirationDate = Converter.ToWeb (ExpirationDate);
                return result;
            }
        }
    }    
}