using System;
using System.Collections.Generic;
using System.Numerics;
using Veldrid;
using zzio.vfs;
using zzre.game;
using zzre.imgui;

namespace zzre.tools;

public class ZanzarahWindow : IZanzarahContainer, IECSWindow
{
    private readonly ITagContainer diContainer;
    private readonly FramebufferArea fbArea;
    private readonly MouseEventArea mouseArea;
    private readonly OpenFileModal selectSceneModal;
    private readonly HashSet<Key> keysDown = new();
    private readonly HashSet<MouseButton> buttonsDown = new();
    private Action<Vector2>? onMouseMove;
    private bool moveCamWithDrag;
    private ECSExplorer? ecsExplorer;
    private bool isFirstFrame = true;

    private bool MoveCamWithDrag
    {
        get => moveCamWithDrag;
        set
        {
            moveCamWithDrag = value;
            if (value)
            {
                mouseArea.OnMove -= InvokeMouseMove;
                mouseArea.OnDrag += HandleMouseDrag;
            }
            else
            {
                mouseArea.OnMove += InvokeMouseMove;
                mouseArea.OnDrag -= HandleMouseDrag;
            }
        }
    }

    public event Action OnResize
    {
        add => fbArea.OnResize += value;
        remove => fbArea.OnResize -= value;
    }

    public event Action<Key> OnKeyDown
    {
        add => Window.OnKeyDown += value;
        remove => Window.OnKeyDown -= value;
    }

    public event Action<Key> OnKeyUp
    {
        add => Window.OnKeyUp += value;
        remove => Window.OnKeyUp -= value;
    }

    public event Action<MouseButton, Vector2> OnMouseDown
    {
        add => mouseArea.OnButtonDown += value;
        remove => mouseArea.OnButtonDown -= value;
    }

    public event Action<MouseButton, Vector2> OnMouseUp
    {
        add => mouseArea.OnButtonUp += value;
        remove => mouseArea.OnButtonUp -= value;
    }

    public event Action<Vector2> OnMouseMove
    {
        add => onMouseMove += value;
        remove => onMouseMove -= value;
    }

    public Window Window { get; }
    public Zanzarah Zanzarah { get; }
    public Framebuffer Framebuffer => fbArea.Framebuffer;
    public Vector2 MousePos => mouseArea.MousePosition;
    public bool IsMouseCaptured { get; set; }

    public ZanzarahWindow(ITagContainer diContainer)
    {
        this.diContainer = diContainer;
        Window = diContainer.GetTag<WindowContainer>().NewWindow("Zanzarah");
        Window.AddTag(this);
        Window.InitialBounds = new Rect(float.NaN, float.NaN, 1040, 800); // a bit more to compensate for borders (about)

        fbArea = new FramebufferArea(Window, diContainer.GetTag<GraphicsDevice>());
        mouseArea = new MouseEventArea(Window);
        Zanzarah = new Zanzarah(diContainer, this);
        Window.AddTag(Zanzarah);

        selectSceneModal = new(diContainer)
        {
            Filter = "sc_*.scn",
            IsFilterChangeable = false
        };
        selectSceneModal.OnOpenedResource += TeleportToScene;

        var menuBar = new MenuBarWindowTag(Window);
        menuBar.AddCheckbox(
            "Controls/Move camera by dragging",
            () => ref moveCamWithDrag,
            () => MoveCamWithDrag = MoveCamWithDrag);
        menuBar.AddButton("Open scene", HandleOpenScene);
        menuBar.AddButton("Debug/ECS Explorer", HandleOpenECSExplorer);
        menuBar.AddButton("Debug/Teleport to scene", selectSceneModal.Modal.Open);

        Window.OnContent += HandleContent;
        fbArea.OnRender += Zanzarah.Render;
        fbArea.OnResize += HandleResize;
        OnKeyDown += HandleKeyDown;
        OnKeyUp += HandleKeyUp;
        OnMouseDown += HandleMouseDown;
        OnMouseUp += HandleMouseUp;

        MoveCamWithDrag = true;
        HandleResize();
    }

    private void HandleContent()
    {
        if (!Window.IsFocused)
        {
            keysDown.Clear();
            buttonsDown.Clear();
        }
        if (isFirstFrame)
            // this fixes a rare bug where UI elements are misplaced due to ImGui resizing the window
            // on the second frame using the config.
            isFirstFrame = false;
        else
            Zanzarah.Update();
        fbArea.IsDirty = true;
        mouseArea.Content();
        fbArea.Content();
    }

    private void InvokeMouseMove(Vector2 delta) => onMouseMove?.Invoke(delta);

    private void HandleMouseDrag(MouseButton button, Vector2 delta)
    {
        if (button == MouseButton.Right)
            onMouseMove?.Invoke(delta);
    }

    private void HandleKeyDown(Key key) => keysDown.Add(key);
    private void HandleKeyUp(Key key) => keysDown.Remove(key);
    public bool IsKeyDown(Key key) => keysDown.Contains(key);
    private void HandleMouseDown(MouseButton button, Vector2 _) => buttonsDown.Add(button);
    private void HandleMouseUp(MouseButton button, Vector2 _) => buttonsDown.Remove(button);
    public bool IsMouseDown(MouseButton button) => buttonsDown.Contains(button);

    private void HandleResize()
    {
        Window.Title = $"Zanzarah {fbArea.Framebuffer.Width}x{fbArea.Framebuffer.Height}###Zanzarah";
    }

    private void HandleOpenScene()
    {
        var sceneResource = Zanzarah.CurrentGame?.SceneResource;
        if (sceneResource == null)
            return;

        diContainer.GetTag<OpenDocumentSet>().OpenWith<SceneEditor>(sceneResource);
    }

    private void HandleOpenECSExplorer()
    {
        if (ecsExplorer == null)
        {
            ecsExplorer = new ECSExplorer(diContainer, this);
            ecsExplorer.Window.OnClose += () => ecsExplorer = null;
        }
        else
            diContainer.GetTag<WindowContainer>().SetNextFocusedWindow(ecsExplorer.Window);
    }

    private void TeleportToScene(IResource resource)
    {
        // this should probably check if we even are in the Overworld
        var game = Zanzarah.CurrentGame;
        if (game == null)
            return;
        game.LoadScene(resource.Name.Replace(".scn", ""), () => game.FindEntryTrigger(-1));
    }

    public IEnumerable<(string, DefaultEcs.World)> GetWorlds()
    {
        yield return ("UI", Zanzarah.UI.GetTag<DefaultEcs.World>());
        if (Zanzarah.CurrentGame != null)
            yield return ("Overworld", Zanzarah.CurrentGame.GetTag<DefaultEcs.World>());
    }
}
