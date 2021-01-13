using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using MongoDB.Bson;
using Model = store_service.App.Model;

namespace store_service.Helpers
{
    class VariationIdsComparer : IEqualityComparer<List<Model.Web.VariationId>>
    {

        public bool Equals(List<Model.Web.VariationId> x, List<Model.Web.VariationId> y)
        {
            if (x.Count == y.Count)
            {
                for (int i = 0; i < x.Count; i++)
                {
                    if (x[i].ParameterId != y[i].ParameterId || x[i].ParameterValueId != y[i].ParameterValueId)
                        return false;
                }
                return true;
            }
            else
            {
                return false;
            }
        }
        public int GetHashCode(List<Model.Web.VariationId> obj)
        {
            int result = 0;
            for (int i = 0; i < obj.Count; i++)
                result += obj[i].ParameterId.GetHashCode() + obj[i].ParameterValueId.GetHashCode();
            return result;
        }
    }

    public class Variation
    {
         public static bool Inc (int[] index, int[] lengths) {
            for (int i = 0; i < index.Length; i++) {
                if (index[i] < lengths[i] - 1) {
                    index[i]++;
                    return true;
                }
                else
                {
                    if(i==index.Length-1)
                    {
                        return false;
                    }else
                    {
                        index[i]=0;
                    }
                }
            }
            return false;
        }
        public static List<Model.Web.VariationId> ToVariationIds (List<Model.Data.VariationParameter> list, int[] index) {
            var result = new List<Model.Web.VariationId> ();
            for (int i = 0; i < index.Length; i++) {
                result.Add (new Model.Web.VariationId {
                    ParameterId = list[i].Id.ToString (),
                        ParameterValueId = list[i].Values[index[i]].Id.ToString ()
                });
            }
            return result;
        }
        public static string ConcatTitles (List<Model.Data.VariationParameter> list, int[] index) {
            var result = new StringBuilder();
            for (int i = 0; i < index.Length; i++) {

                result.Append(list[i].Values[index[i]].Title);
                if(i!=index.Length-1)
                    result.Append("/");
            }
            return result.ToString();
        }
        public static string ConcatTitles (List<Model.Data.VariationParameter> list, List<Model.Web.VariationId> index) {
            var result = new StringBuilder();
            int i=0;
            foreach(var p in list)
            {
                var valId = index.First(x=>ObjectId.Parse(x.ParameterId)==p.Id);
                result.Append(p.Values.First(x=>x.Id==ObjectId.Parse(valId.ParameterValueId)).Title);
                if(i!=list.Count-1)
                    result.Append("/");
                i++;
            }
            return result.ToString();
        }
        public static string ConcatTitles(List<Model.Data.VariationParameter> list, List<Model.Data.VariationId> index)
        {
            var result = new StringBuilder();
            int i = 0;
            foreach (var p in list)
            {
                var valId = index.First(x => x.ParameterId == p.Id);
                var targetValue = p.Values.FirstOrDefault(x => x.Id == valId.ParameterValueId);
                if (targetValue != null)
                {
                    result.Append(targetValue.Title);
                    if (i != list.Count - 1)
                        result.Append("/");
                }
                i++;
            }
            return result.ToString();
        }
        public static List<int[]> GenerateVariations(Model.Data.Group group)
        {            
            var index = new int[group.VariationParameters.Count];
            var lengths = new int[index.Length];
            for (int i = 0; i < lengths.Length; i++) {
                index[i] = 0;
                lengths[i] = group.VariationParameters[i].Values.Count;
            }
            var result = new List<int[]>(lengths.Aggregate((a,b)=>a*b));
            while (true) {
                var temp = new int[index.Length];
                index.CopyTo(temp,0);
                result.Add(temp);

                if (!Inc(index, lengths))
                    break;
            }

            return result;
        }
    }
}