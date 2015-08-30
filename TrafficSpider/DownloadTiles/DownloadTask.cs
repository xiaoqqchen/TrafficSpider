using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using BruTile;
using BruTile.Cache;
using BruTile.Web;
using BrutileArcGIS.Lib;
using BrutileArcGIS.lib;
using ESRI.ArcGIS.Geometry;

namespace DownloadTiles
{
    class DownloadTask
    {
        private static ITileSource _tileSource;
        private static FileCache _fileCache;
        private string _cacheDir;
        private IConfig _config;
        List<TileInfo> _tiles;
        private const int _tileTimeOut = 1;
        static WebTileProvider _tileProvider;
        private ITileSchema _schema;
        private List<double> extent;

        private double level = 9.554628536;
        //beijing
        //private string _extent = "116.23,39.8,116.547,40.047";
        //tianjin
        //private string _extent = "117.050444,39.007160,117.367747,39.277034";
        //guangzhou
        //private string _extent = "113.073901,22.799396,113.540634,23.323048";
        private int num = 13;
        private string basePath = @"D:\我的文件\天津师大切片解决方案\Tiles\";
        //private string tileDir = @"D:\我的文件\天津师大切片解决方案\Tiles\GZ";
        private string csv = @"points.csv";


        public void DoMainTask(DateTime time,string _extent,string basePath,string tileDir,string csv)
        {
            this.basePath = basePath;
            this.csv = csv;

            extent = _extent.Split(new[] { ',' }).Select(Convert.ToDouble).ToList();
            _config = ConfigHelper.GetConfig(EnumBruTileLayer.OSM);
            _tileSource = _config.CreateTileSource();
            _tileProvider = (WebTileProvider)_tileSource.Provider;

           
            _cacheDir = CacheSettings.GetCacheFolder(tileDir);

            
            while (true)
            {
                try
                {
                    DeleteDirectory(_cacheDir);
                    break;
                }
                catch (Exception ex)
                {
                    LogManager.LogPath = AppDomain.CurrentDomain.BaseDirectory + "\\log\\";
                    LogManager.WriteLog("error", ex.Message);
                }
            }


            _fileCache = CacheDirectory.GetFileCache(_cacheDir, _config, EnumBruTileLayer.OSM);
            Draw();
            var map = mosaicImage();
            DirectoryInfo directory =
                new DirectoryInfo(basePath + time.ToString("yyyy") + "\\" + time.ToString("yyyyMMdd") + "\\" +
                                  time.ToString("yyyyMMddHHmm"));
            if(!directory.Exists)
                directory.Create();
            map.Save(directory.FullName + "\\traffic.png");
            CreateTrafficCsv(directory);
        }

        private void CreateTrafficCsv(DirectoryInfo directory)
        {
            string[] dics = File.ReadAllLines(basePath + csv);

            Bitmap bitmap = new Bitmap(directory.FullName + "\\traffic.png");
            List<List<string>> traffic = new List<List<string>>();
            foreach (string dic in dics)
            {
                string[] s = dic.Split(new char[] { ',' });
                if (s[0] == "RId")
                    continue;
                int x = (int)double.Parse(s[3]);
                int y = (int)double.Parse(s[4]);
                try
                {

                    Color color1 = bitmap.GetPixel(x, y);
                    int r = color1.R;
                    int g = color1.G;
                    int b = color1.B;

                    int yongdu = 0;
                    if (r > 200 && g > 200)
                        yongdu = 2;
                    else if (g > r)
                        yongdu = 1;
                    else if (r > g)
                        yongdu = 3;

                    traffic.Add(new List<string>() { s[2], yongdu.ToString() });
                }
                catch (Exception)
                {
                    
                    throw;
                }
               
            }

            StreamWriter sr = new StreamWriter(directory.FullName + @"\\traffic.csv");
            sr.WriteLine("RID,Traffic");
            foreach (List<string> list in traffic)
            {
                sr.WriteLine("{0},{1}", list[0], list[1]);
            }
            sr.Dispose();
        }

        void DeleteDirectory(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            if (dir.Exists)
            {
                DirectoryInfo[] childs = dir.GetDirectories();
                foreach (DirectoryInfo child in childs)
                {
                    child.Delete(true);
                }
                dir.Delete(true);
            }
        }

        public void Draw()
        {
            _tiles = GetTile();

            if (_tiles.Any())
            {
                var downloadFinished = new ManualResetEvent(false);
                var t = new Thread(DownloadTiles);
                t.Start(downloadFinished);
                downloadFinished.WaitOne();
            }
        }


        private void DownloadTiles(object args)
        {
            var downloadFinished = args as ManualResetEvent;

            // Loop through the tiles, and filter tiles that are already on disk.
            var downloadTiles = new List<TileInfo>();
            for (var i = 0; i < _tiles.Count(); i++)
            {
                if (!_fileCache.Exists(_tiles[i].Index))
                {
                    downloadTiles.Add(_tiles[i]);
                }
                else
                {
                    // Read tiles from disk
                    var name = _fileCache.GetFileName(_tiles[i].Index);

                    // Determine age of tile...
                    var fi = new FileInfo(name);
                    if ((DateTime.Now - fi.LastWriteTime).Days <= _tileTimeOut) continue;
                    File.Delete(name);
                    downloadTiles.Add(_tiles[i]);
                }
            }

            if (downloadTiles.Count > 0)
            {
                int count = 1;
                int allCount = 100;
                while ((count - 1) * allCount < downloadTiles.Count)
                {
                    try
                    {
                        int temp = allCount;
                        if (count*allCount > downloadTiles.Count)
                            temp = downloadTiles.Count - (count - 1)*allCount;
                        var doneEvents = new MultipleThreadResetEvent(temp);
                        ThreadPool.SetMaxThreads(25, 25);
                        for (int i = 0; i < temp; i++)
                        {
                            TileInfo t = downloadTiles[(count - 1)*allCount + i];
                            object o = new object[] {t, doneEvents};
                            ThreadPool.QueueUserWorkItem(DownloadTile, o);
                        }
                        doneEvents.WaitAll();
                        doneEvents.Dispose();
                        //Thread.Sleep(10);
                        count++;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("下载异常:" + ex.Message);
                    }
                }
            }

            if (downloadFinished != null) downloadFinished.Set();
        }

        private void DownloadTile(object tile)
        {
            var parameters = (object[])tile;
            if (parameters.Length != 2) throw new ArgumentException("Two parameters expected");
            var tileInfo = (TileInfo)parameters[0];
            var doneEvent = (MultipleThreadResetEvent)parameters[1];

            var url = _tileProvider.Request.GetUri(tileInfo);
            var bytes = GetBitmap(url);

            try
            {
                if (bytes != null)
                {
                    var name = _fileCache.GetFileName(tileInfo.Index);
                    _fileCache.Add(tileInfo.Index, bytes);
                    CreateRaster(tileInfo, name);

                }
            }
            catch (Exception ex)
            {
                LogManager.LogPath = AppDomain.CurrentDomain.BaseDirectory + "\\log\\";
                LogManager.WriteLog("error", ex.Message);
            }
            doneEvent.SetOne();
        }

        public byte[] GetBitmap(Uri uri)
        {
            byte[] bytes = null;
            while (true)
            {
                try
                {
                    bytes = RequestHelper.FetchImage(uri);
                    break;
                }
                catch (System.Net.WebException ex)
                {
                    LogManager.LogPath = AppDomain.CurrentDomain.BaseDirectory + "\\log\\";
                    LogManager.WriteLog("error", ex.Message);
                }
            }

            return bytes;
        }

        private void CreateRaster(TileInfo tile, string name)
        {
            var schema = _tileSource.Schema;
            var fi = new FileInfo(name);
            var tfwFile = name.Replace(fi.Extension, "." + WorldFileWriter.GetWorldFile(schema.Format));
            WorldFileWriter.WriteWorldFile(tfwFile, tile.Extent, schema);
        }

        private List<TileInfo> GetTile()
        {
            var schema = _tileSource.Schema;
            IEnvelope pEnvelope = new EnvelopeClass();
            ISpatialReferenceFactory pSpatRefFact = new SpatialReferenceEnvironmentClass();
            pEnvelope.SpatialReference = pSpatRefFact.CreateGeographicCoordinateSystem(4326);
            pEnvelope.XMin = extent[0];
            pEnvelope.XMax = extent[2];
            pEnvelope.YMin = extent[1];
            pEnvelope.YMax = extent[3];


            var env = Projector.ProjectEnvelope(pEnvelope, schema.Srs);
            
            var mapWidth = 256 * num;
            var mapHeight = 256 * num;
            float resolution = (float)level;


            var centerPoint = env.GetCenterPoint();

            var transform = new Transform(centerPoint, resolution, mapWidth, mapHeight);
            Extent exte = new Extent(pEnvelope.XMin, pEnvelope.YMin, pEnvelope.XMax, pEnvelope.YMax);
            var level1 = Utilities.GetNearestLevel(schema.Resolutions, transform.Resolution);

            var tempExtent = new Extent(12597408.0986328, 2623556.09863281, 12629205.9013672, 2655353.90136719);
            var tiles = schema.GetTilesInView(tempExtent, 10);

            return tiles.ToList();
        }

        public Bitmap mosaicImage()
        {
            int minX = _tiles.Min(p => p.Index.Col);
            int minY = _tiles.Min(p => p.Index.Row);
            int maxX = _tiles.Max(p => p.Index.Col);
            int maxY = _tiles.Max(p => p.Index.Row);

            Bitmap resultImg = new Bitmap(256 * (maxX - minX + 1), 256 * (maxY - minY + 1));
            Graphics resultGraphics = Graphics.FromImage(resultImg);
            foreach (TileInfo tileInfo in _tiles)
            {
                string name = _fileCache.GetFileName(tileInfo.Index);
                if (!File.Exists(name)) continue;
                resultGraphics.DrawImage(Image.FromFile(name), (tileInfo.Index.Col - minX) * 256, (tileInfo.Index.Row - minY) * 256);
            }

            resultGraphics.Dispose();
            return resultImg;
        }
    }
}
