using System;
using Envelopes.Messages;
using Envelopes.Util;
using Vintagestory.API.Client;

namespace Envelopes.Gui;

public class GuiSealStampDesigner : GuiDialog
{
    private const int LeftSectionWidth = 400;
    private const int LineHeight = 30;
    private const int GridDimensions = 24;

    private bool[,] _designState = new bool[GridDimensions, GridDimensions];

    private ElementBounds? _gridBounds;

    private bool _isDragging;

    public GuiSealStampDesigner(ICoreClientAPI? capi) : base(capi)
    {
        SetupDialog();

        capi?.Input.RegisterHotKey(
            "redraw",
            "Redraw",
            GlKeys.N,
            HotkeyType.GUIOrOtherControls
        );
        capi?.Input.SetHotKeyHandler("redraw", _ => SetupDialog());
    }


    private ElementBounds BaseBounds => ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);


    public override string ToggleKeyCombinationCode => "sealstampdesignerkeycombo";

    private bool SetupDialog()
    {
        _designState = new bool[GridDimensions, GridDimensions];

        var inputBounds = ElementBounds.Fixed(5.0, GuiStyle.ElementToDialogPadding, LeftSectionWidth, LineHeight);

        var totalWidth = 550.0;
        var dialogContentBounds =
            ElementBounds.Fixed(0.0, 0.0, totalWidth, LeftSectionWidth + 63).WithFixedPadding(14.0, 30.0);
        var dialogBounds = ElementBounds.Fixed(0.0, 0.0, totalWidth, LeftSectionWidth + 63)
            .WithFixedPadding(14.0, 30.0);


        SingleComposer = capi.Gui.CreateCompo("stampdesigner", BaseBounds)
            .AddShadedDialogBG(dialogBounds)
            .AddDialogTitleBar(Helpers.EnvelopesLangString("stampdesigner-title"), OnClose)
            .BeginChildElements(dialogContentBounds);

        SingleComposer.AddTextInput(inputBounds, OnTitleChanged, key: "title", font: CairoFont.TextInput());


        SingleComposer.GetTextInput("title")
            .SetPlaceHolderText(Helpers.EnvelopesLangString("stampdesigner-title-placeholder"));

        // ==== Grid

        _gridBounds = ElementBounds
            .Fixed(5.0, GuiStyle.ElementToDialogPadding * 2.0 + LineHeight, LeftSectionWidth, LeftSectionWidth)
            .WithParent(dialogContentBounds);

        SingleComposer.BeginChildElements(_gridBounds);

        var toggleSize = _gridBounds.fixedWidth / GridDimensions;
        var toggleBounds = ElementBounds.Fixed(0, 0, toggleSize, toggleSize);

        for (var y = 0; y < GridDimensions; y++)
        {
            for (var x = 0; x < GridDimensions; x++)
            {
                var shouldDraw = Constants.EditableArea[y, x] == 1;

                if (!shouldDraw)
                {
                    continue;
                }

                var buttonBounds = toggleBounds.CopyOffsetedSibling(x * toggleSize, y * toggleSize);
                var capturedX = x;
                var capturedY = y;
                var key = $"design-{x},{y}";
                SingleComposer.AddToggleButton(
                    "",
                    CairoFont.ButtonText(),
                    on => OnToggleButton(on, capturedX, capturedY),
                    buttonBounds,
                    key
                );
            }
        }

        SingleComposer.EndChildElements();

        // ==== End Grid


        var buttonWidth = 120.0;
        var createButtonBounds = ElementBounds.Fixed(LeftSectionWidth + GuiStyle.ElementToDialogPadding,
            LeftSectionWidth, buttonWidth, LineHeight);
        var resetButtonBounds = ElementBounds.Fixed(LeftSectionWidth + GuiStyle.ElementToDialogPadding,
            LeftSectionWidth + GuiStyle.ElementToDialogPadding * 2.0,
            buttonWidth, LineHeight);

        var addTutorialTextBounds = ElementBounds.Fixed((int)(LeftSectionWidth + GuiStyle.ElementToDialogPadding),
            LeftSectionWidth - LineHeight * 2, buttonWidth, LineHeight);

        var removeTutorialTextBounds = ElementBounds.Fixed((int)(LeftSectionWidth + GuiStyle.ElementToDialogPadding),
            LeftSectionWidth - LineHeight, buttonWidth, LineHeight);

        var addTutorialText =
            $"<icon name=leftmousebutton></icon>: {Helpers.EnvelopesLangString("stampdesigner-carve-tutorial")}";
        var removeTutorialText =
            $"<icon name=rightmousebutton></icon>: {Helpers.EnvelopesLangString("stampdesigner-fill-tutorial")}";
        var tutorialTextFont = CairoFont.WhiteDetailText().WithFontSize(20);
        SingleComposer
            .AddRichtext(addTutorialText, tutorialTextFont, addTutorialTextBounds,
                "addTutorial")
            .AddRichtext(removeTutorialText, tutorialTextFont, removeTutorialTextBounds,
                "removeTutorial")
            .AddButton(Helpers.EnvelopesLangString("stampdesigner-btn-create"), OnCreate, createButtonBounds,
                key: "createButton")
            .AddButton(Helpers.EnvelopesLangString("stampdesigner-btn-reset"), OnReset, resetButtonBounds,
                key: "resetButton")
            .EndChildElements();

        SingleComposer.GetButton("createButton").Enabled = false;

        SingleComposer.Compose();

        return true;
    }

    private void OnToggleButton(bool newState, int x, int y)
    {
        _designState[y, x] = newState;
    }

    private void OnClose()
    {
        TryClose();
    }

    private bool OnCreate()
    {
        new GuiDialogConfirm(capi, Helpers.EnvelopesLangString("stampdesigner-create-warning"), confirmed =>
        {
            if (confirmed)
            {
                var title = SingleComposer.GetTextInput("title").GetText();

                EnvelopesModSystem.ClientNetworkChannel?.SendPacket(new SaveStampDesignPacket
                {
                    Title = title, Design = BooleanArrayPacker.Pack(_designState), Dimensions = GridDimensions
                });

                TryClose();
            }
        }).TryOpen();

        return true;
    }

    private bool OnReset()
    {
        new GuiDialogConfirm(capi, Helpers.EnvelopesLangString("stampdesigner-reset-warning"), confirmed =>
        {
            if (confirmed)
            {
                SetupDialog();
            }
        }).TryOpen();

        return true;
    }


    private void OnTitleChanged(string newTitle)
    {
        SingleComposer.GetButton("createButton").Enabled = newTitle.Length > 0;
    }

    public override void OnMouseUp(MouseEvent args)
    {
        _isDragging = false;
        base.OnMouseUp(args);
    }

    public override void OnMouseDown(MouseEvent args)
    {
        if (_gridBounds == null)
        {
            base.OnMouseDown(args);
            return;
        }

        _isDragging = true;

        var posInside = _gridBounds.PositionInside(args.X, args.Y);
        if (posInside == null)
        {
            base.OnMouseDown(args);
            return;
        }

        var toggleSize = _gridBounds.ChildBounds[0].OuterWidth;

        var x = Convert.ToInt32(posInside.X / toggleSize);
        var y = Convert.ToInt32(posInside.Y / toggleSize);

        if (x < 0 || x >= GridDimensions || y < 0 || y >= GridDimensions)
        {
            base.OnMouseDown(args);
            return;
        }

        if (Constants.EditableArea[y, x] != 1)
        {
            base.OnMouseDown(args);
            return;
        }

        var shouldAdd = capi.Input.MouseButton.Left;
        var shouldDelete = capi.Input.MouseButton.Right;

        if (shouldAdd)
        {
            UpdateToggleState(x, y, true);
        }
        else if (shouldDelete)
        {
            UpdateToggleState(x, y, false);
        }
    }

    public override void OnMouseMove(MouseEvent args)
    {
        if (_gridBounds == null)
        {
            base.OnMouseMove(args);
            return;
        }

        if (!_isDragging)
        {
            base.OnMouseMove(args);
            return;
        }

        var posInside = _gridBounds.PositionInside(args.X, args.Y);
        if (posInside == null)
        {
            base.OnMouseMove(args);
            return;
        }


        var toggleSize = _gridBounds.ChildBounds[0].OuterWidth;

        var x = Convert.ToInt32(posInside.X / toggleSize);
        var y = Convert.ToInt32(posInside.Y / toggleSize);

        if (x < 0 || x >= GridDimensions || y < 0 || y >= GridDimensions)
        {
            base.OnMouseMove(args);
            return;
        }

        if (Constants.EditableArea[y, x] != 1)
        {
            base.OnMouseMove(args);
            return;
        }

        var shouldAdd = capi.Input.MouseButton.Left;
        var shouldDelete = capi.Input.MouseButton.Right;

        if (shouldAdd)
        {
            UpdateToggleState(x, y, true);
        }
        else if (shouldDelete)
        {
            UpdateToggleState(x, y, false);
        }
    }

    private void UpdateToggleState(int x, int y, bool state)
    {
        var key = $"design-{x},{y}";
        var button = SingleComposer.GetToggleButton(key);
        if (button != null && button.On != state)
        {
            button.On = state;
            OnToggleButton(state, x, y);
        }
    }
}