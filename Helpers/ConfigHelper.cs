using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        public static T OpenConfig(string productName) =>
            JsonConvert.DeserializeObject<T>(File.OpenText(Path.Combine(GetConfigFolder(productName), "config.json"))
                .ReadToEnd());

        public static void SaveConfig(string productName, T config) => File.WriteAllText(
            Path.Combine(GetConfigFolder(productName), "config.json"), JsonConvert.SerializeObject(config));
    }
}
