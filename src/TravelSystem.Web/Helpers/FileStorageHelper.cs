using Microsoft.AspNetCore.Http;
using System.IO;

namespace TravelSystem.Web.Helpers
{
    public static class FileStorageHelper
    {
        public static async Task<string?> SaveImageAsync(IFormFile? file, string webRootPath, string folder)
        {
            if (file == null || file.Length == 0)
                return null;

            // Ensure folder exists in wwwroot/images/
            string targetFolder = Path.Combine(webRootPath, "images", folder);
            if (!Directory.Exists(targetFolder))
            {
                Directory.CreateDirectory(targetFolder);
            }

            // Generate unique filename
            string extension = Path.GetExtension(file.FileName).ToLower();
            string fileName = $"{Guid.NewGuid()}{extension}";
            string filePath = Path.Combine(targetFolder, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Return relative path for DB
            return $"/images/{folder}/{fileName}";
        }

        public static void DeleteImage(string? relativePath, string webRootPath)
        {
            if (string.IsNullOrEmpty(relativePath)) return;

            // Convert /images/folder/file.jpg back to physical path
            string fullPath = Path.Combine(webRootPath, relativePath.TrimStart('/'));
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }
    }
}
