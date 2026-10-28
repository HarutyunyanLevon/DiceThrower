using System;
using System.Collections.Generic;
using UnityEngine.UIElements;

namespace DiceThrower.UI
{
    /// <summary>The tab strip and its pages. Tapping a tab reports its index; Select shows one page and hides the rest.</summary>
    public sealed class TabBar
    {
        private const string ActiveClass = "tab--active";

        private readonly List<VisualElement> _tabs = new List<VisualElement>();
        private readonly List<VisualElement> _pages = new List<VisualElement>();

        public TabBar(VisualElement root, string[] tabNames, string[] pageNames, Action<int> onTapped)
        {
            for (int i = 0; i < tabNames.Length; i++)
            {
                int index = i;
                VisualElement tab = root.Q<VisualElement>(tabNames[i]);
                _tabs.Add(tab);
                _pages.Add(root.Q<VisualElement>(pageNames[i]));
                PressGesture.Tappable(tab, () => onTapped(index));
            }
        }

        public void Select(int index)
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                _tabs[i].EnableInClassList(ActiveClass, i == index);
                _pages[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }
    }
}
