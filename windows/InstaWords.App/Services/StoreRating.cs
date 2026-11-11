using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using InstaWords.App.Views;

namespace InstaWords.App.Services
{
    /// <summary>
    /// «قيّم InstaWords» (Store copy only): the Microsoft Store's own rating dialog, through the WinRT
    /// StoreContext API (Microsoft.Windows.SDK.Contracts, like StoreStartupTask), shown over the app's window.
    /// When that dialog cannot open, the app's review page in the Store app instead. When to ask is
    /// <see cref="StoreLogic.RatingDue"/>; no reward of any kind is offered (Store policy).
    /// </summary>
    public static class StoreRating
    {
        /// <summary>Opens the Store's rating dialog over <paramref name="owner"/>. Does nothing outside the Store copy.</summary>
        public static Task RequestAsync(Window owner)
        {
            // Checked before any WinRT type is touched: installed and portable copies never load them.
            if (!StoreLogic.CanRate(AppPackage.Kind)) return Task.CompletedTask;
            var hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            return StoreDialog.RequestAsync(hwnd);
        }

        /// <summary>The app's «Ratings and reviews» page in the Microsoft Store app.</summary>
        public static void OpenReviewPage()
        {
            try
            {
                Process.Start(new ProcessStartInfo(StoreLogic.ReviewUri) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Warn("Store review page: " + ex.Message);
                ToastWindow.ShowMessage(false, Loc.T("Common.OpenLinkFailed"), ex.Message);
            }
        }

        private static class StoreDialog
        {
            public static async Task RequestAsync(IntPtr hwnd)
            {
                var status = RateReviewStatus.Error;
                try
                {
                    var context = Windows.Services.Store.StoreContext.GetDefault();
                    // A desktop app must give the Store dialog its owner window, or the call fails.
                    if (hwnd != IntPtr.Zero) ((IInitializeWithWindow)(object)context).Initialize(hwnd);
                    var result = await context.RequestRateAndReviewAppAsync();
                    status = (RateReviewStatus)(int)result.Status;
                    if (result.ExtendedError != null) Log.Warn("Store rating: " + result.ExtendedError.Message);
                }
                catch (Exception ex)
                {
                    // For example Windows 10 before 1809 (no RequestRateAndReviewAppAsync).
                    Log.Warn("Store rating: " + ex.Message);
                }
                if (StoreLogic.OpenReviewPageInstead(status)) OpenReviewPage();
            }
        }

        [ComImport]
        [Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IInitializeWithWindow
        {
            void Initialize(IntPtr hwnd);
        }
    }
}
