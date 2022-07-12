using System;
using DefaultEcs;
using zzre.game.systems.ui;

using TileSheetResource = DefaultEcs.Resource.ManagedResource<zzre.game.resources.UITileSheetInfo, zzre.rendering.TileSheet>;

namespace zzre.game.uibuilder
{
    internal abstract record ButtonLike<T> : Identified<T> where T : ButtonLike<T>
    {
        protected components.ui.ButtonTiles? buttonTiles;
        protected TileSheetResource? tileSheet;
        protected components.ui.FullAlignment btnAlign;

        protected ButtonLike(UIPreloader preload, Entity parent) : base(preload, parent)
        {
        }

        public T With(components.ui.ButtonTiles buttonTiles)
        {
            this.buttonTiles = buttonTiles;
            return (T)this;
        }

        public T With(TileSheetResource tileSheet)
        {
            if (this.tileSheet != null)
                throw new InvalidCastException("Tile sheet was already set on button-like UI element");
            this.tileSheet = tileSheet;
            return (T)this;
        }

        public T With(components.ui.FullAlignment btnAlign)
        {
            this.btnAlign = btnAlign;
            return (T)this;
        }

        protected override Entity BuildBase()
        {
            if (!buttonTiles.HasValue)
                throw new InvalidOperationException("Button-like UI element has no tiles");
            if (!tileSheet.HasValue)
                throw new InvalidOperationException("Button-like UI element has no tile sheet");
            var entity = base.BuildBase();
            entity.Set(btnAlign);
            entity.Set(tileSheet.Value);
            entity.Set(buttonTiles.Value);
            return entity;
        }
    }
}
