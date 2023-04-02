using System;

namespace zzre.game.components.ui;

public struct ScrNotification
{
    public int CurTextIndex;
    public bool IsFading;
    public bool WasButtonClicked;
    public float TimeLeft;
    public DefaultEcs.Entity MainOverlay;
    public DefaultEcs.Entity TopBorder;
    public DefaultEcs.Entity BottomBorder;
    public DefaultEcs.Entity TextLabel;
    public DefaultEcs.Entity? IconImage;
    public DefaultEcs.Entity? Button;
    public messages.ui.Notification Message;
};
