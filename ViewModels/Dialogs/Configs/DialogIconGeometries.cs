// MIT License
// Copyright (c) 2025 Dave Black
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using Avalonia.Media;
using Avalonia.Threading;

namespace MermaidPad.ViewModels.Dialogs.Configs;

/// <summary>
/// Provides cached geometries for dialog icons used in UI elements.
/// </summary>
/// <remarks>
/// <para>
/// This class supplies efficient, reusable <see cref="StreamGeometry"/> instances for standard dialog
/// icons such as success, error, warning, and information. Geometries are cached to improve performance and should be
/// accessed only from the UI thread. Use the <see cref="GetGeometry(DialogIcon)"/> method to retrieve the geometry for
/// a specific icon.
/// </para>
/// <para>
/// Usage example:
/// <code>
/// MessageDialogConfig config = new MessageDialogConfig
/// {
///     IconData = DialogIconGeometries.GetGeometry(DialogIcon.Success),
///     IconColor = Avalonia.Media.Brushes.Green
/// };
/// </code>
/// </para>
/// </remarks>
internal static class DialogIconGeometries
{
    private static readonly Lock _lock = new Lock();
    private static readonly Dictionary<DialogIcon, StreamGeometry> _geometryCache = new Dictionary<DialogIcon, StreamGeometry>();

    #region Path data for icons

    /// <summary>
    /// A checkmark icon indicating success or completion.
    /// </summary>
    private const string Success = "M9 12l2 2 4-4";

    /// <summary>
    /// A circled checkmark icon indicating success with emphasis.
    /// </summary>
    private const string SuccessCircled = "M12,2 C17.52,2 22,6.48 22,12 C22,17.52 17.52,22 12,22 C6.48,22 2,17.52 2,12 C2,6.48 6.48,2 12,2 M9,16.17 L4.83,12 L3.41,13.41 L9,19 L21,7 L19.59,5.59 L9,16.17 Z";

    /// <summary>
    /// An error triangle icon for error states (triangle with exclamation mark).
    /// </summary>
    private const string Error = "M12,2L1,21H23M12,6L19.53,19H4.47M11,10V14H13V10H11M11,16V18H13V16H11Z";

    /// <summary>
    /// A circled exclamation mark icon for warning/caution states.
    /// </summary>
    private const string Warning = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M11,7V13H13V7H11M11,15V17H13V15H11Z";

    /// <summary>
    /// A circled "i" icon for informational messages.
    /// </summary>
    private const string Information = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M11,9V7H13V9H11M11,11V17H13V11H11Z";

    #endregion Path data for icons

    /// <summary>
    /// Returns a cached <see cref="StreamGeometry"/> for the specified icon.
    /// </summary>
    /// <param name="icon">The icon identifier.</param>
    /// <returns>A cached <see cref="StreamGeometry"/> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when called off the UI thread.</exception>
    /// <exception cref="ArgumentException">Thrown when the icon is <see cref="DialogIcon.None"/>.</exception>
    internal static StreamGeometry GetGeometry(DialogIcon icon)
    {
        // Ensure that creation and access happen on the UI thread
        if (!Dispatcher.UIThread.CheckAccess())
        {
            throw new InvalidOperationException($"{nameof(DialogIconGeometries.GetGeometry)} must be called on the UI thread. " +
                "Ensure dialog configurations are created on the UI thread or marshal the call using Dispatcher.UIThread.");
        }

        if (icon == DialogIcon.None)
        {
            throw new ArgumentException("DialogIcon.None is not a drawable icon. Specify a concrete DialogIcon value.", nameof(icon));
        }

        lock (_lock)
        {
            if (_geometryCache.TryGetValue(icon, out StreamGeometry? cachedGeometry))
            {
                return cachedGeometry;
            }

            string pathData = GetPathData(icon);
            try
            {
                // Per Avalonia documentation, using StreamGeometry is more efficient than PathGeometry.
                // See https://docs.avaloniaui.net/docs/guides/development-guides/improving-performance#use-streamgeometries-over-pathgeometries
                StreamGeometry geometry = StreamGeometry.Parse(pathData);
                _geometryCache.Add(icon, geometry);
                return geometry;
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Failed to parse dialog icon '{icon}'. PathData='{pathData}'.", exception);
            }
        }
    }

    /// <summary>
    /// Retrieves the path data string corresponding to the specified dialog icon.
    /// </summary>
    /// <param name="icon">The dialog icon for which to obtain the path data. Must be a defined value of the <see cref="DialogIcon"/>
    /// enumeration.</param>
    /// <returns>A string containing the path data for the specified dialog icon.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="icon"/> is not a recognized
    /// value of the <see cref="DialogIcon"/> enumeration.</exception>
    private static string GetPathData(DialogIcon icon)
    {
        return icon switch
        {
            DialogIcon.Success => Success,
            DialogIcon.SuccessCircled => SuccessCircled,
            DialogIcon.Error => Error,
            DialogIcon.Warning => Warning,
            DialogIcon.Information => Information,
            DialogIcon.None => throw new ArgumentException("DialogIcon.None has no path data. Specify a concrete DialogIcon value.", nameof(icon)),
            _ => throw new ArgumentOutOfRangeException(nameof(icon), icon, "Unknown dialog icon.")
        };
    }
}
