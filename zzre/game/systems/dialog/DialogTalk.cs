using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DefaultEcs.System;
using zzio;
using zzio.db;
using zzio.vfs;

namespace zzre.game.systems
{
    public partial class DialogTalk : ui.BaseScreen<components.DialogTalk, messages.DialogTalk>
    {
        private readonly MappedDB db;
        private readonly IResourcePool resourcePool;
        private readonly IDisposable resetUIDisposable;

        public DialogTalk(ITagContainer diContainer) : base(diContainer, isBlocking: false)
        {
            db = diContainer.GetTag<MappedDB>();
            resourcePool = diContainer.GetTag<IResourcePool>();
            resetUIDisposable = World.Subscribe<messages.DialogResetUI>(HandleResetUI);
        }

        public override void Dispose()
        {
            base.Dispose();
            resetUIDisposable.Dispose();
        }

        private void HandleResetUI(in messages.DialogResetUI message)
        {
            foreach (var entity in Set.GetEntities())
                entity.Dispose();
        }

        protected override void HandleOpen(in messages.DialogTalk message)
        {
            // TODO: Fix talk text layout by overriding line height per label

            message.DialogEntity.Set(components.DialogState.Talk);

            var wasAlreadyOpen = Set.Count > 0;
            World.Publish(new messages.DialogResetUI(message.DialogEntity));
            var uiWorld = ui.GetTag<DefaultEcs.World>();
            var uiEntity = uiWorld.CreateEntity();
            uiEntity.Set(new components.Parent(message.DialogEntity));
            uiEntity.Set(new components.DialogTalk(message.DialogEntity));

            preload.CreateLabel( // TODO: Remove workaround after Doraku/DefaultEcs#159
                uiEntity,
                Vector2.One * 10000,
                "abcdef",
                preload.Fnt001);

            preload.CreateDialogBackground(uiEntity, animateOverlay: !wasAlreadyOpen, out var bgRect);
            CreateTalkLabel(uiEntity, message.DialogUID, bgRect);
            var npcEntity = message.DialogEntity.Get<components.DialogNPC>().Entity;
            var faceWidth = TryCreateFace(uiEntity, npcEntity, bgRect);
            CreateNameLabel(uiEntity, npcEntity, bgRect, faceWidth);
        }

        private const float MaxTextWidth = 400f;
        private const float TextOffsetX = 55f;
        private const float TextOffsetY = 195f;
        private void CreateTalkLabel(DefaultEcs.Entity parent, UID dialogUID, Rect bgRect)
        {
            var text = db.GetDialog(dialogUID).Text;
            if (text.Length > 0 && text[0] >= 'A' && text[0] <= 'Z' && false)
                text = $"{{8*{text[0]}}}{text[1..]}"; // use the ridiculous font for the first letter

            var entity = preload.CreateAnimatedLabel(
                parent,
                Vector2.Zero,
                text,
                preload.Fnt003,
                wrapLines: 400f);
            var tileSheet = entity.Get<rendering.TileSheet>();
            var textHeight = tileSheet.GetTextHeight(text);
            ref var labelRect = ref entity.Get<Rect>();
            labelRect = new Rect(
                bgRect.Min.X + TextOffsetX,
                bgRect.Min.Y + TextOffsetY - textHeight / 2,
                MaxTextWidth,
                textHeight);
        }

        private const string BaseFacePath = "resources/bitmaps/faces/";
        private float? TryCreateFace(DefaultEcs.Entity parent, DefaultEcs.Entity npcEntity, Rect bgRect)
        {
            var npcBodyEntity = npcEntity.Get<components.ActorParts>().Body;
            var npcModelName = npcBodyEntity.Get<resources.ClumpInfo>().Name
                .Replace(".dff", "", StringComparison.OrdinalIgnoreCase);
            var hasFace = resourcePool.FindFile($"{BaseFacePath}{npcModelName}.bmp") != null;

            if (!hasFace)
                return null;
            var faceEntity = preload.CreateImage(
                parent,
                bgRect.Min + Vector2.One * 20f,
                $"faces/{npcModelName}",
                renderOrder: 0);
            return faceEntity.Get<Rect>().Size.X;
        }

        private const float NameOffsetY = 35f;
        private void CreateNameLabel(DefaultEcs.Entity parent, DefaultEcs.Entity npcEntity, Rect bgRect, float? faceWidth)
        {
            var npcName = npcEntity.Get<NpcRow>().Name;
            var entity = preload.CreateLabel(
                parent,
                Vector2.Zero,
                npcName,
                preload.Fnt001);
            var tileSheet = entity.Get<rendering.TileSheet>();
            ref var rect = ref entity.Get<Rect>();
            rect = new Rect(
                faceWidth.HasValue
                    ? bgRect.Min.X + faceWidth.Value + 25
                    : bgRect.Center.X - tileSheet.GetUnformattedWidth(npcName) / 2,
                bgRect.Min.Y + NameOffsetY,
                0f, 0f);
        }

        protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.DialogTalk component)
        {
        }
    }
}
