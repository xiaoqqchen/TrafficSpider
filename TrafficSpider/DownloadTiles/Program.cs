using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ESRI.ArcGIS;

namespace DownloadTiles
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static int Main(string[] args)
        {
            //CreatePoint();
            try
            {
                RuntimeManager.BindLicense(ProductCode.EngineOrDesktop);
                DownloadTask downloadTask = new DownloadTask();
                DateTime time = Convert.ToDateTime(args[0]);
                string _extent = args[1];
                string basePath = args[2];
                string tileDir = args[3];
                string csv = args[4];
                downloadTask.DoMainTask(time,_extent,basePath,tileDir,csv);
            }
            catch (Exception ex)
            {
                LogManager.LogPath = AppDomain.CurrentDomain.BaseDirectory + "\\log\\";
                LogManager.WriteLog("error",ex.Message);
                return -1;
            }
            LogManager.LogPath = AppDomain.CurrentDomain.BaseDirectory + "\\log\\";
            LogManager.WriteLog("state","执行任务成功！");
            return 2;
        }

        public static void CreatePoint()
        {
            List<int[]> point = new List<int[]>();
            StreamReader sr3 = new StreamReader(@"D:\我的文件\项目\Data\TrafficLevel9\广州\temp\band3.txt");
            StreamReader sr4 = new StreamReader(@"D:\我的文件\项目\Data\TrafficLevel9\广州\temp\band4.txt");
            for (int i = 0; i < 3584; i++)
            {
                string[] line3 = sr3.ReadLine().Split(new char[] { ' ' });
                string[] line4 = sr4.ReadLine().Split(new char[] { ' ' });
                for (int j = 0; j < 3584; j++)
                {
                    if (line4[j] != "0" && line3[j] == "0")
                    {
                        point.Add(new[] { j, i });
                        j = j + 5;
                    }

                }
                for (int k = 0; k < 2; k++)
                {
                    sr3.ReadLine();
                    sr4.ReadLine();
                    i++;
                }

            }
            StreamWriter sr = new StreamWriter(@"D:\我的文件\项目\Data\TrafficLevel9\广州\temp\point.txt");
            foreach (int[] list in point)
            {
                sr.WriteLine("{0},{1}", list[0], list[1]);
            }
            sr.Dispose();
        }
    }



}
