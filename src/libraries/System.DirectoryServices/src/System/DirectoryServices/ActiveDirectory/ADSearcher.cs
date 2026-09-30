// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Specialized;

namespace System.DirectoryServices.ActiveDirectory
{
    internal sealed class ADSearcher
    {
        private readonly DirectorySearcher _searcher;
        private static TimeSpan DefaultTimeSpan => new TimeSpan(120 * TimeSpan.TicksPerMinute);

        public ADSearcher(DirectoryEntry searchRoot, string filter, string[] propertiesToLoad, SearchScope scope)
        {
            _searcher = new DirectorySearcher(searchRoot, filter, propertiesToLoad, scope);

            // set all search preferences
            // don't cache the results on the client
            _searcher.CacheResults = false;
            // set the timeout to 2 minutes
            _searcher.ClientTimeout = DefaultTimeSpan;
            _searcher.ServerPageTimeLimit = DefaultTimeSpan;
            // Page Size needs to be set so that we
            // can get all the results even when the number of results
            // is greater than the server set limit (1000 in Win2000 and 1500 in Win2003)
            _searcher.PageSize = 512;
        }

        public ADSearcher(DirectoryEntry searchRoot, string? filter, string[] propertiesToLoad, SearchScope scope, bool pagedSearch, bool cacheResults)
        {
            _searcher = new DirectorySearcher(searchRoot, filter, propertiesToLoad, scope);
            // set proper time out
            _searcher.ClientTimeout = DefaultTimeSpan;
            if (pagedSearch)
            {
                _searcher.PageSize = 512;
                _searcher.ServerPageTimeLimit = DefaultTimeSpan;
            }

            _searcher.CacheResults = cacheResults;
        }

        public SearchResult? FindOne() => _searcher.FindOne();

        public SearchResultCollection FindAll() => _searcher.FindAll();

        public StringCollection PropertiesToLoad => _searcher.PropertiesToLoad;

        public string? Filter
        {
            get => _searcher.Filter;
            set => _searcher.Filter = value;
        }

        public void Dispose() => _searcher.Dispose();
    }
}
