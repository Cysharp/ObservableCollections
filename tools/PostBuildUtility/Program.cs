using ConsoleAppFramework;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PostBuildUtility
{
    class Program : ConsoleAppBase
    {
        static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder().RunConsoleAppFrameworkAsync<Program>(args);
        }

        [Command("replace-to-unity")]
        public void ReplaceToUnity([Option(0)] string directory)
        {
            var replaceSet = new Dictionary<string, string>
            {
                // Remove nullable
                {"#nullable disable", "" },
                {"where T : notnull", "" },
                {"where TKey : notnull", "" },
                {">?", ">" }, // generics <T>?
                {"T?", "T" },
                {"T[]?", "T[]" },
                {"default!", "default" },
                {"null!", "null" },
                // project specified
                {"array!", "array" },
                {"Current!", "Current" },
                {"NotifyCollectionChangedEventHandler?", "NotifyCollectionChangedEventHandler" },
                {"PropertyChangedEventHandler?", "PropertyChangedEventHandler" },
            };

            System.Console.WriteLine("Start to replace code, remove nullability.");
            var noBomUtf8 = new UTF8Encoding(false);

            foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                var original = text;

                foreach (var item in replaceSet)
                {
                    text = text.Replace(item.Key, item.Value);
                }

                if (text != original)
                {
                    File.WriteAllText(path, text, noBomUtf8);
                }
            }

            System.Console.WriteLine("Replace complete.");
        }
    }
}