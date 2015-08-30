using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Web;
using BruTile;
using BruTile.Cache;
using BruTile.Web;
using BrutileArcGIS.Lib;
using BrutileArcGIS.lib;
using ESRI.ArcGIS;
using ESRI.ArcGIS.Geometry;

namespace GetTiles
{
    /// <summary>
    /// GetTiles 的摘要说明
    /// </summary>
    public class GetTiles : IHttpHandler
    {
        private static ITileSource _tileSource;
        private static FileCache _fileCache;
        private string _cacheDir;
        private IConfig _config;
        List<TileInfo> _tiles;
        private const int _tileTimeOut = 1;

        static WebTileProvider _tileProvider;
        private List<double> extent;
        private double level;

        public void ProcessRequest(HttpContext context)
        {
            RuntimeManager.BindLicense(ProductCode.EngineOrDesktop);
            extent = context.Request.QueryString["Extent"].Split(new[] { ',' }).Select(Convert.ToDouble).ToList();
            level = double.Parse(context.Request.QueryString["level"]);
            _config = ConfigHelper.GetConfig(EnumBruTileLayer.OSM);

            _tileSource = _config.CreateTileSource();
            _tileProvider = (WebTileProvider)_tileSource.Provider;


            _cacheDir = CacheSettings.GetCacheFolder();

            _fileCache = CacheDirectory.GetFileCache(_cacheDir, _config, EnumBruTileLayer.OSM);
            Draw();
            //using (Bitmap map = new Bitmap(@"D:\我的文件\天津师大切片解决方案\DownloadTiles\DownloadTiles\bin\Debug\p.png"))
            using (Bitmap map = mosaicImage())
            {
                using (MemoryStream mem = new MemoryStream())
                {
                    map.Save(mem, ImageFormat.Png);
                    mem.Seek(0, SeekOrigin.Begin);

                    context.Response.ContentType = "image/png";

                    mem.CopyTo(context.Response.OutputStream, 4096);
                    context.Response.Flush();
                }
            }
        }
        
        public void Draw()
        {

            _tiles = GetTile();

            if (_tiles.Any())
            {
                DownloadTiles();

                //var downloadFinished = new ManualResetEvent(false);
                //var t = new Thread(DownloadTiles);
                //t.Start(downloadFinished);
                //downloadFinished.WaitOne();
            }
        }

        private void DownloadTiles()
        {
            //var downloadFinished = args as ManualResetEvent;

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
                //var doneEvents = new MultipleThreadResetEvent(downloadTiles.Count);

                foreach (var t in downloadTiles)
                {
                    DownloadTile(t);
                    //object o = new object[] { t, doneEvents };
                    //ThreadPool.SetMaxThreads(25, 25);
                    //ThreadPool.QueueUserWorkItem(DownloadTile, o);
                }

                //doneEvents.WaitAll();
            }
            //if (downloadFinished != null) downloadFinished.Set();
        }

        private static void DownloadTile(TileInfo tileInfo)
        {
            //var parameters = (object[])tile;
            //if (parameters.Length != 2) throw new ArgumentException("Two parameters expected");
            //var tileInfo = (TileInfo)parameters[0];
            //var doneEvent = (MultipleThreadResetEvent)parameters[1];
            
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
            catch (Exception)
            {
            }
           // doneEvent.SetOne();
        }

        public static byte[] GetBitmap(Uri uri)
        {
            byte[] bytes = null;

            try
            {
                bytes = RequestHelper.FetchImage(uri);

            }
            catch (System.Net.WebException)
            {
            }
            return bytes;
        }

        private static void CreateRaster(TileInfo tile, string name)
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

            var mapWidth = 250;
            var mapHeight = 250;
            float resolution = (float) level;


            var centerPoint = env.GetCenterPoint();

            var transform = new Transform(centerPoint, resolution, mapWidth, mapHeight);
            var level1 = Utilities.GetNearestLevel(schema.Resolutions, transform.Resolution);

            var tiles = schema.GetTilesInView(transform.Extent, level1);

            return tiles.ToList();
        }

        public Bitmap mosaicImage()
        {
            int minX = _tiles.Min(p => p.Index.Col);
            int minY = _tiles.Min(p => p.Index.Row);
            int maxX = _tiles.Max(p => p.Index.Col);
            int maxY = _tiles.Max(p => p.Index.Row);
            string level = _tiles.Min(p => p.Index.Level);

            Bitmap resultImg = new Bitmap(256 * (maxX - minX + 1), 256 * (maxY - minY + 1));
            Graphics resultGraphics = Graphics.FromImage(resultImg);
            foreach (TileInfo tileInfo in _tiles)
            {
                string name = _fileCache.GetFileName(tileInfo.Index);
                if (!File.Exists(name)) continue;
                resultGraphics.DrawImage(Image.FromFile(name), (tileInfo.Index.Col - minX) * 256, (tileInfo.Index.Row - minY) * 256);
            }

            resultGraphics.Dispose();

            //string minName = _fileCache.GetFileName(new TileIndex(minX, minY, level));
            //string pgwName = minName.Replace(".png", ".pgw");

            //File.Copy(pgwName, "test.pgw", true);
            return resultImg;
            //return Cut(resultImg, 0, 0, resultImg.Width, resultImg.Height);

            //resultImg.Save("test.png", ImageFormat.Png);
        }

        public static Bitmap Cut(Bitmap b, int StartX, int StartY, int iWidth, int iHeight)
        {
            if (b == null)
            {
                return null;
            }
            int w = b.Width;
            int h = b.Height;
            if (StartX >= w || StartY >= h)
            {
                return null;
            }
            if (StartX + iWidth > w)
            {
                iWidth = w - StartX;
            }
            if (StartY + iHeight > h)
            {
                iHeight = h - StartY;
            }
            try
            {
                Bitmap bmpOut = new Bitmap(iWidth, iHeight);
                Graphics g = Graphics.FromImage(bmpOut);
                g.DrawImage(b, new Rectangle(0, 0, iWidth, iHeight), new Rectangle(StartX, StartY, iWidth, iHeight), GraphicsUnit.Pixel);
                g.Dispose();
                return bmpOut;
            }
            catch
            {
                return null;
            }
        } 
        
        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}