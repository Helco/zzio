using System;
using System.Numerics;
using System.Collections.Generic;
using System.Linq;
using DefaultEcs.System;
using zzio;
using zzre.game.messages.ui;

namespace zzre.game.systems.ui
{
    public partial class ScrDeck : BaseScreen<components.ui.ScrDeck, messages.ui.OpenDeck>
    {
        private static readonly components.ui.ElementId IDSliderUp = new(1);
        private static readonly components.ui.ElementId IDSliderDown = new(2);
        private static readonly components.ui.ElementId IDSlider = new(3);
        private static readonly components.ui.ElementId IDSwitchListMode = new(4);
        private static readonly components.ui.ElementId IDTabFairies = new(10);
        private static readonly components.ui.ElementId IDTabItems = new(11);
        private static readonly components.ui.ElementId IDTabAttackSpells = new(12);
        private static readonly components.ui.ElementId IDTabSupportSpells = new(13);

        public ScrDeck(ITagContainer diContainer) : base(diContainer)
        {
        }

        protected override void HandleOpen(in OpenDeck message)
        {
            var entity = World.CreateEntity();
            entity.Set<components.ui.ScrDeck>();
            ref var deck = ref entity.Get<components.ui.ScrDeck>();
            CreateBackgrounds(entity, ref deck);
            CreateListControls(entity, ref deck);

            preload.CreateTooltip(entity, new Vector2(-320 + 11, -240 + 11), "{205} - ");
        }

        private void CreateBackgrounds(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
        {
            preload.CreateImage(
                entity,
                - new Vector2(52, 240),
                "dec000",
                renderOrder: 1);

            deck.SpellBackground = preload.CreateImage(
                entity,
                - new Vector2(320, 240),
                "dec001",
                renderOrder: 1);

            deck.SummaryBackground = preload.CreateImage(
                entity,
                - new Vector2(320, 240),
                "dec002",
                renderOrder: 1);
        }

        private void CreateListControls(DefaultEcs.Entity entity, ref components.ui.ScrDeck deck)
        {
            var mid = - new Vector2(320, 240);

            preload.CreateImageButton(
                entity,
                IDSliderUp,
                mid + new Vector2(592, 73),
                new(16, 17),
                preload.Btn001);

            preload.CreateImageButton(
                entity,
                IDSliderDown,
                mid + new Vector2(592, 291),
                new(18, 19),
                preload.Btn001);

            deck.ListSlider = preload.CreateImageButton(
                entity,
                IDSlider,
                mid + new Vector2(592, 112),
                new(14, 15),
                preload.Btn001);

            var tabButtonRect = Rect.FromTopLeftSize(mid + new Vector2(281, 0f), new Vector2(35, 35));
            preload.CreateImageButton(
                entity,
                IDTabFairies,
                tabButtonRect.OffsettedBy(0, 79),
                new(0, 1, 2),
                preload.Btn002,
                tooltipUID: new UID(0x7DB4EEB1));

            preload.CreateImageButton(
                entity,
                IDTabItems,
                tabButtonRect.OffsettedBy(0, 123),
                new(3, 4, 5),
                preload.Btn002,
                tooltipUID: new UID(0x93530331));

            preload.CreateImageButton(
                entity,
                IDTabAttackSpells,
                tabButtonRect.OffsettedBy(0, 167),
                new(6, 7, 8),
                preload.Btn002,
                tooltipUID: new UID(0xB5E80331));

            preload.CreateImageButton(
                entity,
                IDTabSupportSpells,
                tabButtonRect.OffsettedBy(0, 211),
                new(9, 10, 11),
                preload.Btn002,
                tooltipUID: new UID(0x9D0DAD11));

            preload.CreateImageButton(
                entity,
                IDSwitchListMode,
                tabButtonRect.Min + new Vector2(15, 261),
                new(28, 29),
                preload.Btn002,
                tooltipUID: new UID(0xA086B911));

            // TODO: Add pixie count label
        }

        protected override void Update(
            float timeElapsed,
            ref components.ui.ScrDeck component)
        {
            //throw new NotImplementedException();
        }
    }
}
