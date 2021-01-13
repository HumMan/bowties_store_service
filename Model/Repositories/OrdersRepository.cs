using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using store_service.Helpers;
using store_service.App.Model;
using Data = store_service.App.Model.Data;

namespace store_service.App.Repository.Internal {

    public class OrdersRepository : RepositoryBase, IOrdersRepository {
        private readonly IMongoCollection<Data.Order> _orders;
        private readonly IMongoCollection<Data.Inventory> _inventory;

        /// <summary>
        /// На сколько часов резервируется заказ при оплате при получении (много т.к. нужно подтвердить по телефону)
        /// </summary>
        private const int CashOrderReserve = 24;
        /// <summary>
        /// Время на оплату заказа в яндексе
        /// </summary>
        private const int OrderReserve = 2;

        public OrdersRepository (IConfiguration configuration, IHostingEnvironment env) : base (configuration, env) {
            var database = _client.GetDatabase ("bowties_store2_db");

            _orders = database.GetCollection<Data.Order> ("orders");

            var options = new CreateIndexOptions() { Unique = false };
            var field = new StringFieldDefinition<Data.Order>("UserId");
            var indexDefinition = new IndexKeysDefinitionBuilder<Data.Order>().Ascending(field);
            var indexModel = new CreateIndexModel<Data.Order>(indexDefinition, options);
            _orders.Indexes.CreateOne(indexModel);

            _inventory = database.GetCollection<Data.Inventory>("inventory");
        }
        public async Task<ObjectId?> CreateOrderFromCart (Data.Cart cart, ObjectId userId, CheckoutParameters parameters) {
            
            //cart.LastUpdate

            //TODO проверка ошибок - 0 count
            var orderId = ObjectId.GenerateNewId ();

            await _orders.InsertOneAsync(new Data.Order {
                Id = orderId,
                OrderId = "JK-"+DateTime.UtcNow.ToString("MMdd-HHmmss"),
                    Items = cart.Items.Select(i=>new Data.OrderItem {
                        ProductId = i.ProductId,
                        VariationId = i.VariationId,
                        Count = i.Count,
                        Price = i.Price
                    }).ToList(),
                    Created = DateTime.UtcNow,
                    ReservedUntil = parameters.PaymentType == PaymentType.Cash ? DateTime.UtcNow.AddHours(CashOrderReserve): DateTime.UtcNow.AddHours(OrderReserve),
                    Status = OrderStatus.Creating,
                    UserId = userId,
                    Parameters = parameters,
                    Total = PriceCalc.CalcTotal(cart.Items, parameters.DeliveryType),
                    DeliveryPrice = parameters.DeliveryType==DeliveryType.Mail?PriceCalc.CalcPostDeliveryPrice(cart.Items):(decimal?)null
            });

            if (await ReserveInventory (cart.Items, orderId)) {
                if (await ChangeOrderStatus (orderId, OrderStatus.Creating,
                        parameters.PaymentType == PaymentType.Cash? OrderStatus.WaitingApprove : OrderStatus.WaitingPayment
                    )) {
                    
                } else {
                    await RollbackOrderInventory (orderId);
                    await DeleteOrder (orderId, OrderStatus.Creating);
                    return null;
                }
            } else {
                await RollbackOrderInventory (orderId);
                await DeleteOrder (orderId, OrderStatus.Creating);
                return null;
            }

            return orderId;
        }
        
        private async Task<bool> ReserveInventory (List<Data.CartItem> items, ObjectId orderId) {
            foreach (var cartItem in items)
            {
                if (!cartItem.WithoutCount)
                {
                    if (!await ReserveInventory(orderId, cartItem.VariationId, cartItem.Count))
                        return false;
                }
            }
            return true;
        }

        private async Task<bool> ReserveInventory (ObjectId orderId, ObjectId variationId, int count) {
            var availableFilter = Builders<Data.Inventory>.Filter.And (
                Builders<Data.Inventory>.Filter.Eq (i => i.VariationId, variationId),
                Builders<Data.Inventory>.Filter.Eq (i => i.State, InventoryState.Available)
            );
            //находим нужное кол-во productId Available и помечаем их orderId Reserved
            long needToReserve = count;
            do {
                var idsList = (await (await _inventory.FindAsync (availableFilter,
                    new FindOptions<Data.Inventory, SimpleId> {
                        Limit = (int) needToReserve,
                        Projection = Builders<Data.Inventory>.Projection.Include (x => x.Id)
                    })).ToListAsync ()).Select (i => i.Id).ToArray ();

                if (idsList.Length == needToReserve) {
                    var result = (await _inventory.UpdateManyAsync (
                        Builders<Data.Inventory>.Filter.And (
                            Builders<Data.Inventory>.Filter.In (x => x.Id, idsList),
                            Builders<Data.Inventory>.Filter.Eq (x => x.State, InventoryState.Available)
                        ),
                        Builders<Data.Inventory>.Update
                        .Set (x => x.State, InventoryState.Reserved)
                        .Set (x => x.OrderId, orderId)
                    ));
                    needToReserve -= result.ModifiedCount;
                } else {
                    return false;
                }
            } while (needToReserve > 0);
            return true;
        }
        private async Task<int> RollbackOrderInventory (ObjectId orderId) {
            var result = await _inventory.UpdateManyAsync (
                Builders<Data.Inventory>.Filter.Eq (x => x.OrderId, orderId),
                Builders<Data.Inventory>.Update
                .Set (x => x.State, InventoryState.Available)
                .Set (x => x.OrderId, ObjectId.Empty));
            return (int) result.ModifiedCount;
        }
        private async Task<bool> DeleteOrder (ObjectId orderId, OrderStatus status) {
            var result = await _orders.DeleteOneAsync (Builders<Data.Order>.Filter.And (
                Builders<Data.Order>.Filter.Eq (x => x.Id, orderId),
                Builders<Data.Order>.Filter.Eq (x => x.Status, status)));
            return result.DeletedCount == 1;
        }
        public async Task<bool> ChangeOrderStatus (ObjectId orderId, OrderStatus status, OrderStatus changeToStatus) {
            var result = await _orders.UpdateOneAsync (Builders<Data.Order>.Filter.And (
                    Builders<Data.Order>.Filter.Eq (x => x.Id, orderId),
                    Builders<Data.Order>.Filter.Eq (x => x.Status, status)),
                Builders<Data.Order>.Update.Set (i => i.Status, changeToStatus)
            );
            return result.ModifiedCount == 1;
        }
        public async Task CleanInvalidOrders (OrderStatus status, DateTime changedBefore) {
            var filter = Builders<Data.Order>.Filter.And (
                Builders<Data.Order>.Filter.Eq (x => x.Status, status),
                Builders<Data.Order>.Filter.Lt (x => x.Created, changedBefore)
            );

            do {
                var orderIds = await (await _orders.FindAsync (filter,
                    new FindOptions<Data.Order, SimpleId> {
                        Projection = Builders<Data.Order>.Projection.Include (x => x.Id)
                    })).ToListAsync ();
                if (orderIds.Count > 0) {
                    foreach (var order in orderIds) {
                        await RollbackOrderInventory (order.Id);
                        await ChangeOrderStatus (order.Id, status, OrderStatus.Deleted);
                    }
                } else
                    break;

            } while (true);
        }
        public async Task RollbackExpiredOrders (OrderStatus status) {
            var filter = Builders<Data.Order>.Filter.And (
                Builders<Data.Order>.Filter.Eq (x => x.Status, status),
                Builders<Data.Order>.Filter.Lt (x => x.ReservedUntil, DateTime.UtcNow)
            );

            do {
                var orderIds = await (await _orders.FindAsync (filter,
                    new FindOptions<Data.Order, SimpleId> {
                        Projection = Builders<Data.Order>.Projection.Include (x => x.Id)
                    })).ToListAsync ();
                if (orderIds.Count > 0) {
                    foreach (var order in orderIds) {
                        await RollbackOrderInventory (order.Id);
                        await ChangeOrderStatus (order.Id, status, OrderStatus.Deleted);
                    }
                } else
                    break;

            } while (true);
        }
        public async Task<Data.Order> FindOrder (ObjectId orderId) {
            var filter = Builders<Data.Order>.Filter.Eq (x => x.Id, orderId);
            var result = await (await _orders.FindAsync (filter)).ToListAsync ();
            if (result.Count == 1)
                return result.First ();
            else
                return null;
        }

        public async Task<List<Data.Order>> GetAll()
        {
            var result = await (await _orders.FindAsync((i) => true)).ToListAsync();
            return result;
        }

        public async Task<Data.Order> GetUserOrder(ObjectId userId, ObjectId orderId)
        {
            var filter = Builders<Data.Order>.Filter.And(
                Builders<Data.Order>.Filter.Eq(x => x.Id, orderId),
                Builders<Data.Order>.Filter.Eq(x => x.UserId, userId));

            var result = await (await _orders.FindAsync(filter)).ToListAsync();
            if (result.Count == 1)
                return result.First();
            else
                return null;
        }

        public async Task<List<Data.Order>> GetAllUserOrders(ObjectId userId)
        {
            var filter = 
                Builders<Data.Order>.Filter.Eq(x => x.UserId, userId);

            var result = await (await _orders.FindAsync(filter)).ToListAsync();
            return result;
        }
    }
}