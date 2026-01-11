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

using System.Diagnostics.CodeAnalysis;

namespace MermaidPad.Views.UserControls;

/// <summary>
/// Provides mechanisms to capture and validate consistent snapshots of a view model and its version from a specified
/// source.
/// </summary>
/// <remarks>Use this class to ensure that operations on a view model are performed against a consistent and
/// up-to-date state. This is particularly useful in scenarios involving concurrency or change tracking, where it is
/// important to detect if the view model has changed between operations. The guard relies on an external source to
/// provide the current view model and its version.</remarks>
/// <typeparam name="TViewModel">The type of the view model to be guarded. Must be a reference type.</typeparam>
internal sealed class ViewModelVersionGuard<TViewModel> where TViewModel : class
{
    private readonly IViewModelVersionSource<TViewModel> _source;

    /// <summary>
    /// Initializes a new instance of the ViewModelVersionGuard class using the specified view model version source.
    /// </summary>
    /// <param name="source">The source that provides version information for the view model. Cannot be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if the provided source is null.</exception>
    public ViewModelVersionGuard(IViewModelVersionSource<TViewModel> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _source = source;
    }

    /// <summary>
    /// Attempts to capture a consistent snapshot of the current view model and its version.
    /// </summary>
    /// <remarks>A snapshot is considered consistent if the view model is not null and the version has not
    /// changed during the capture. If the operation fails, the out parameters are still set, but the captured view
    /// model will be null.</remarks>
    /// <param name="capturedViewModel">When this method returns, contains the captured view model if the
    /// snapshot was successful; otherwise, null. This parameter is passed uninitialized.</param>
    /// <param name="capturedVersion">When this method returns, contains the version number associated
    /// with the captured view model. This parameter is always set, regardless of success.</param>
    /// <returns>true if a consistent snapshot of the view model and version was captured; otherwise, false.</returns>
    public bool TryCaptureSnapshot([NotNullWhen(true)] out TViewModel? capturedViewModel, out long capturedVersion)
    {
        long versionBefore = _source.CurrentVersion;
        TViewModel? viewModel = _source.CurrentViewModel;
        long versionAfter = _source.CurrentVersion;

        bool isSuccess = viewModel is not null && versionBefore == versionAfter;
        capturedViewModel = viewModel;  // null when isSuccess == false, non-null when isSuccess == true
        capturedVersion = versionAfter; // use the final version after reading the view model; same when success == true

        return isSuccess;
    }

    /// <summary>
    /// Determines whether the specified view model and version still represent the current state of the source.
    /// </summary>
    /// <remarks>Use this method to check whether a previously captured view model and version are still
    /// valid, such as when implementing change tracking or concurrency checks.</remarks>
    /// <param name="capturedViewModel">The view model instance to validate against the current view model. Can be null.</param>
    /// <param name="capturedVersion">The version number to validate against the current version of the source.</param>
    /// <returns>true if the specified view model is the current view model and the version matches the current version;
    /// otherwise, false.</returns>
    public bool IsStillValid(TViewModel? capturedViewModel, long capturedVersion)
    {
        // If the caller didn't capture a view model (or intentionally passed null),
        // it cannot possibly be "still valid"
        if (capturedViewModel is null)
        {
            return false;
        }

        // IMPORTANT:
        // We intentionally read (version -> viewModel -> version) rather than reading
        // CurrentViewModel and CurrentVersion independently and trusting them to be consistent
        //
        // Why:
        // - The source can update the current VM reference and the version as separate operations.
        //   That means there can be a brief window where:
        //     CurrentViewModel == new VM
        //     CurrentVersion  == old version
        //   (or vice versa) depending on update order
        //
        // - If we read CurrentViewModel and CurrentVersion separately, a version bump could occur
        //   between those reads, and we would be comparing a "mixed" state (VM from time A, version
        //   from time B). The safe response is to treat that as invalid and bail
        long versionBefore = _source.CurrentVersion;
        TViewModel? currentViewModel = _source.CurrentViewModel;
        long versionAfter = _source.CurrentVersion;

        // If the version changed while we were reading, we don't have a coherent snapshot
        // of the source state, so we must not claim the captured pair is still valid.
        if (versionBefore != versionAfter)
        {
            return false;
        }

        // At this point, (currentViewModel, versionAfter) is a consistent snapshot.
        // To be "still valid", we require:
        // 1) Reference-equality: still the same VM instance
        // 2) Version match: still the same version we captured
        return ReferenceEquals(currentViewModel, capturedViewModel) && versionAfter == capturedVersion;
    }
}
