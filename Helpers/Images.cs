using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Model = store_service.App.Model;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System.IO;
using store_service.App.Repository;
using MongoDB.Bson;

namespace store_service.Helpers
{
    public static class Images
    {
        static readonly IReadOnlyList<int> ThumbTable = new List<int>
        {
            800,400,300,250,200,100,50,25
        };

        public static async Task CreateThumbnails(Model.Web.ImageDesc result, Microsoft.AspNetCore.Http.IFormFile file, IFilesRepository Repository)
        {

            using (var stream = file.OpenReadStream())
            {
                using (var currentImage = Image.Load(stream, new JpegDecoder()))
                {
                    foreach (var t in ThumbTable)
                    {
                        MutateImage(currentImage, t);

                        result.ThumbIds[t.ToString()] = Model.Converters.Converter.ToWeb(
                            await SaveFile(file.FileName, Repository, currentImage, t));
                    }
                }
            }
            Configuration.Default.MemoryAllocator.ReleaseRetainedResources();
        }

        private static void MutateImage(Image currentImage, int size)
        {
            currentImage.Mutate(x => x.Resize(new ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size
                {
                    Height = size,
                    Width = size
                },
                Mode = ResizeMode.Pad
            }).BackgroundColor(new Rgba32(255, 255, 255))
                                        );
        }

        private static async Task<ObjectId> SaveFile(string fileName, IFilesRepository Repository, Image currentImage, int size)
        {
            using (MemoryStream memStream = new MemoryStream())
            {
                currentImage.Save(memStream, new JpegEncoder());
                memStream.Seek(0, SeekOrigin.Begin);
                return await Repository.SaveFile(size + "px-" + fileName, memStream);
            }
        }


        public class UpdateResult
        {
            public int UpdatedCount { get; set; }
            public Model.Data.ImageDesc Result { get; set; }
        }

        public static async Task<UpdateResult> UpdateThumb(Model.Data.ImageDesc input, IFilesRepository Repository)
        {
            int updated = 0;
            Tuple<int, Image> currentImage = null;
            for (int i = 0; i < ThumbTable.Count; i++)
            {
                if (currentImage != null && currentImage.Item1 != i - 1)
                {
                    currentImage.Item2.Dispose();
                    currentImage = null;
                }

                if (!input.ThumbIds.ContainsKey(ThumbTable[i].ToString()))
                {
                    var size = ThumbTable[i];
                    if (currentImage == null)
                    {
                        if (i == 0)
                        {
                            using (var imageStream = await Repository.OpenDownloadStream(input.Id))
                            {
                                currentImage = new Tuple<int, Image>(
                                    -1,
                                    Image.Load(imageStream, new JpegDecoder())
                                    );
                            }
                        }
                        else
                        {
                            using (var imageStream = await Repository.OpenDownloadStream(
                                input.ThumbIds[ThumbTable[i - 1].ToString()]
                                ))
                            {
                                currentImage = new Tuple<int, Image>(
                                    i - 1,
                                    Image.Load(imageStream, new JpegDecoder())
                                    );
                            }
                        }
                    }

                    MutateImage(currentImage.Item2, size);
                    currentImage = new Tuple<int, Image>(i, currentImage.Item2);

                    input.ThumbIds[size.ToString()] =
                            await SaveFile("updated", Repository, currentImage.Item2, size);
                    updated++;
                }
            }
            return new UpdateResult
            {
                Result = input,
                UpdatedCount = updated
            };
        }
    }
}