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
    public static class ConfigHelper<T> where T : new()
    {
        public static string GetConfigFolder()
        {
            string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            @"Ruminoid\Config", GetProductName());
            Directory.CreateDirectory(folder);
            return folder;
        }

        public static string GetProductName()
        {
            RuminoidProductAttribute product =
                (RuminoidProductAttribute)Attribute.GetCustomAttribute(typeof(T), typeof(RuminoidProductAttribute));
            if (product == null) throw new CustomAttributeFormatException("Product name not found.");
            return product.ProductName;
        }

        private static string GetConfigFileName() => Path.Combine(GetConfigFolder(), "config.json");

        public static T OpenConfig()
        {
            if (!File.Exists(GetConfigFileName())) return new T();
            return JsonConvert.DeserializeObject<T>(File.ReadAllText(GetConfigFileName()));
        }

        public static void SaveConfig(T config)
        {
            File.WriteAllText(
                GetConfigFileName(), JsonConvert.SerializeObject(config));
        }
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class RuminoidProductAttribute : Attribute
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
