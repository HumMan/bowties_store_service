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

namespace store_service.App.Repository.Internal {

    public class FilesRepository : RepositoryBase, IFilesRepository {
        private readonly GridFSBucket _files;
        public FilesRepository (IConfiguration configuration, IHostingEnvironment env) 
        : base (configuration, env) {

            var filesDatabase = _client.GetDatabase ("bowties_store2_files");
            _files = new GridFSBucket (filesDatabase, new GridFSBucketOptions {
                BucketName = "files",
                    ChunkSizeBytes = 1048576
            });
        }
        public async Task LoadFile (ObjectId id, Stream destination) {
            await _files.DownloadToStreamAsync (id, destination);
        }

        public async Task<List<GridFSFileInfo>> GetAllFilesDesc()
        {
            var result = await _files.Find(Builders<GridFSFileInfo>.Filter.Empty).ToListAsync();
            return result;
        }

        public async Task DeleteFile(ObjectId id)
        {
            await _files.DeleteAsync(id);
        }

        public async Task<GridFSDownloadStream> OpenDownloadStream (ObjectId id) {
            return await _files.OpenDownloadStreamAsync (id);
        }
        public async Task<ObjectId> SaveFile (string fileName, Stream file) {
            return await _files.UploadFromStreamAsync (fileName, file);
        }
        public async Task<string> GetFileName (ObjectId id) {
            var filter = Builders<GridFSFileInfo>.Filter.Eq (i => i.Id, id);
            var result = await _files.Find (filter).FirstOrDefaultAsync ();
            if (result != null)
                return result.Filename;
            else
                return null;
        }

    }
}