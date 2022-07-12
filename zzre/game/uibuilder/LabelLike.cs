using System.Numerics;
using DefaultEcs;
using zzre.game.systems.ui;

using TileSheetResource = DefaultEcs.Resource.ManagedResource<zzre.game.resources.UITileSheetInfo, zzre.rendering.TileSheet>;

namespace zzre.game.uibuilder
{
    internal abstract record LabelLike<T> : Base<T> where T : LabelLike<T>
    {
        protected string text = "";
        protected TileSheetResource? font;
        protected components.ui.FullAlignment? textAlign;
        protected float? lineHeight;
        protected float wrapLines = float.NaN;
        protected bool doFormat = true;
        protected bool useTotalFontHeight = false;
        protected int? segmentsPerAdd;
        protected bool isBlinking = false;
        // an unfortunate special case that probably was a bug in Zanzarah.
        // The tile sheet height might not be the default line height and layout 
        // sometimes depend on the former value (e.g. button labels)

        protected LabelLike(UIPreloader preload, Entity parent) : base(preload, parent)
        {
        }

        public T WithText(string text)
        {
            this.text = text;
            return (T)this;
        }

        public T WithText(zzio.UID uid)
        {
            text = preload.GetDBText(uid);
            return (T)this;
        }

        public T WithText(uint rawUID) => WithText(new zzio.UID(rawUID));

        public T With(TileSheetResource font)
        {
            this.font = font;
            return (T)this;
        }

        public T With(components.ui.FullAlignment textAlign)
        {
            this.textAlign = textAlign;
            return (T)this;
        }

        public T WithLineHeight(float lineHeight)
        {
            this.lineHeight = lineHeight;
            return (T)this;
        }

        public T WithLineWrap(float wrapLines)
        {
            this.wrapLines = wrapLines;
            return (T)this;
        }

        public T WithAnimation(int segmentsPerAdd = 4, bool isBlinking = false)
        {
            this.segmentsPerAdd = segmentsPerAdd;
            this.isBlinking = isBlinking;
            return (T)this;
        }

        public T Unformatted()
        {
            doFormat = false;
            return (T)this;
        }

        protected override Entity BuildBase()
        {
            if (font == null)
                throw new System.InvalidOperationException("Font was not set on label-like UI element");
            var entity = base.BuildBase();
            entity.Set(font.Value);
            var tileSheet = entity.Get<rendering.TileSheet>();

            if (lineHeight == null && useTotalFontHeight)
                lineHeight = tileSheet.TotalSize.Y;
            if (float.IsFinite(wrapLines) && wrapLines > 0f)
                text = tileSheet.WrapLines(text, wrapLines);
            if (textAlign != null)
            {
                var size = new Vector2(tileSheet.GetUnformattedWidth(text), tileSheet.GetTextHeight(text, lineHeight));
                AlignToSize(entity, size, textAlign);
                entity.Set(rect = Rect.FromMinMax(rect.Min, rect.Min));
            }

            entity.Set(new components.ui.Label(text, doFormat, lineHeight));
            if (segmentsPerAdd.HasValue)
                entity.Set(new components.ui.AnimatedLabel(text, segmentsPerAdd.Value, isBlinking));

            return entity;
        }
    }
}
