using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Ruminoid.Common.Helpers
{
    public static class ConfigHelper<T>
    {
        private static string GetConfigFolder(string productName)
        {
            string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Ruminoid\Config", productName);
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static T OpenConfig()
        {
            RuminoidProductAttribute product =
                (RuminoidProductAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(RuminoidProductAttribute));
            if (product == null) throw new CustomAttributeFormatException("Product name not found.");
            return JsonConvert.DeserializeObject<T>(File
                .OpenText(Path.Combine(GetConfigFolder(product.ProductName), "config.json"))
                .ReadToEnd());
        }

        public static void SaveConfig(T config)
        {
            RuminoidProductAttribute product =
                (RuminoidProductAttribute) Attribute.GetCustomAttribute(typeof(T), typeof(RuminoidProductAttribute));
            if (product == null) throw new CustomAttributeFormatException("Product name not found.");
            File.WriteAllText(
                Path.Combine(GetConfigFolder(product.ProductName), "config.json"), JsonConvert.SerializeObject(config));
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    sealed class RuminoidProductAttribute : Attribute
    {
        public RuminoidProductAttribute(string productName)
        {
            _productName = productName;
        }

        private string _productName;

        public string ProductName
        {
            get => _productName;
            set => _productName = value;
        }
    }
}
