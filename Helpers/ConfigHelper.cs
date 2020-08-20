using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Ruminoid.Common.Helpers;

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

namespace Ruminoid.Dashboard
{
    [RuminoidProduct("Dashboard")]
    [JsonObject(MemberSerialization.OptIn)]
    public class Config : INotifyPropertyChanged
    {
        #region Current

        public static Config Current { get; set; } = ConfigHelper<Config>.OpenConfig();

        #endregion

        #region Recent Products

        [JsonProperty]
        private Dictionary<string, int> recentProducts = new Dictionary<string, int>();

        public Dictionary<string, int> RecentProducts
        {
            get => recentProducts;
            set
            {
                recentProducts = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region Update

        [JsonProperty] private string updateServer = "https://update.ruminoid.world/";

        public string UpdateServer
        {
            get => updateServer;
            set
            {
                updateServer = value;
                OnPropertyChanged();
            }
        }

        [JsonProperty] private string updateChannel = "stable";

        public string UpdateChannel
        {
            get => updateChannel;
            set
            {
                updateChannel = value;
                OnPropertyChanged();
            }
        }

        #endregion

        #region PropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
