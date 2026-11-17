using System;
using Ski.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Ski.Game
{
    /// <summary>
    /// The ski shop in the village: the outfit of the friend you skied there as. Colours and the
    /// plain prints are free; this friend's own things are listed too, locked until earned, with
    /// what earns them. Everything else belongs to someone else.
    /// </summary>
    public sealed class ShopPanel
    {
        readonly VisualElement _root, _list;
        readonly ShopStage _stage;
        readonly Action _onClose;
        readonly CharacterId _who;
        Outfit _outfit;
        readonly ScrollView _scroll;

        public static ShopPanel Open(Transform world, CharacterId who, Action onClose) => new ShopPanel(world, who, onClose);

        ShopPanel(Transform world, CharacterId who, Action onClose)
        {
            _onClose = onClose;
            _who = who;
            _stage = ShopStage.Create(world);
            var ui = Ui.Instance;
            _root = Ui.Div(ui.DialogLayer, "layer shop");
            var card = Ui.Div(_root, "card shadowed shop-card");
            var head = Ui.Div(card, "row shop-head");
            Ui.Text(head, "SKI SHOP", "h1");
            Ui.Text(head, SkierInfo.Get(who).Name.ToUpperInvariant(), "h2 shop-who");
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.AddToClassList("shop-scroll");
            card.Add(_scroll);
            _list = _scroll.contentContainer;
            var foot = Ui.Div(card, "row shop-foot");
            Ui.Btn(foot, "DONE", Close);

            var view = new Image { image = _stage.Texture, scaleMode = ScaleMode.ScaleToFit };
            view.AddToClassList("shop-view");
            // Drag to turn the model; while the button is held it stays where you put it.
            view.RegisterCallback<PointerDownEvent>(e => { _stage.Hold(true); view.CapturePointer(e.pointerId); });
            view.RegisterCallback<PointerMoveEvent>(e => { if ((e.pressedButtons & 1) != 0) _stage.Drag(-e.deltaPosition.x * 0.6f); });
            view.RegisterCallback<PointerUpEvent>(e => { _stage.Hold(false); view.ReleasePointer(e.pointerId); });
            view.RegisterCallback<PointerCaptureOutEvent>(_ => _stage.Hold(false));
            _root.Add(view);
            _outfit = SaveData.Outfit(who);
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
            float keep = _scroll.verticalScroller.value;
            _stage.Show(PersonLook.For(_who, _outfit));
            _list.Clear();
            var style = CharacterStyle.Get(_who);
            bool board = SkierInfo.Get(_who).Gear == Gear.Snowboard;
            Swatches("JACKET", _outfit.jacket, style.Jacket, v => _outfit.jacket = v);
            Swatches("TRIM", _outfit.trim, style.JacketTrim, v => _outfit.trim = v);
            Swatches("PANTS", _outfit.pants, style.Pants, v => _outfit.pants = v);
            Swatches("HELMET", _outfit.helmet, style.Helmet, v => _outfit.helmet = v);
            Choices("PATTERN", ItemKind.Pattern, Unlocks.Patterns, _outfit.pattern, v => _outfit.pattern = v);
            Choices("HEAD", ItemKind.Headgear, Unlocks.Headgear, _outfit.headgear, v => _outfit.headgear = v);
            Choices("SUIT", ItemKind.Suit, Unlocks.Suits, _outfit.suit, v => _outfit.suit = v);
            Choices(board ? "BOARD" : "SKIS", ItemKind.Gear, Unlocks.Gear, _outfit.gear, v => _outfit.gear = v);
            Swatches(board ? "BOARD COLOUR" : "SKI COLOUR", _outfit.gearColour, style.Gear, v => _outfit.gearColour = v);
            Swatches(board ? "BOARD DETAIL" : "SKI DETAIL", _outfit.gearAccent, style.GearAccent, v => _outfit.gearAccent = v);
            _scroll.schedule.Execute(() => _scroll.verticalScroller.value = keep);
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
            var p = SaveData.ProgressOf(_who);
            for (int i = 0; i < names.Length; i++)
            {
                if (!Unlocks.Listed(_who, kind, i)) continue;
                int v = i;
                var unlock = Unlocks.For(kind, v);
                bool open = Unlocks.Available(_who, p, kind, v);
                var item = new Button(() => { if (!open) { AudioDirector.Sfx("gate_miss", 0.4f); return; } set(v); Changed(); });
                item.AddToClassList("shop-item");
                if (v == current) item.AddToClassList("shop-item-on");
                if (!open) item.AddToClassList("shop-item-locked");
                var name = new Label(open || unlock == null ? names[i] : "\U0001F512 " + names[i]);
                name.AddToClassList("shop-item-name");
                name.pickingMode = PickingMode.Ignore;
                item.Add(name);
                if (!open && unlock != null)
                {
                    var goal = new Label(unlock.Goal);
                    goal.AddToClassList("shop-item-goal");
                    goal.pickingMode = PickingMode.Ignore;
                    item.Add(goal);
                }
                wrap.Add(item);
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
