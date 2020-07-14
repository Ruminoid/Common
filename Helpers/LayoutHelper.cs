using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Xml.Linq;
using YDock;

namespace Ruminoid.Common.Helpers
{
    public static class LayoutHelper<T>
    {
        private static string GetLayoutFolder()
        {
            string folder = Path.Combine(ConfigHelper<T>.GetConfigFolder(), "Layout");
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string GetWindowName(Control window) => window.GetType().Name;

        private static string GetLayoutFileName(Control window) =>
            Path.Combine(GetLayoutFolder(), $"{GetWindowName(window)}.xml");

        public static void SaveLayout(DockManager dockManager, Control window)
        {
            dockManager.SaveCurrentLayout(GetWindowName(window));
            var doc = new XDocument();
            var rootNode = new XElement("Layouts");
            foreach (var layout in dockManager.Layouts.Values)
                layout.Save(rootNode);
            doc.Add(rootNode);
            doc.Save(GetLayoutFileName(window));
        }

        public static void SaveLayoutAndDispose(DockManager dockManager, Control window)
        {
            SaveLayout(dockManager, window);
            dockManager.Dispose();
        }

        public static bool ApplyLayout(DockManager dockManager, Control window)
        {
            if (!File.Exists(GetLayoutFileName(window))) return false;
            XDocument layout = XDocument.Parse(File.ReadAllText(GetLayoutFileName(window)));
            if (layout.Root != null)
                foreach (XElement item in layout.Root.Elements())
                {
                    string name = item.Attribute("Name")?.Value;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (dockManager.Layouts.ContainsKey(name))
                        dockManager.Layouts[name].Load(item);
                    else dockManager.Layouts[name] = new YDock.LayoutSetting.LayoutSetting(name, item);
                }
            dockManager.ApplyLayout(GetWindowName(window));
            return true;
        }
    }
}
