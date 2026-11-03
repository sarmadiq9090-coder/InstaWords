using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace InstaWords.App.Services
{
    /// <summary>Clipboard access with retries: another program may hold the clipboard open for a moment.</summary>
    public static class ClipboardService
    {
        public static bool TryGetText(out string text)
        {
            text = null;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    if (Clipboard.ContainsText(TextDataFormat.UnicodeText)) text = Clipboard.GetText(TextDataFormat.UnicodeText);
                    else if (Clipboard.ContainsText()) text = Clipboard.GetText();
                    return true;
                }
                catch (COMException) { Thread.Sleep(40); }
                catch (ExternalException) { Thread.Sleep(40); }
            }
            return false;
        }

        public static bool TrySetText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (var attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    var data = new DataObject();
                    data.SetData(DataFormats.UnicodeText, text);
                    data.SetData(DataFormats.Text, text);
                    Clipboard.SetDataObject(data, copy: true);
                    return true;
                }
                catch (COMException) { Thread.Sleep(40); }
                catch (ExternalException) { Thread.Sleep(40); }
            }
            return false;
        }
    }
}
