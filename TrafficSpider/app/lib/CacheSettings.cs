﻿using System;
using System.Configuration;
using System.IO;

namespace BrutileArcGIS.Lib
{
    public static class CacheSettings
    {
        public static string GetServicesConfigDir()
        {
            var config = ConfigurationHelper.GetConfig();
            var servicesConfigDir = config.AppSettings.Settings["servicesConfigDir"].Value;
            if (servicesConfigDir.Contains("%"))
            {
                servicesConfigDir = ReplaceEnvironmentVar(servicesConfigDir);
            }

            if (!Directory.Exists(servicesConfigDir))
            {
                Directory.CreateDirectory(servicesConfigDir);
            }

            return servicesConfigDir;
        }

        public static string GetCacheFolder()
        {
            var config = ConfigurationHelper.GetConfig();
            var tileDir = @"D:\我的文件\天津师大切片解决方案\Tiles\GZ";
            if (tileDir.Contains("%"))
            {
                tileDir = ReplaceEnvironmentVar(tileDir);
            }

            return tileDir;
        }

        public static string GetCacheFolder(string tileDir)
        {
            var config=ConfigurationHelper.GetConfig();
            //var tileDir = @"D:\我的文件\天津师大切片解决方案\Tiles\GZ";
            if(tileDir.Contains("%"))
            {
                tileDir = ReplaceEnvironmentVar(tileDir);
            }

            return tileDir;
        }

        private static string ReplaceEnvironmentVar(string path)
        {
            var firstIndex = path.IndexOf("%", StringComparison.Ordinal);
            var lastIndex = path.LastIndexOf("%", StringComparison.Ordinal);
            var envVar = path.Substring(firstIndex+1, lastIndex - firstIndex-1);
            var environmentVariable = Environment.GetEnvironmentVariable(envVar);
            path = path.Replace("%"+envVar+"%", environmentVariable);
            return path;
        }
    }
}
