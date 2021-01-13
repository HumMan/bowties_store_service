using System;
using System.Collections.Generic;
using System.Linq;
using MongoDB.Bson;
using Omu.ValueInjecter;
using store_service.App.Model;

namespace store_service.App.Model.Converters {

    public static class UserExtension {
        public static Data.User ToData (this Web.User source) {
            var result = new Data.User ();
            result.InjectFrom (source);
            if (source.Id != null)
                result.Id = Converter.FromWeb (source.Id);
            return result;
        }
    }
    public static class CartItemExtension {
        public static Data.CartItem ToData (this Web.CartItem source) {
            var result = new Data.CartItem ();
            result.InjectFrom (source);
            if (source.ProductId != null)
                result.ProductId = Converter.FromWeb (source.ProductId);
            if (source.VariationId != null)
                result.VariationId = Converter.FromWeb (source.VariationId);
            return result;
        }
    }
    public static class CartExtension {
        public static Data.Cart ToData (this Web.Cart source) {
            var result = new Data.Cart ();
            result.InjectFrom (source);
            if (source.Id != null)
                result.UserId = Converter.FromWeb (source.Id);
            result.LastUpdate = Converter.FromWeb (source.Timestamp);
            result.Items = source.Items.Where (i => i.Count > 0).Select (i => i.ToData ()).ToList ();
            return result;
        }
    }
    public static class ProductExtension {
        public static Data.Product ToData (this Web.Product source) {
            var result = new Data.Product ();
            result.InjectFrom (source);
            result.Created = Converter.FromWeb(source.Created);
            if(source.LastChange.HasValue)
                result.LastChange = Converter.FromWeb(source.LastChange.Value);
            if (source.Id != null)
                result.Id = Converter.FromWeb (source.Id);
            result.GroupId = Converter.FromWeb (source.GroupId);
            if (source.Images != null)
                result.Images = source.Images.Select (x => x.ToData ()).ToList ();
            if (source.Properties != null)
                result.Properties.InjectFrom (source.Properties);
            result.Variations = source.Variations.Select (x => x.ToData ()).ToList ();
            return result;
        }
    }
    public static class ProductVariationExtension {
        internal static Data.ProductVariation ToData (this Web.ProductVariation source) {
            var result = new Data.ProductVariation ();
            result.InjectFrom (source);
            result.Id = Converter.FromWeb (source.Id);
            if(source.VariationIds!=null)
                result.VariationIds = source.VariationIds.Select (x => x.ToData ()).ToList ();
            return result;
        }
    }
    public static class VariationIdExtension {
        public static Data.VariationId ToData (this Web.VariationId source) {
            var result = new Data.VariationId ();
            result.InjectFrom (source);
            result.ParameterId = Converter.FromWeb (source.ParameterId);
            result.ParameterValueId = Converter.FromWeb (source.ParameterValueId);
            return result;
        }
    }
    public static class VariationParameterValueExtension {
        public static Data.VariationParameterValue ToData (this Web.VariationParameterValue source) {
            var result = new Data.VariationParameterValue ();
            result.InjectFrom (source);
            if(source.Id!=null)
                result.Id = Converter.FromWeb(source.Id);
            return result;
        }
    }
    public static class VariationParameterExtension {
        public static Data.VariationParameter ToData (this Web.VariationParameter source) {
            var result = new Data.VariationParameter ();
            if(source.Id!=null)
                result.Id = Converter.FromWeb(source.Id);
            result.InjectFrom (source);
            result.Values = source.Values.Select (x => x.ToData ()).ToList ();
            return result;
        }
    }
    public static class GroupExtension {
        public static Data.Group ToData (this Web.Group source) {
            var result = new Data.Group ();
            result.InjectFrom (source);
            if (source.Id != null)
                result.Id = Converter.FromWeb (source.Id);
            if (source.Image != null)
                result.Image = source.Image.ToData();
            result.VariationParameters = source.VariationParameters.Select (x => x.ToData ()).ToList ();
            return result;
        }
    }
    public static class ImageDescExtension {
        public static Data.ImageDesc ToData (this Web.ImageDesc source) {
            var result = new Data.ImageDesc ();
            result.Id = Converter.FromWeb (source.Id);
            result.ThumbIds = source.ThumbIds.ToDictionary (x => x.Key,
                x => Converter.FromWeb (x.Value));
            return result;
        }
    }
    public static class Converter {
        public static string ToWeb (ObjectId from) {
            return from.ToString ();
        }
        public static long ToWeb (BsonDateTime from) {
            return from.ToUniversalTime ().ToFileTimeUtc ();
        }
        public static ObjectId FromWeb (string from) {
            return ObjectId.Parse (from);
        }
        public static BsonDateTime FromWeb (long from) {
            return new BsonDateTime (DateTime.FromFileTimeUtc (from));
        }
    }
}