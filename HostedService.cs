using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using store_service.App.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using store_service.App.Model;
using Data = store_service.App.Model.Data;

namespace store_service
{
    internal class TimedHostedService : IHostedService, IDisposable
    {
        private readonly ILogger _logger;
        private Timer _timer;
        private readonly IOrdersRepository _repository;

        private readonly IFilesRepository _filesRepo;
        private readonly IGroupsRepository _groupsRepo;
        private readonly IProductsRepository _productsRepo;

        public TimedHostedService(ILogger<TimedHostedService> logger, IServiceProvider services)
        {
            _repository =  services.GetService(typeof(IOrdersRepository)) as IOrdersRepository;
            _filesRepo = services.GetService(typeof(IFilesRepository)) as IFilesRepository;
            _groupsRepo = services.GetService(typeof(IGroupsRepository)) as IGroupsRepository;
            _productsRepo = services.GetService(typeof(IProductsRepository)) as IProductsRepository;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is starting.");

            _timer = new Timer(DoWork, null, TimeSpan.Zero,
                TimeSpan.FromMinutes(10));

            return Task.CompletedTask;
        }

        private void DoWork(object state)
        {
            //TODO лог всего кривого что есть

            //очищаем зависшие заказы, если есть
            _repository.CleanInvalidOrders(OrderStatus.Creating, DateTime.UtcNow.AddMinutes(-5)).Wait();

            //очистка просроченных заказов - т.к. происходит резервирование товара, товар обратно в Available
            _repository.RollbackExpiredOrders(OrderStatus.WaitingPayment).Wait();
            _repository.RollbackExpiredOrders(OrderStatus.WaitingApprove).Wait();
            //TODO remove unused images
            //TODO Удаление доступного инвентаря для архивных товаров (могут появиться после очистки/снятия резервирования товаров)

            //CheckImagesConsistency().Wait();
        }

        private async Task CheckImagesConsistency()
        {
            var products = await _productsRepo.GetAllProducts(filterArchived: false);
            List<Data.ImageDesc> allImages = new List<Data.ImageDesc>();
            allImages.AddRange(products.SelectMany(i => i.Images).ToArray());

            var groups = await _groupsRepo.GetAllGroups();
            allImages.AddRange(groups.Select(i => i.Image).ToArray());

            var allFiles = (await _filesRepo.GetAllFilesDesc()).ToDictionary(i=>i.Id);

            var invalidImageDesc = new List<Data.ImageDesc>();
            foreach(var image in allImages)
            {
                var exists = allFiles.TryGetValue(image.Id, out var desc);
                if (!exists)
                    invalidImageDesc.Add(image);
                foreach(var thumb in image.ThumbIds)
                {
                    exists = allFiles.TryGetValue(thumb.Value, out desc);
                    if (!exists)
                        invalidImageDesc.Add(image);
                }
            }

            var allImageIds = new List<MongoDB.Bson.ObjectId>();
            allImageIds.AddRange(allImages.Select(i => i.Id));
            allImageIds.AddRange(allImages.SelectMany(i => i.ThumbIds).Select(i=>i.Value));

            var allImageIdsDict = allImageIds.ToDictionary(i => i);

            var invalidImageFiles = new List<MongoDB.Driver.GridFS.GridFSFileInfo>();

            foreach (var file in allFiles)
            {
                var exists = allImageIdsDict.TryGetValue(file.Key, out var desc);
                if (!exists)
                    invalidImageFiles.Add(file.Value);
            }

            foreach(var file in invalidImageFiles)
            {
                await _filesRepo.DeleteFile(file.Id);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Timed Background Service is stopping.");

            _timer?.Change(Timeout.Infinite, 0);

            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
