using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using MongoDB.Bson;
using MongoDB.Driver.GridFS;

using store_service.App.Repository;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Model = store_service.App.Model;

namespace store_service.Controllers
{
    [Authorize()]
    [Produces("application/json")]
    [Route("api/[controller]")]
    public class ImageController : Controller
    {
        private IHostingEnvironment hostingEnvironment;
        private readonly IFilesRepository Repository;

        public ImageController(IHostingEnvironment hostingEnvironment, IFilesRepository Repository)
        {
            this.hostingEnvironment = hostingEnvironment;
            this.Repository = Repository;
        }       
        [Authorize(Roles = "Manager")]
        [HttpPost("thumb")]
        public async Task<Model.Web.ImageDesc> UploadImagesWithThumb()
        {
            try
            {
                var result = new Model.Web.ImageDesc();
                //без этого далее падает с внутренней ошибкой "System.Private.CoreLib"
                var file = Request.Form.Files[0];

                if (file.Length > 0)
                {
                    using (var stream = file.OpenReadStream())
                    {
                        result.Id = Model.Converters.Converter.ToWeb(await Repository.SaveFile(file.FileName, stream));
                    }
                }

                await Helpers.Images.CreateThumbnails(result, file, Repository);

                GC.Collect();

                return result;
            }
            catch (System.Exception ex)
            {
                //return Json("Upload Failed: " + ex.Message);
                return null;
            }
        }        
        [AllowAnonymous]
        [HttpGet()]
        public async Task Download(string id)
        {
            try
            {
                using (Response.Body)
                {
                    if (ObjectId.TryParse(id, out var objectId))
                    {
                        using (var readStream = await Repository.OpenDownloadStream(objectId))
                        {
                            var provider = new FileExtensionContentTypeProvider();
                            var fileName = readStream.FileInfo.Filename;
                            if (provider.TryGetContentType(fileName, out string contentType))
                                Response.ContentType = contentType;
                            Response.StatusCode = (int)HttpStatusCode.OK;
                            Response.Headers.Add("Content-Disposition", "filename=" + WebUtility.UrlEncode(fileName));
                            Response.ContentLength = readStream.FileInfo.Length;

                            await readStream.CopyToAsync(Response.Body);
                        }
                    }
                    else
                    {
                        if (!Response.HasStarted)
                            Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    }
                }
            }
            catch (GridFSFileNotFoundException ex)
            {
                if (!Response.HasStarted)
                    Response.StatusCode = (int)HttpStatusCode.NotFound;
            }
            catch (Exception ex)
            {
                if (!Response.HasStarted)
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }
    }
}