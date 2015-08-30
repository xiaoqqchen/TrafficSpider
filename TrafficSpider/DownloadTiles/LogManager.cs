using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DownloadTiles
{
    public class LogManager
    {
        private static string logPath = string.Empty;
        ///   <summary> 
        ///  保存日志的文件夹
        ///   </summary> 
        public static string LogPath
        {
            get
            {
                if (logPath == string.Empty)
                {
                    logPath = AppDomain.CurrentDomain.BaseDirectory;
                }
                return logPath;
            }
            set
            {
                logPath = value;
                if (System.IO.Directory.Exists(logPath) == false)//如果不存在就创建file文件夹  
                {
                    System.IO.Directory.CreateDirectory(logPath);
                }
            }
        }

        private static string logFielPrefix = string.Empty;
        ///   <summary> 
        ///  日志文件前缀
        ///   </summary> 
        public static string LogFielPrefix
        {
            get { return logFielPrefix; }
            set { logFielPrefix = value; }
        }

        ///   <summary> 
        ///  写日志
        ///   </summary> 
        public static void WriteLog(string logFile, string msg)
        {
            try
            {
                System.IO.StreamWriter sw = System.IO.File.AppendText(
                    LogPath + LogFielPrefix + logFile + "_" +
                    DateTime.Now.ToString("yyyyMMdd") + ".Log "
                    );
                sw.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss:  ") + msg);
                sw.Close();
            }
            catch
            { }
        }

        ///   <summary> 
        ///  写日志
        ///   </summary> 
        public static void WriteLog(LogFile logFile, string msg)
        {
            WriteLog(logFile.ToString(), msg);
        }
    }

    ///   <summary> 
    ///  日志类型
    ///   </summary> 
    public enum LogFile
    {
        Trace,
        Warning,
        Error,
        SQL
    } 
}
