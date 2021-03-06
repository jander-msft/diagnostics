// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.SymbolStore;
using System;
using System.Collections.Immutable;

namespace Microsoft.Diagnostics.DebugServices
{
    public interface ISymbolService
    {
        /// <summary>
        /// Symbol store change event. The sender is the symbol store instance.
        /// </summary>
        public delegate void ChangeEventHandler(object sender, EventArgs e);

        /// <summary>
        /// Invoked when anything changes in the symbol service (adding servers, caches, or directories, clearing store, etc.)
        /// </summary>
        event ChangeEventHandler OnChangeEvent;

        /// <summary>
        /// Returns true if symbol download has been enabled.
        /// </summary>
        public bool IsSymbolStoreEnabled { get; }

        /// <summary>
        /// The default symbol cache path:
        /// 
        /// * dbgeng on Windows uses the dbgeng symbol cache path: %PROGRAMDATA%\dbg\sym
        /// * dotnet-dump on Windows uses the VS symbol cache path: %TEMPDIR%\SymbolCache
        /// * dotnet-dump/lldb on Linux/MacOS uses: $HOME/.dotnet/symbolcache
        /// </summary>
        public string DefaultSymbolCache { get; set; }

        /// <summary>
        /// Parses the Windows debugger symbol path (srv*, cache*, etc.).
        /// </summary>
        /// <param name="symbolPath">Windows symbol path</param>
        /// <returns>if false, error parsing symbol path</returns>
        public bool ParseSymbolPath(string symbolPath);

        /// <summary>
        /// Add symbol server to search path.
        /// </summary>
        /// <param name="msdl">if true, use the public Microsoft server</param>
        /// <param name="symweb">if true, use symweb internal server and protocol (file.ptr)</param>
        /// <param name="symbolServerPath">symbol server url (optional)</param>
        /// <param name="authToken"></param>
        /// <param name="timeoutInMinutes">symbol server timeout in minutes (optional)</param>
        /// <returns>if false, failure</returns>
        public bool AddSymbolServer(bool msdl, bool symweb, string symbolServerPath, string authToken, int timeoutInMinutes);

        /// <summary>
        /// Add cache path to symbol search path
        /// </summary>
        /// <param name="symbolCachePath">symbol cache directory path (optional)</param>
        public void AddCachePath(string symbolCachePath);

        /// <summary>
        /// Add directory path to symbol search path
        /// </summary>
        /// <param name="symbolDirectoryPath">symbol directory path to search (optional)</param>
        public void AddDirectoryPath(string symbolDirectoryPath);

        /// <summary>
        /// This function disables any symbol downloading support.
        /// </summary>
        public void DisableSymbolStore();

        /// <summary>
        /// Download a file from the symbol stores/server.
        /// </summary>
        /// <param name="key">index of the file to download</param>
        /// <returns>path to the downloaded file either in the cache or in the temp directory or null if error</returns>
        public string DownloadFile(SymbolStoreKey key);

        /// <summary>
        /// Attempts to download/retrieve from cache the key.
        /// </summary>
        /// <param name="key">index of the file to retrieve</param>
        /// <returns>stream or null</returns>
        public SymbolStoreFile GetSymbolStoreFile(SymbolStoreKey key);

        /// <summary>
        /// Returns the metadata for the assembly
        /// </summary>
        /// <param name="imagePath">file name and path to module</param>
        /// <param name="imageTimestamp">module timestamp</param>
        /// <param name="imageSize">size of PE image</param>
        /// <returns>metadata</returns>
        public ImmutableArray<byte> GetMetadata(string imagePath, uint imageTimestamp, uint imageSize);
    }
}
