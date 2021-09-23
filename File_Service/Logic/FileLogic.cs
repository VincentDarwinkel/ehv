﻿using File_Service.CustomExceptions;
using File_Service.Dal.Interfaces;
using File_Service.Enums;
using File_Service.Models;
using File_Service.Models.HelperFiles;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace File_Service.Logic
{
    public class FileLogic
    {
        private readonly FileHelper _fileHelper;
        private readonly DirectoryLogic _directoryLogic;
        private readonly IFileDal _fileDal;

        public FileLogic(FileHelper fileHelper, DirectoryLogic directoryLogic, IFileDal fileDal)
        {
            _fileHelper = fileHelper;
            _directoryLogic = directoryLogic;
            _fileDal = fileDal;
        }

        /// <summary>
        /// Saves the file on the file system if the provided userSpecifiedPath is valid, the file is an webp image or mp4 video and the file does not contain viruses
        /// </summary>
        /// <param name="files">The files to save</param>
        /// <param name="userSpecifiedPath">The userSpecifiedPath to save the files in</param>
        /// <param name="requestingUserUuid">The uuid of the requesting user</param>
        /// <returns>A list of the name of the files that are saved</returns>
        public async Task SaveFile(List<IFormFile> files, string userSpecifiedPath, Guid requestingUserUuid)
        {
            List<IFormFile> validFiles = await _fileHelper.FilterFiles(files);
            if (validFiles.Count == 0)
            {
                throw new UnprocessableException();
            }

            var fullPath = $"{Environment.CurrentDirectory}{userSpecifiedPath}";
            if (!Directory.Exists(fullPath))
            {
                await _directoryLogic.CreateDirectory(userSpecifiedPath, requestingUserUuid);
            }

            string[] supportedImageFileTypes = { ".webp", ".png", ".jpeg", ".jpg" };
            List<IFormFile> imageCollection = validFiles
                .FindAll(file => supportedImageFileTypes
                .Any(sift => file.FileName
                    .EndsWith(sift)));

            string[] supportedVideoFileTypes = { ".webm", ".mp4", ".mov", ".avi" };
            List<IFormFile> videoCollection = validFiles
                .FindAll(file => supportedVideoFileTypes
                    .Any(sift => file.FileName
                        .EndsWith(sift)));

            DirectoryDto parentDirectory = await _directoryLogic.Find(userSpecifiedPath);
            if (parentDirectory == null)
            {
                throw new NoNullAllowedException("parentDirectory was empty, no directory found with this paths in the database");
            }

            var filesToAdd = new List<FileDto>();
            foreach (var video in videoCollection)
            {
                var fileUuid = Guid.NewGuid();
                bool success = await CompressAndSaveVideo(video, fullPath, fileUuid);
                if (success)
                {
                    filesToAdd.Add(new FileDto
                    {
                        Uuid = fileUuid,
                        FullPath = $"{fullPath}/{fileUuid}.mp4",
                        FileType = FileType.Video,
                        OwnerUuid = requestingUserUuid,
                        ParentDirectoryUuid = parentDirectory.Uuid
                    });
                }
            }

            foreach (var image in imageCollection)
            {
                var fileUuid = Guid.NewGuid();
                bool success = await CompressAndSaveImage(image, fullPath, fileUuid);
                if (success)
                {
                    filesToAdd.Add(new FileDto
                    {
                        Uuid = fileUuid,
                        FullPath = $"{fullPath}/{fileUuid}.webp",
                        FileType = FileType.Image,
                        OwnerUuid = requestingUserUuid,
                        ParentDirectoryUuid = parentDirectory.Uuid
                    });
                }
            }

            await _fileDal.Add(filesToAdd);
        }

        public async Task<FileContentResult> Find(Guid uuid)
        {
            FileDto file = await _fileDal.Find(uuid);
            if (file == null)
            {
                return null;
            }

            byte[] fileBytes = await File.ReadAllBytesAsync(file.FullPath);
            return new FileContentResult(fileBytes, file.FileType == FileType.Image ? "image/webp" : "video/mp4");
        }

        /// <summary>
        /// Compresses the image and saves the compressed image in the specified path
        /// </summary>
        /// <param name="image">The image to compress</param>
        /// <param name="path">The path to save the image to</param>
        /// <returns>The path of the compressed image</returns>
        private static async Task<bool> CompressAndSaveImage(IFormFile image, string path, Guid fileUuid)
        {
            string fileExtension = image.ContentType.Replace("image/", ".");
            var newFileName = fileUuid.ToString();
            var tempPath = $"{Environment.CurrentDirectory}/Media/TempFiles/{Guid.NewGuid()}/";

            try
            {
                Directory.CreateDirectory(tempPath);
                File.Copy($"{Environment.CurrentDirectory}/Media/TempFiles/ImageConverter.py", $"{tempPath}ImageConverter.py");
                await using (Stream fileStream = new FileStream($"{tempPath}input{fileExtension}", FileMode.Create))
                {
                    await image.CopyToAsync(fileStream);
                }

                SystemHelper.ExecuteOsCommand($"python3 {tempPath}ImageConverter.py");
                File.Move($"{tempPath}output.webp", $"{path}/{newFileName}.webp");
                DirectoryHelper.DeleteDirectory(tempPath);
                return true;
            }
            catch (Exception)
            {
                DirectoryHelper.DeleteDirectory(tempPath);
                File.Delete($"{path}/{newFileName}.webp");
                return false;
            }
        }

        /// <summary>
        /// Compresses the video and saves the compressed video in the specified path
        /// </summary>
        /// <param name="video">The video to compress</param>
        /// <param name="path">The path to save the video to</param>
        /// <returns>The path of the compressed video</returns>
        private static async Task<bool> CompressAndSaveVideo(IFormFile video, string path, Guid fileUuid)
        {
            string fileExtension = video.ContentType.Replace("video/", ".");
            var tempFileName = Guid.NewGuid().ToString();
            var newFileName = fileUuid.ToString();
            var tempPath = $"{Environment.CurrentDirectory}/Media/TempFiles/";

            try
            {
                await using (Stream fileStream = new FileStream(tempPath + tempFileName + fileExtension, FileMode.Create))
                {
                    await video.CopyToAsync(fileStream);
                }

                SystemHelper.ExecuteOsCommand($"ffmpeg -i {tempPath + tempFileName + fileExtension} -b:a 300k -vcodec libx264 -crf 26 -filter:v fps=24 {path}/{newFileName}.mp4");
                File.Delete(tempPath + tempFileName + fileExtension);
                return true;
            }
            catch (Exception)
            {
                File.Delete(tempPath + tempFileName + fileExtension);
                File.Delete($"{path}/{newFileName}.mp4");
                return false;
            }
        }

        /// <summary>
        /// Removes a file by uuid if the user is owner and the file exists
        /// </summary>
        /// <param name="uuid">The uuid of the file to remove</param>
        /// <param name="requestingUser">The user that made the request</param>
        public async Task Delete(Guid uuid, UserHelper requestingUser)
        {
            if (uuid == Guid.Empty)
            {
                throw new UnprocessableException();
            }

            FileDto fileToDelete = await _fileDal.Find(uuid);
            File.Delete(fileToDelete.FullPath);
            await _fileDal.Delete(fileToDelete);
        }
    }
}