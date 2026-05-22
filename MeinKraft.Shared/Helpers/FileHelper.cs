public class FileHelper
{
    public static string[] DirectoryGetFiles(string path) => !Directory.Exists(path) ? [] : Directory.GetFiles(path);
}