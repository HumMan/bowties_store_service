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
using Data = store_service.App.Model.Data;
using store_service.App.Model;

namespace store_service.App.Repository
{
    class SimpleId
    {
        public ObjectId Id { get; set; }
    }

    public interface IEmailVerifyTicketRepository
    {
        Task<Data.UserTicket> TryTicket(string tokenHash);
        Task CreateTicket(ObjectId userId, string email, string tokenHash, DateTime expirationDate);
    }

    public interface IPasswordResetTicketRepository
    {
        Task<Data.UserTicket> TryTicket(string tokenHash);
        Task CreateTicket(ObjectId userId, string email, string tokenHash, DateTime expirationDate);
        Task<bool> IsValidTicket(string token);
    }

    public interface IUsersRepository
    {
        Task<Data.User> TryLogin(string email, string password);
        Task<Data.User> TryLogin(ObjectId userId, string password);
        Task<Data.User> FindUser(string email);
        Task<Data.User> FindUser(ObjectId id);
        Task SetUserEmailValid(ObjectId id, bool value);
        Task<Data.User[]> GetUsers();
        Task<Data.User> RegisterUser(string userName, string password, string email);
        //Task UpdateUser(Data.User user);
        Task<Data.User> CreateTempUser();
        Task ChangeUserPassword(string email, ObjectId userId, string newPassword);
        Task UpdateCheckoutParams(ObjectId userId, CheckoutParameters parameters);
    }

    public class GroupCount
    {
        public ObjectId Id { get; set; }
        public int Count { get; set; }
    }

    public interface IProductsRepository
    {
        Task<string[]> GetAllColors();
        Task<Data.Product[]> GetAllProducts(ObjectId? group = null, bool filterUnavailable = false, bool filterArchived = true);
        Task<Data.Product[]> GetAllProducts(ObjectId[] group, bool filterUnavailable = false);
        Task<Data.Product> GetProduct(ObjectId id);
        Task SetProductOrder(ObjectId id, int order);
        Task CreateProduct(Data.Product product);
        Task UpdateProduct(Data.Product product);
        Task UpdateProductImages(ObjectId id, List<Data.ImageDesc> images);
        Task DeleteProduct(ObjectId productId);
        Task UpdateVariationInventory(ObjectId productId, ObjectId variationId, int inventoryCount);
        Task<int> GetInventoryAvailable(ObjectId variationId);
        Task<GroupCount[]> GetAllInventoryAvailable();
        Task<int[]> GetInventoryAvailable(ObjectId[] variationId);

    }
    public interface IPaymentsRepository
    {
        Task InsertPaymentNotify(Data.PaymentNotify notify);
    }

    public interface IOrdersRepository
    {
        Task<ObjectId?> CreateOrderFromCart(Data.Cart cart, ObjectId userId, CheckoutParameters parameters);
        Task CleanInvalidOrders(OrderStatus status, DateTime changedBefore);
        Task<Data.Order> FindOrder(ObjectId orderId);
        Task<Data.Order> GetUserOrder(ObjectId userId, ObjectId orderId);
        Task<List<Data.Order>> GetAllUserOrders(ObjectId userId);
        Task<bool> ChangeOrderStatus(ObjectId orderId, OrderStatus status, OrderStatus changeToStatus);
        Task RollbackExpiredOrders(OrderStatus status);
        Task<List<Data.Order>> GetAll();
    }
    public interface IFilesRepository
    {
        Task DeleteFile(ObjectId id);
        Task<List<GridFSFileInfo>> GetAllFilesDesc();
        Task LoadFile(ObjectId id, Stream destination);
        Task<ObjectId> SaveFile(string fileName, Stream file);
        Task<GridFSDownloadStream> OpenDownloadStream(ObjectId id);
        Task<string> GetFileName(ObjectId id);
    }
    public interface ICartsRepository
    {
        Task<Data.Cart> GetUserCart(ObjectId userId);
        Task<bool> UpdateCart(Data.Cart cart);
        Task CreateCart(ObjectId userId);
        Task MergeCarts(ObjectId tempSessionUserId, ObjectId userId);
        Task<List<Data.Cart>> GetAllCarts();
    }
    public interface IGroupsRepository
    {
        Task<List<Data.Group>> GetAllGroups(bool filterArchivedGroups = true);
        Task<Data.Group> GetGroup(ObjectId id);
        Task CreateGroup(Data.Group group);
        Task UpdateGroup(Data.Group group);
        Task DeleteGroup(ObjectId id);
        Task SetGroupOrder(ObjectId id, int order);
        Task UpdateGroupImages(ObjectId id, Data.ImageDesc image);
    }
}