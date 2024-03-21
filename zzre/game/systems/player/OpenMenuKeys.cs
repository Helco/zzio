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

    public OpenMenuKeys(ITagContainer diContainer)
    {
        ui = diContainer.GetTag<UI>();
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
        if (!IsEnabled || playerControls.IsLocked)
            return;
        switch (code)
        {
            case MenuKey: ui.Publish<messages.ui.OpenDeck>(); break;
            case RuneMenuKey: ui.Publish<messages.ui.OpenRuneMenu>(); break;
            case BookMenuKey: ui.Publish<messages.ui.OpenBookMenu>(); break;
            case MapMenuKey: ui.Publish<messages.ui.OpenMapMenu>(); break;
            case DeckMenuKey: ui.Publish<messages.ui.OpenDeck>(); break;
        }
    }

    public void Update(float state)
    {
    }
}
