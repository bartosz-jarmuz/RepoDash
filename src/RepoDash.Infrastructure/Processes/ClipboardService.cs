using System;
using System.Windows.Forms;
using RepoDash.Core.Abstractions;

namespace RepoDash.Infrastructure.Processes;

public sealed class ClipboardService : IClipboardService
{
    public void SetText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        try
        {
            Clipboard.SetText(text);
        }
        catch (Exception)
        {
            // Intentionally swallow clipboard errors for now.
        }
    }
}
