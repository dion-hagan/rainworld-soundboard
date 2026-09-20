using System.Collections.Generic;
using Menu.Remix.MixedUI;
using UnityEngine;

namespace SoundboardMod
{
    /// <summary>
    /// A Remix dropdown you can search the moment it opens. Remix's own OpComboBox has a search too
    /// (decompiled: OpComboBox), but it's hard to find and blunt:
    /// - it only starts after a quick DOUBLE-click on the box; typing into a list opened with one click
    ///   does nothing (KeyboardAccept returns unless _searchMode);
    /// - it waits 10-50 frames after the last key before it filters;
    /// - ListItem.SearchMatch also accepts a query whose letters merely appear in order anywhere
    ///   ("die" finds "dandelion"), which buries the real matches in a long list;
    /// - it only lets letters, digits and spaces through, so "_", "-" and "." (all common in file names) are dropped;
    /// - the results stay in alphabetical order rather than putting the best match first.
    /// This one reuses that machinery (the search list, the cursor, the scroll bar, the hover
    /// descriptions, selecting by click) but starts searching as soon as the list opens with the mouse,
    /// filters on every key with <see cref="DropdownFilter"/> and lets Enter pick the top result.
    /// </summary>
    public sealed class SearchComboBox : OpComboBox
    {
        private const string Hint = "type to search...";

        // OpComboBox waits until _searchIdle reaches _searchDelay and then rebuilds the list its own
        // way. _EnterSearchMode parks it at 1000 for the same reason; every key does the same here.
        private const int NoAutoRefresh = 1000;

        private DropdownFilter filter;
        private ListItem[] filterSource;

        public SearchComboBox(Configurable<string> config, Vector2 pos, float width, List<ListItem> list)
            : base(config, pos, width, list)
        {
            // Swap the base class's key handler (KeyboardAccept) for ours.
            OnKeyDown = KeyPressed;
        }

        public override void Update()
        {
            base.Update();

            // The list has just been opened (the base class clears _searchMode whenever it closes, so
            // this happens once per opening): start searching straight away. Not for a controller -
            // searching makes the menu switch to mouse control, and a pad can't type anyway.
            if (held && !_searchMode && MenuMouseMode && !greyedOut && _lblList.Length > 0)
            {
                _searchMode = true;
                _EnterSearchMode();
            }
        }

        public override void GrafUpdate(float timeStacker)
        {
            base.GrafUpdate(timeStacker);

            // Until something is typed the box would otherwise go blank (the search text replaces the value).
            if (_searchMode && _searchQuery.Length == 0)
            {
                _lblText.text = Hint;
                _lblText.color = Color.Lerp(_rect.colorEdge, colorFill, 0.5f);
            }
        }

        protected override bool CopyFromClipboard(string value)
        {
            if (!_searchMode)
            {
                _searchMode = true;
                _EnterSearchMode();
            }

            _searchQuery = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ');
            Refilter();
            return true;
        }

        private void KeyPressed(char input)
        {
            if (!held || !_searchMode || _bumpScroll.held)
            {
                return;
            }

            if (input == '\b')
            {
                if (_searchQuery.Length > 0)
                {
                    _searchQuery = _searchQuery.Substring(0, _searchQuery.Length - 1);
                    PlaySound(SoundID.MENY_Already_Selected_MultipleChoice_Clicked);
                    Refilter();
                }
            }
            else if (input == '\r' || input == '\n')
            {
                PickFirst();
            }
            else if (!char.IsControl(input))
            {
                bumpBehav.flash = 2.5f;
                _searchQuery += input;
                PlaySound(SoundID.MENU_Checkbox_Uncheck);
                Refilter();
            }
        }

        /// <summary>Enter: choose the best match and close the list (nothing typed yet: leave the choice alone).</summary>
        private void PickFirst()
        {
            if (_searchQuery.Trim().Length == 0 || _searchList.Count == 0)
            {
                return;
            }

            string name = _searchList[0].name;
            _mouseDown = false;
            if (name != value)
            {
                value = name;
                PlaySound(SoundID.MENU_MultipleChoice_Clicked);
            }

            // Closes the list, the same way letting go of the controller's accept button does.
            NonMouseSetHeld(false);
        }

        private void Refilter()
        {
            // The list can gain or lose items while the screen is open (AddItems/RemoveItems replace the array).
            if (filter == null || !ReferenceEquals(filterSource, _itemList))
            {
                filterSource = _itemList;
                var texts = new string[_itemList.Length][];
                for (int i = 0; i < texts.Length; i++)
                {
                    // What the player sees in the list (for the Edit page's entries the name is an internal id).
                    texts[i] = new[] { _itemList[i].EffectiveDisplayName };
                }

                filter = new DropdownFilter(texts);
            }

            _searchList.Clear();
            foreach (int index in filter.Search(_searchQuery))
            {
                _searchList.Add(_itemList[index]);
            }

            _listTop = 0;
            _searchIdle = NoAutoRefresh;
        }
    }
}
