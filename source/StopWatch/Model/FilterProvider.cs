/**
 * Copyright 2023 Y. Meyer-Norwood
 * Copyright 2020 Dan Tulloh
 * Copyright 2016 Carsten Gehling
 *
 * For a full list of contributing authors, see:
 *
 *     https://jirastopwatch.com/contributors
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at:
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace StopWatch
{
    /// <summary>One entry of the filter list: what to show and what to query with.</summary>
    internal class FilterItem
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Jql { get; set; }

        public FilterItem(int id, string name, string jql)
        {
            Id = id;
            Name = name;
            Jql = jql;
        }

        /// <summary>
        /// Whether this entry is the separator rather than a real filter.
        /// Selecting it queries nothing.
        /// </summary>
        public bool IsSeparator
        {
            get { return Id == SeparatorId; }
        }

        public const int MyOpenIssuesId = -1;
        public const int SeparatorId = 0;
    }


    /// <summary>
    /// Owns the filter list and which filter is active.
    ///
    /// Until this existed, a row that wanted the active JQL reached for
    /// Application.OpenForms[0] and looked up a ComboBox by name - the ad-hoc
    /// controller layer the original authors left a TODO about. Now the JQL is
    /// a dependency a row is handed.
    /// </summary>
    internal class FilterProvider
    {
        #region public members
        /// <summary>Raised when the list of filters has been reloaded.</summary>
        public event EventHandler FiltersLoaded;

        /// <summary>The filters as last loaded, separator included.</summary>
        public IEnumerable<FilterItem> Filters
        {
            get { return filters; }
        }


        /// <summary>
        /// The filter the user has active, or null when none is selected. The
        /// separator never counts as selected.
        /// </summary>
        public FilterItem Current
        {
            get { return current; }
            set
            {
                if (value != null && value.IsSeparator)
                    return;

                current = value;

                if (value != null)
                    settings.CurrentFilter = value.Id;
            }
        }


        /// <summary>
        /// The JQL to query the active filter with, or an empty string when
        /// there is no filter selected. This is what an issue row is handed
        /// instead of reaching into the main window.
        /// </summary>
        public string CurrentJql
        {
            get { return current == null ? "" : current.Jql ?? ""; }
        }
        #endregion


        #region public methods
        public FilterProvider(IJiraOperations jira, Settings settings)
        {
            if (jira == null)
                throw new ArgumentNullException("jira");
            if (settings == null)
                throw new ArgumentNullException("settings");

            this.jira = jira;
            this.settings = settings;
        }


        /// <summary>
        /// Reloads the favourite filters from Jira and re-resolves the active
        /// one from the saved setting. A failed load leaves the previous list
        /// in place, which is what the main window did before.
        /// </summary>
        public async Task LoadAsync()
        {
            List<Filter> loaded = await Task.Run(() => jira.GetFavoriteFilters());
            if (loaded == null)
                return;

            loaded.Insert(0, new Filter
            {
                Id = FilterItem.MyOpenIssuesId,
                Name = "My open issues",
                Jql = "assignee = currentUser() AND resolution = Unresolved order by updated DESC"
            });

            // The separator only earns its place once there is something below
            // it to separate from.
            if (loaded.Count() > 1)
            {
                loaded.Insert(1, new Filter
                {
                    Id = FilterItem.SeparatorId,
                    Name = "--------------",
                    Jql = ""
                });
            }

            filters = loaded.Select(f => new FilterItem(f.Id, f.Name, f.Jql)).ToList();

            FilterItem saved = filters.FirstOrDefault(f => f.Id == settings.CurrentFilter);
            if (saved != null && !saved.IsSeparator)
                current = saved;

            EventHandler handler = FiltersLoaded;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
        #endregion


        #region private members
        private readonly IJiraOperations jira;
        private readonly Settings settings;

        private List<FilterItem> filters = new List<FilterItem>();
        private FilterItem current;
        #endregion
    }
}
