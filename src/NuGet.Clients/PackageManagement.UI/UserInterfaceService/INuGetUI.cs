﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Packaging.Core;
using NuGet.ProjectManagement;
using NuGet.Protocol.Core.Types;
using NuGet.Resolver;

namespace NuGet.PackageManagement.UI
{
    /// <summary>
    /// The NuGet package management UI
    /// </summary>
    /// <remarks>This is not expected to be thread safe.</remarks>
    public interface INuGetUI
    {
        bool WarnAboutDotnetDeprecation(IEnumerable<NuGetProject> projects);

        bool PromptForLicenseAcceptance(IEnumerable<PackageLicenseInfo> packages);

        void LaunchExternalLink(Uri url);

        void LaunchNuGetOptionsDialog(OptionsPage optionsPageToOpen);

        /// <summary>
        /// Displays the preview window with options to accept or cancel
        /// </summary>
        bool PromptForPreviewAcceptance(IEnumerable<PreviewResult> actions);

        /// <summary>
        /// Marks the beginning of NuGet operation
        /// </summary>
        void BeginOperation();

        /// <summary>
        /// Marks the ending of NuGet operation
        /// </summary>
        void EndOperation();

        /// <summary>
        /// Common operations
        /// </summary>
        ICommonOperations CommonOperations { get; }

        /// <summary>
        /// Shared UI context
        /// </summary>
        INuGetUIContext UIContext { get; }

        /// <summary>
        /// A project context used for NuGet operations
        /// </summary>
        INuGetProjectContext ProjectContext { get; }

        /// <summary>
        /// Target projects
        /// </summary>
        IEnumerable<NuGetProject> Projects { get; }

        /// <summary>
        /// True if the option to preview actions first is checked
        /// </summary>
        bool DisplayPreviewWindow { get; }

        /// <summary>
        /// True if the option to ignore the deprecated framework window is unchecked
        /// </summary>
        bool DisplayDeprecatedFrameworkWindow { get; }

        /// <summary>
        /// Package currently selected in the UI
        /// </summary>
        PackageIdentity SelectedPackage { get; }

        /// <summary>
        /// Reports that an error has occurred.
        /// </summary>
        void ShowError(Exception ex);

        /// <summary>
        /// File conflict option
        /// </summary>
        FileConflictAction FileConflictAction { get; }

        /// <summary>
        /// Fires SolutionManager.ActionsExecuted event so that the UI will get 
        /// refreshed.
        /// </summary>
        void OnActionsExecuted(IEnumerable<ResolvedAction> actions);

        IEnumerable<SourceRepository> ActiveSources { get; }

        bool RemoveDependencies { get; }

        bool ForceRemove { get; }

        DependencyBehavior DependencyBehavior { get; }
    }
}