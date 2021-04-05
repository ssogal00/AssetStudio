using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AssetStudio;
using AssetStudioGUI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Object = AssetStudio.Object;

namespace TextureExporter
{
    public class Program
    {
        static bool IsPOT(int width, int height)
        {
            int[] pots = { 4, 16, 32, 64, 128, 256, 512, 1024, 2048 };
            foreach (var denominator in pots)
            {
                if (denominator <= width)
                {
                    if (width % denominator != 0)
                    {
                        return false;
                    }
                }

                if(denominator <= height)
                {
                    if (height % denominator != 0)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        static bool IsCompressed(TextureFormat format)
        {
            var formatString= format.ToString();
            if (formatString.StartsWith("ETC") ||
                formatString.StartsWith("ASTC") ||
                formatString.StartsWith("PVRTC") ||
                formatString.StartsWith("DXT"))
            {
                return true;
            }

            return false;
        }

        static int Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage : TextureExporter.exe androidAssetPath textureoutputpath textureSummaryPath");
                return -1;
            }

            if (args.Length < 1 || !Directory.Exists(args[0].Trim()))
            {
                Console.WriteLine("{0} does not exit..", args[0]);
                return -1;
            }

            if (!Directory.Exists(args[1].Trim()))
            {
                Directory.CreateDirectory(args[1]);
            }

            string exportDir = args[1].Trim();

            var assetManager = new AssetsManager();
            try
            {
                assetManager.LoadFolder(args[0]);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            var objectAssetPaths = new Dictionary<Object, HashSet<string>>();
            var containers = new List<(PPtr<Object>, string)>();

            foreach (var asset in assetManager.assetsFileList)
            {
                foreach (var obj in asset.Objects)
                {
                    objectAssetPaths.Add(obj, new HashSet<string>());

                    switch (obj)
                    {
                        case AssetBundle bundle:
                            foreach (var m_Container in bundle.m_Container)
                            {
                                var preloadIndex = m_Container.Value.preloadIndex;
                                var preloadSize = m_Container.Value.preloadSize;
                                var preloadEnd = preloadIndex + preloadSize;
                                for (int k = preloadIndex; k < preloadEnd; k++)
                                {
                                    containers.Add((bundle.m_PreloadTable[k], m_Container.Key));
                                }
                            }
                            break;

                        case ResourceManager resourceManager:
                            foreach (var m_Container in resourceManager.m_Container)
                            {
                                containers.Add((m_Container.Value, m_Container.Key));
                            }

                            break;
                    }
                }
            }

            foreach ((var pptr, var container) in containers)
            {
                if (pptr.TryGet(out var obj))
                {
                    objectAssetPaths[obj].Add(container);
                }
            }


            List<JObject> textures = new List<JObject>();
            List<JObject> npotTextures = new List<JObject>();
            List<JObject> uncompressedTextures = new List<JObject>();
            
            foreach (var asset in assetManager.assetsFileList)
            {
                foreach (var obj in asset.Objects)
                {
                    switch (obj)
                    {
                        case Texture2D texture:
                            List<string> paths = objectAssetPaths[obj].ToList();
                            var possiblePath = paths.Where(x => x.Contains($"{texture.m_Name}.jpg") ||
                                                                x.Contains($"{texture.m_Name}.tga") ||
                                                                x.Contains($"{texture.m_Name}.png") ||
                                                                x.Contains($"{texture.m_Name}.psd") ||
                                                                x.Contains($"{texture.m_Name}.bmp")).FirstOrDefault();

                            if (possiblePath == null || possiblePath == string.Empty)
                            {
                                possiblePath = paths.Where(x => x.Contains($"{texture.m_Name}")).FirstOrDefault();
                                if (possiblePath == null)
                                {
                                    possiblePath = $"MaybeAtlas/{texture.m_Name}";
                                }
                            }

                            ClassIDType id = (ClassIDType) obj.serializedType.classID;
                            var jobject = new JObject(
                                new JProperty("name", texture.m_Name),
                                new JProperty("path", paths),
                                new JProperty("possiblePath", possiblePath),
                                new JProperty("classId", id.ToString()),
                                new JProperty("size", $"{texture.m_Width} x {texture.m_Height}"),
                                new JProperty("format", texture.m_TextureFormat.ToString()),
                                new JProperty("mipsCount", texture.m_MipCount),
                                new JProperty("mipmap", texture.m_MipMap ? "true" : "false"),
                                new JProperty("npot", IsPOT(texture.m_Width, texture.m_Height) ? "false" : "true"),
                                new JProperty("byteSize", (texture.m_StreamData != null)
                                    ? obj.byteSize + texture.m_StreamData.size
                                    : obj.byteSize));

                            if (!IsPOT(texture.m_Width, texture.m_Height))
                            {
                                npotTextures.Add(jobject);
                            }

                            if (!IsCompressed(texture.m_TextureFormat))
                            {
                                uncompressedTextures.Add(jobject);
                            }

                            textures.Add(jobject);
                            Exporter.ExportTexture2D(texture, Path.Combine(exportDir, Path.GetDirectoryName(possiblePath)));
                            Console.WriteLine("Exporting {0}", Path.Combine(exportDir,possiblePath));
                            break;
                    }
                }
            }

            textures = textures.OrderByDescending(x => x["byteSize"]).ToList();
            uncompressedTextures = uncompressedTextures.OrderByDescending(x => x["byteSize"]).ToList();
            npotTextures = npotTextures.OrderByDescending(x => x["byteSize"]).ToList();

            var assetObject = new JObject(
                new JProperty("textures", new JArray(textures)),
                new JProperty("uncompressed" , new JArray(uncompressedTextures)),
                new JProperty("npot" , new JArray(npotTextures))
            );

            try
            {
                using (var file = File.CreateText(args[2].Trim()))
                using (var writer = new JsonTextWriter(file)
                {
                    Indentation = 2,
                    Formatting = Formatting.Indented,
                    AutoCompleteOnClose = true
                })
                {
                    assetObject.WriteTo(writer);
                    Console.WriteLine("Success");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return -1;
            }

            return 0;
        }
    }
}
