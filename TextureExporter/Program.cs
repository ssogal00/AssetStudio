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
        static void Main(string[] args)
        {
            var assetManager = new AssetsManager();
            try
            {
                assetManager.LoadFolder(@"C:\Users\ssogal\Desktop\Android");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            var containerDic = new Dictionary<AssetStudio.Object, string>();
            var objectCount = assetManager.assetsFileList.Sum(x => x.Objects.Count);
            var objectAssetItemDic = new Dictionary<Object, AssetItem>(objectCount);
            var objectAssetPaths = new Dictionary<Object, HashSet<string>>();
            var containers = new List<(PPtr<Object>, string)>();

            foreach (var asset in assetManager.assetsFileList)
            {
                List<JObject> textures = new List<JObject>();
                
                foreach (var obj in asset.Objects)
                {
                    objectAssetItemDic.Add(obj, new AssetItem(obj));
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
                    objectAssetItemDic[obj].Container = container;
                    objectAssetPaths[obj].Add(container);
                }
            }

            Dictionary<string, JArray> atlasMap = new Dictionary<string, JArray>();

            foreach (var asset in assetManager.assetsFileList)
            {
                foreach(var obj in asset.Objects)
                {
                    switch (obj)
                    {
                        case SpriteAtlas atlas:
                            if (!atlasMap.ContainsKey(atlas.m_Name))
                            {
                                atlasMap.Add(atlas.m_Name, new JArray());

                                foreach (var sprite in atlas.m_PackedSprites)
                                {
                                    if (sprite.TryGet(out var result))
                                    {
                                        if (result.m_RD.texture.TryGet(out var texture))
                                        {
                                            atlasMap[atlas.m_Name].Add(texture.m_Name);
                                        }
                                    }
                                }
                            }
                            break;
                    }
                }
            }

            foreach (var asset in assetManager.assetsFileList)
            {
                List<JObject> textures = new List<JObject>();

                

                foreach (var obj in asset.Objects)
                {
                    switch (obj)
                    {
                        case Texture2D texture:
                            List<string> paths = objectAssetPaths[obj].ToList();
                            
                            ClassIDType id = (ClassIDType) obj.serializedType.classID;
                            var jobject = new JObject(
                                new JProperty("name", texture.m_Name),
                                new JProperty("path", paths),
                                new JProperty("classId", id.ToString()),
                                new JProperty("width", texture.m_Width),
                                new JProperty("height", texture.m_Height),
                                new JProperty("format", texture.m_TextureFormat.ToString()),
                                new JProperty("mipsCount", texture.m_MipCount),
                                new JProperty("mipmap", texture.m_MipMap),
                                new JProperty("streamPath", (texture.m_StreamData != null) ? texture.m_StreamData.path : ""),
                                new JProperty("size", (texture.m_StreamData != null) 
                                    ? obj.byteSize +  texture.m_StreamData.size : obj.byteSize));
                            textures.Add(jobject);
                            break;

                        
                        case Sprite sprite:
                            
                            break;
                    }
                }
                
                var assetObject = new JObject(
                    new JProperty("assetFileName", asset.fileName),
                    new JProperty("textures", new JArray(textures))
                );

                try
                {
                    using (var file = File.CreateText($"{asset.fileName}.json"))
                    using (var writer = new JsonTextWriter(file)
                    {
                        Indentation = 2,
                        Formatting = Formatting.Indented,
                        AutoCompleteOnClose = true
                    })
                    {
                        assetObject.WriteTo(writer);
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }
        }
    }
}
