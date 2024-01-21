using System;
using System.Numerics;
using zzio;
using zzio.db;
using zzio.vfs;

namespace zzre.game.systems;

public partial class DialogTalk : ui.BaseScreen<components.ui.DialogTalk, messages.DialogTalk>
{
    private static readonly components.ui.ElementId IDExit = new(1);
    private static readonly components.ui.ElementId IDContinue = new(2);
    private static readonly components.ui.ElementId IDYes = new(3);
    private static readonly components.ui.ElementId IDNo = new(4);

    private readonly MappedDB db;
    private readonly IResourcePool resourcePool;
    private readonly IDisposable resetUIDisposable;

    public DialogTalk(ITagContainer diContainer) : base(diContainer, BlockFlags.None)
    {
        db = diContainer.GetTag<MappedDB>();
        resourcePool = diContainer.GetTag<IResourcePool>();
        resetUIDisposable = World.Subscribe<messages.DialogResetUI>(HandleResetUI);
        OnElementDown += HandleElementDown;
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
        message.DialogEntity.Set(components.DialogState.Talk);

        var wasAlreadyOpen = Set.Count > 0;
        World.Publish(new messages.DialogResetUI(message.DialogEntity));
        var uiEntity = World.CreateEntity();
        uiEntity.Set(new components.Parent(message.DialogEntity));
        uiEntity.Set(new components.ui.DialogTalk(message.DialogEntity));

        preload.CreateDialogBackground(uiEntity, animateOverlay: !wasAlreadyOpen, out var bgRect);
        CreateTalkLabel(uiEntity, message.DialogUID, bgRect);
        var npcEntity = message.DialogEntity.Get<components.DialogNPC>().Entity;
        var faceWidth = TryCreateFace(uiEntity, npcEntity, bgRect);
        CreateNameLabel(uiEntity, npcEntity, bgRect, faceWidth);

        var talkLabels = message.DialogEntity.Get<components.DialogTalkLabels>();
        if (talkLabels == components.DialogTalkLabels.Exit)
            CreateSingleButton(uiEntity, new UID(0xF7DFDC21), IDExit, bgRect);
        else if (talkLabels == components.DialogTalkLabels.Continue)
            CreateSingleButton(uiEntity, new UID(0xCABAD411), IDContinue, bgRect);
        else
            CreateYesNoButtons(uiEntity, bgRect);
    }

    private const float MaxTextWidth = 400f;
    private const float TextOffsetX = 55f;
    private const float TextOffsetY = 195f;
    private const float TalkLineHeight = 30f;
    private void CreateTalkLabel(DefaultEcs.Entity parent, UID dialogUID, Rect bgRect)
    {
        var text = db.GetDialog(dialogUID).Text;
        if (text.Length > 0 && text[0] >= 'A' && text[0] <= 'Z')
            text = $"{{8*{text[0]}}}{text[1..]}"; // use the ridiculous font for the first letter

        var entity = preload.CreateLabel(parent)
            .With(Vector2.Zero)
            .With(preload.Fnt003)
            .WithText(text)
            .WithLineHeight(TalkLineHeight)
            .WithLineWrap(MaxTextWidth)
            .WithAnimation()
            .Build();

        var wrappedText = entity.Get<components.ui.AnimatedLabel>().FullText;
        var tileSheet = entity.Get<rendering.TileSheet>();
        var textHeight = tileSheet.GetTextHeight(wrappedText, TalkLineHeight, removeFirstLine: true);
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
        if (!npcEntity.TryGet<components.ActorParts>(out var actorParts))
            return null;

        var npcModelName = actorParts.Body.Get<resources.ClumpInfo>().Name
            .Replace(".dff", "", StringComparison.OrdinalIgnoreCase);
        var hasFace = resourcePool.FindFile($"{BaseFacePath}{npcModelName}.bmp") != null;

        if (!hasFace)
            return null;
        var faceEntity = preload.CreateImage(parent)
            .With(bgRect.Min + Vector2.One * 20f)
            .WithBitmap($"faces/{npcModelName}")
            .Build();
        return faceEntity.Get<Rect>().Size.X;
    }

    private const float NameOffsetY = 35f;
    private void CreateNameLabel(DefaultEcs.Entity parent, DefaultEcs.Entity npcEntity, Rect bgRect, float? faceWidth)
    {
        var npcName = npcEntity.Get<NpcRow>().Name;
        if (string.IsNullOrWhiteSpace(npcName) || npcName == "-")
            return;

        var entity = preload.CreateLabel(parent)
            .WithText(npcName)
            .With(preload.Fnt001)
            .Build();
        var tileSheet = entity.Get<rendering.TileSheet>();
        ref var rect = ref entity.Get<Rect>();
        rect = new Rect(
            faceWidth.HasValue
                ? bgRect.Min.X + faceWidth.Value + 25
                : bgRect.Center.X - tileSheet.GetUnformattedWidth(npcName) / 2,
            bgRect.Min.Y + NameOffsetY,
            0f, 0f);
    }

    private const float YesNoButtonOffsetX = 4f;
    private const float ButtonOffsetY = -50f;
    private void CreateSingleButton(DefaultEcs.Entity parent, UID textUID, components.ui.ElementId elementId, Rect bgRect)
    {
        preload.CreateButton(parent)
            .With(elementId)
            .With(new Vector2(bgRect.Center.X, bgRect.Max.Y + ButtonOffsetY))
            .With(new components.ui.ButtonTiles(0, 1))
            .With(components.ui.FullAlignment.TopCenter)
            .With(preload.Btn000)
            .WithLabel()
            .With(preload.Fnt000)
            .WithText(textUID)
            .Build();

        // TODO: Set cursor position in dialog talk
    }

    private void CreateYesNoButtons(DefaultEcs.Entity parent, Rect bgRect)
    {
        preload.CreateButton(parent)
            .With(IDYes)
            .With(new Vector2(bgRect.Center.X + YesNoButtonOffsetX, bgRect.Max.Y + ButtonOffsetY))
            .With(new components.ui.ButtonTiles(0, 1))
            .With(components.ui.FullAlignment.TopRight)
            .With(preload.Btn000)
            .WithLabel()
            .With(preload.Fnt000)
            .WithText(0xB2153621)
            .Build();

        preload.CreateButton(parent)
            .With(IDNo)
            .With(new Vector2(bgRect.Center.X + YesNoButtonOffsetX, bgRect.Max.Y + ButtonOffsetY))
            .With(new components.ui.ButtonTiles(0, 1))
            .With(preload.Btn000)
            .WithLabel()
            .With(preload.Fnt000)
            .WithText(0x2F5B3621)
            .Build();
    }

    private void HandleElementDown(DefaultEcs.Entity clickedEntity, components.ui.ElementId clickedId)
    {
        var talkEntity = Set.GetEntities()[0];
        var dialogEntity = talkEntity.Get<components.ui.DialogTalk>().DialogEntity;
        if (clickedId == IDContinue || clickedId == IDExit)
        {
            // TODO: Play sound sample on dialog talk button clicked
            dialogEntity.Set(components.DialogState.NextScriptOp);
        }
        if (clickedId == IDExit)
            talkEntity.Dispose();

        if (clickedId == IDYes || clickedId == IDNo)
        {
            var talkLabels = dialogEntity.Get<components.DialogTalkLabels>();
            int targetLabel = clickedId == IDYes ? talkLabels.LabelYes : talkLabels.LabelNo;
            ref var scriptExec = ref dialogEntity.Get<components.ScriptExecution>();
            scriptExec.GoToLabel(targetLabel);
            dialogEntity.Set(components.DialogState.NextScriptOp);
            talkEntity.Dispose();
        }
    }

    protected override void Update(float timeElapsed, in DefaultEcs.Entity entity, ref components.ui.DialogTalk component)
    {
    }
}
