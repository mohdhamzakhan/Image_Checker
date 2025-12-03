using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Image_Checker.Utils
{
    public static class PathUtils
    {
        public static string ResolvePath(string[] args, string defaultPath)
        {
            if (args.Length > 0 && Directory.Exists(args[0]))
                return Path.GetFullPath(args[0]);

            if (Directory.Exists(defaultPath))
                return Path.GetFullPath(defaultPath);

            throw new DirectoryNotFoundException("No valid dataset directory found.");
        }

        public static string SanitizeFileName(string name)
        {
            var invalids = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalids.Contains(c) ? '_' : c));
        }
    }
}
