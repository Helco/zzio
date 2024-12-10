using DefaultEcs.System;
using Silk.NET.SDL;

namespace zzre.game.systems;

public class OpenMenuKeys : ISystem<float>
{
    private const KeyCode MenuKey = KeyCode.KReturn;
    // private const KeyCode PauseKey = KeyCode.F1;
    private const KeyCode RuneMenuKey = KeyCode.KF2;
    private const KeyCode BookMenuKey = KeyCode.KF3;
    private const KeyCode MapMenuKey = KeyCode.KF4;
    private const KeyCode DeckMenuKey = KeyCode.KF5;
    // private const KeyCode EscapeKey = KeyCode.Escape;

    public bool IsEnabled { get; set; } = true;

    private readonly IZanzarahContainer zzContainer;
    private readonly PlayerControls playerControls;
    private readonly UI ui;
    private readonly Zanzarah zanzarah;

    public OpenMenuKeys(ITagContainer diContainer)
    {
        ui = diContainer.GetTag<UI>();
        zanzarah = diContainer.GetTag<Zanzarah>();
        playerControls = diContainer.GetTag<PlayerControls>();
        zzContainer = diContainer.GetTag<IZanzarahContainer>();
        zzContainer.OnKeyDown += HandleKeyDown;
    }

    public void Dispose()
    {
        zzContainer.OnKeyDown -= HandleKeyDown;
    }

    private void HandleKeyDown(KeyCode code)
    {
        var inventory = zanzarah.CurrentGame!.PlayerEntity.Get<Inventory>();
        if (!IsEnabled || playerControls.IsLocked)
            return;
        switch (code)
        {
            case RuneMenuKey:
                if (inventory.Contains(StdItemId.RuneFairyGarden))
                    ui.Publish<messages.ui.OpenRuneMenu>();
                break;
            case BookMenuKey:
                if (inventory.Contains(StdItemId.FairyBook))
                    ui.Publish<messages.ui.OpenBookMenu>();
                break;
            case MapMenuKey:
                if (inventory.Contains(StdItemId.MapFairyGarden))
                    ui.Publish<messages.ui.OpenMapMenu>();
                break;
            case MenuKey:
            case DeckMenuKey:
                if (inventory.Contains(StdItemId.FairyBag))
                    ui.Publish<messages.ui.OpenDeck>();
                break;
        }
    }

    public void Update(float state)
    {
    }
}
