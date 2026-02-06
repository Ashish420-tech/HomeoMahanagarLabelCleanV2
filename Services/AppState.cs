using HomeoMahanagarLabelCleanV2.Models;

namespace HomeoMahanagarLabelCleanV2.Services
{
    public static class AppState
    {
        public static AppStorage Storage { get; private set; }

        static AppState()
        {
            Storage = StorageService.Load();
        }

        public static void Save()
        {
            StorageService.Save(Storage);
        }
    }
}
