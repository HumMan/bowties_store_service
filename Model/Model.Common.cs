using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;

namespace store_service.App.Model
{
    public enum UserRole
    {
        //Anonymous=0, - если RegisteredUser==null то это анонимная сессия (на фронте роль 0)
        Regular=1,
        Manager=2,
        Admin=3
    }

    public enum InventoryState
    {
        Available,
        Reserved
    }

    public enum OrderStatus
    {
        /// <summary>
        /// Заказ создаётся, зависшие заказы очищаются, товар возвращается в Available
        /// Переходы:
        /// Наличными при получении (только самовывоз) -> WaitingApprove
        /// Онлайн оплата -> WaitingPayment
        /// Не удалось зарезервировать -> Deleted
        /// </summary>
        Creating,
        /// <summary>
        /// Заказ ждёт оплаты или подтверждения, товар зарезервирован
        /// Через 2 часа будет отменён, если не поступит оплата
        /// Переходы:
        /// Почта -> Packing
        /// Самовывоз -> Shipping
        /// Оплата не выполнена вовремя -> Deleted
        /// </summary>
        WaitingPayment,
        /// <summary>
        /// Заказ ждёт подтверждения, товар зарезервирован (возможно продление резервирования)
        /// Через 24 часа будет отменён
        /// Переходы:
        /// Самовывоз подтверждён -> Shipping
        /// Самовывоз не подтверждён -> Deleted
        /// </summary>
        WaitingApprove,
        /// <summary>
        /// Удалён не оплаченный или не подтвержденный заказ, товар возвращается в Available
        /// </summary>
        Deleted,
        /// <summary>
        /// Для почтового отправления - готовится к отправке
        /// Переходы:
        /// -> Shipping
        /// </summary>
        Packing,
        /// <summary>
        /// Отправлен или ждёт самовывоза
        /// Переходы:
        /// -> Shipped
        /// </summary>
        Shipping,
        /// <summary>
        /// Заказ доставлен, либо передан, все зарезервированные товары очищаются чтобы не лежать мертвым грузом
        /// </summary>
        Shipped
    }

    public class ProductProperties
    {
        public string Color { get; set; }
        public string SecondaryColor { get; set; }
        public string Type { get; set; }
    }

    public class ProfileAddress
    {
        public string PostalCode { get; set; }
        public string Region { get; set; }
        public string City { get; set; }
        public string Address { get; set; }
    }

    /// <summary>
    /// Используется для подсчёта цены и стоимости доставки
    /// </summary>
    public class CartItemMarketBase
    {
        public int Count { get; set; }
        /// <summary>
        /// В Model.Data используется как временная переменная, цена берётся из товара
        /// </summary>
        public int Price { get; set; }        
        //TODO weight
    }

    public enum DeliveryType
    {
        Mail,
        SelfPickup
    }

    public enum PaymentType
    {
        YandexWallet,
        YandexCard,
        Cash
    }

    [BsonIgnoreExtraElements]
    public class CheckoutParameters
    {
        public DeliveryType DeliveryType { get; set; }
        public string OtherInfo { get; set; }
        public PaymentType PaymentType { get; set; }
        public string Email { get; set; }
        public string Name { get; set; }
        //public string VKAccount { get; set; }
        public string Phone { get; set; }
        public ProfileAddress DeliveryAddress { get; set; }
    }
}