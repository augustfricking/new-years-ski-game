using System;
using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The ski shop: the four friends' outfits. Colours and plain patterns are free; the rest are
    /// shown locked with what earns them, and get sillier the harder they are to earn.
    /// </summary>
    public sealed class ShopPanel
    {
        readonly VisualElement _root, _list;
        readonly ShopStage _stage;
        readonly Action _onClose;
        CharacterId _who;
        Outfit _outfit;
        readonly Label _name;
        readonly VisualElement _tabs;

        public static ShopPanel Open(Transform world, CharacterId who, Action onClose) => new ShopPanel(world, who, onClose);

        ShopPanel(Transform world, CharacterId who, Action onClose)
        {
            _onClose = onClose;
            _stage = ShopStage.Create(world);
            var ui = Ui.Instance;
            _root = Ui.Div(ui.DialogLayer, "layer shop");
            var card = Ui.Div(_root, "card shadowed shop-card");
            var head = Ui.Div(card, "row");
            Ui.Text(head, "SKI SHOP", "h1");
            _name = Ui.Text(head, "", "h2 shop-who");
            _tabs = Ui.Div(card, "row shop-tabs");
            for (int i = 0; i < 4; i++)
            {
                var id = (CharacterId)i;
                Ui.Btn(_tabs, SkierInfo.Get(id).Name.ToUpperInvariant(), () => Select(id), "btn-plain btn-small");
            }
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.AddToClassList("shop-scroll");
            card.Add(scroll);
            _list = scroll.contentContainer;
            var foot = Ui.Div(card, "row");
            foot.style.justifyContent = Justify.FlexEnd;
            Ui.Btn(foot, "DONE", Close);

            var view = new Image { image = _stage.Texture, scaleMode = ScaleMode.ScaleToFit };
            view.AddToClassList("shop-view");
            view.RegisterCallback<PointerMoveEvent>(e => { if ((e.pressedButtons & 1) != 0) _stage.Drag(-e.deltaPosition.x * 0.6f); });
            _root.Add(view);
            Select(who);
        }

        void Select(CharacterId who)
        {
            _who = who;
            _outfit = SaveData.Outfit(who);
            _name.text = SkierInfo.Get(who).Name;
            int i = 0;
            foreach (var t in _tabs.Children()) t.EnableInClassList("btn-selected", i++ == (int)who);
            Refresh();
        }

        void Changed()
        {
            SaveData.SetOutfit(_who, _outfit);
            AudioDirector.Click();
            Refresh();
            LiveRooms.OutfitChanged();
        }

        void Refresh()
        {
            _stage.Show(PersonLook.For(_who, _outfit));
            _list.Clear();
            var style = CharacterStyle.Get(_who);
            Swatches("JACKET", _outfit.jacket, style.Jacket, v => _outfit.jacket = v);
            Swatches("TRIM", _outfit.trim, style.JacketTrim, v => _outfit.trim = v);
            Swatches("PANTS", _outfit.pants, style.Pants, v => _outfit.pants = v);
            Swatches("HELMET", _outfit.helmet, style.Helmet, v => _outfit.helmet = v);
            Choices("PATTERN", ItemKind.Pattern, Unlocks.Patterns, _outfit.pattern, v => _outfit.pattern = v);
            Choices("HEAD", ItemKind.Headgear, Unlocks.Headgear, _outfit.headgear, v => _outfit.headgear = v);
            Choices("SUIT", ItemKind.Suit, Unlocks.Suits, _outfit.suit, v => _outfit.suit = v);
        }

        void Swatches(string label, int current, Color own, Action<int> set)
        {
            var row = Ui.Div(_list, "shop-row");
            Ui.Text(row, label, "shop-label");
            var wrap = Ui.Div(row, "shop-swatches");
            void Add(int index, Color c)
            {
                var sw = new Button(() => { set(index); Changed(); });
                sw.AddToClassList("shop-swatch");
                if (index == current) sw.AddToClassList("shop-swatch-on");
                sw.style.backgroundColor = c;
                if (index < 0) sw.text = "*";
                wrap.Add(sw);
            }
            Add(-1, own);
            for (int i = 0; i < OutfitPaint.Colours.Length; i++) Add(i, OutfitPaint.Colours[i]);
        }

        void Choices(string label, ItemKind kind, string[] names, int current, Action<int> set)
        {
            var row = Ui.Div(_list, "shop-row");
            Ui.Text(row, label, "shop-label");
            var wrap = Ui.Div(row, "shop-choices");
            var p = SaveData.Progress;
            for (int i = 0; i < names.Length; i++)
            {
                int v = i;
                var unlock = Unlocks.For(kind, v);
                bool open = unlock == null || unlock.Earned(p);
                var item = Ui.Div(wrap, "shop-item" + (v == current ? " shop-item-on" : "") + (open ? "" : " shop-item-locked"));
                Ui.Text(item, names[i], "shop-item-name");
                if (!open) Ui.Text(item, unlock.Goal, "shop-item-goal");
                if (open) item.RegisterCallback<ClickEvent>(_ => { set(v); Changed(); });
            }
        }

        public void Close()
        {
            if (_root.parent == null) return;
            _root.RemoveFromHierarchy();
            if (_stage != null) UnityEngine.Object.Destroy(_stage.gameObject);
            AudioDirector.Click();
            _onClose?.Invoke();
        }

        public bool IsOpen => _root.parent != null;
    }
}
