using System.Threading;
using System.Threading.Tasks;
using RepoDash.Core.Settings;

namespace RepoDash.Core.Abstractions;

public interface IShortcutLauncher
{
    Task LaunchAsync(ShortcutEntry shortcut, CancellationToken cancellationToken = default);
}
