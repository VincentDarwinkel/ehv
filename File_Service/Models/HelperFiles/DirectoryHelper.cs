﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace File_Service.Models.HelperFiles
{
    public static class DirectoryHelper
    {
        /// <summary>
        /// Returns the name of all folders in a fullPath
        /// </summary>
        /// <returns>An IEnumerable with the names of all folders in the fullPath</returns>
        public static IEnumerable<string> GetFoldersInDirectory(string fullPath)
        {
            if (!Directory.Exists(fullPath))
            {
                throw new DirectoryNotFoundException();
            }

            var directoryInfo = new DirectoryInfo(fullPath);
            return directoryInfo.GetDirectories()
                .Select(di => di.Name);
        }

        private static List<string> FilterFiles(List<string> files)
        {
            string[] allowedFileTypes = { ".webp", ".mp4", ".png", ".jpg", ".jpeg" };
            return files.FindAll(file => allowedFileTypes
                .Any(file.EndsWith));
        }

        /// <summary>
        /// Find all the files in a fullPath
        /// </summary>
        /// <param name="fullPath">The fullPath to search the files in</param>
        /// <returns></returns>
        public static List<string> GetFilesInDirectory(string fullPath)
        {
            var directoryInfo = new DirectoryInfo(fullPath);
            List<string> foundFiles = directoryInfo.GetFiles().Select(file => file.Name)
                .ToList();

            return FilterFiles(foundFiles);
        }
    }
}