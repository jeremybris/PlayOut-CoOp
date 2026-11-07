using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Drawing;

namespace Nucleus.Gaming.Cache
{    /// <summary>
     /// Basic Picture cache system
     /// </summary>
    public static class ImageCache
    {
        private static ConcurrentDictionary<string, Bitmap> cachedImages = new ConcurrentDictionary<string, Bitmap>();
        private static System.Threading.Timer FreeMemoryTimer;
        private static bool initialized;

        public static Bitmap GetImage(string path)
        {
            if (!initialized)
            {
                FreeMemoryTimer = new System.Threading.Timer(FreeMemory_Tick, null, 0, 180000);
                initialized = true;
            }

            return AddGetImage(path);
        }

        private static Bitmap AddGetImage(string path)
        {
            if (AddToCache(path))
            {
                return cachedImages[path];
            }
            else
            {
                AddToCache(path);
                return AddGetImage(path);
            }
        }

        private static bool AddToCache(string path)
        {
            if (!cachedImages.ContainsKey(path))
            {
                cachedImages.TryAdd(path, new Bitmap(path));
                return false;
            }

            return true;
        }

        public static void DeleteImageFromCache(string path)
        {
            if(path == null)
            {
                return;
            }

            RemoveFromCache(path);
        }

        private static void RemoveFromCache(string path)
        {
            if (!initialized)
            {
                return;
            }

            if (cachedImages.ContainsKey(path))
            { 
                cachedImages.TryRemove(path, out Bitmap bitmap);
                bitmap?.Dispose();
            }
        }

        ///This will periodically clean image cache used by the Nucleus UI(mainly but not only).
        private static void FreeMemory_Tick(object state)
        {
            Process currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            double convertToMo = Math.Round(currentProcess.WorkingSet64 / 1e+6);
            ///Force gc for instant result, calling it every 3 minutes and only if "needed" 
            ///should be fine. It will not be called at all most of the time anyway.
            if (convertToMo > 400)
            {
                System.GC.Collect();
            }
        }
    }
}
