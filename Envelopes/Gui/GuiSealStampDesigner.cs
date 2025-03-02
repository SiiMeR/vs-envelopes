using System;
using Cairo;
using Vintagestory.API.Client;

namespace Envelopes.Gui;

public class GuiSealStampDesigner : GuiDialog
{
    private readonly int[,] _editableArea =
    {
        { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 },
        { 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0 },
        { 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0 },
        { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 },
        { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        { 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0 },
        { 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0 },
        { 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0 },
        { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 }
    };

    private bool[,] _designState = new bool[16, 16];


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

    public override string ToggleKeyCombinationCode => "sealstampdesignerkeycombo";

    private bool SetupDialog()
    {
        _designState = new bool[16, 16];

        var leftSectionWidth = 400;
        var lineHeight = 30;
        var elementToDialogPadding = GuiStyle.ElementToDialogPadding;
        var inputBounds = ElementBounds.Fixed(5.0, elementToDialogPadding, leftSectionWidth, lineHeight);
        var gridBounds = ElementBounds
            .Fixed(5.0, elementToDialogPadding * 2.0 + lineHeight, leftSectionWidth, leftSectionWidth)
            .WithFixedPadding(2.0);

        var totalWidth = 550.0;
        var dialogContentBounds =
            ElementBounds.Fixed(0.0, 0.0, totalWidth, leftSectionWidth + 63).WithFixedPadding(14.0, 30.0);
        var dialogBounds = ElementBounds.Fixed(0.0, 0.0, totalWidth, leftSectionWidth + 63)
            .WithFixedPadding(14.0, 30.0);


        SingleComposer = capi.Gui.CreateCompo("stampdesigner",
                ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle))
            .AddShadedDialogBG(dialogBounds)
            .AddDialogTitleBar(Helpers.EnvelopesLangString("stampdesigner-title"), OnClose)
            .BeginChildElements(dialogContentBounds);


        SingleComposer.AddTextInput(inputBounds, OnTitleChanged, key: "title", font: CairoFont.TextInput());


        SingleComposer.GetTextInput("title")
            .SetPlaceHolderText(Helpers.EnvelopesLangString("stampdesigner-title-placeholder"));

        // ==== Grid

        SingleComposer.BeginChildElements(gridBounds);

        var toggleWidth = leftSectionWidth / 16;
        var toggleHeight = leftSectionWidth / 16;
        var toggleBounds = ElementBounds.Fixed(0, 0, toggleWidth, toggleHeight);

        for (var y = 0; y < 16; y++)
        {
            for (var x = 0; x < 16; x++)
            {
                var shouldDraw = _editableArea[y, x] == 1;

                if (!shouldDraw)
                {
                    continue;
                }

                var buttonBounds = toggleBounds.CopyOffsetedSibling(x * toggleWidth, y * toggleHeight);
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

                SingleComposer.GetToggleButton(key).On = true;
            }
        }

        SingleComposer.EndChildElements();

        // ==== End Grid

        var buttonWidth = 120.0;
        var createButtonBounds = ElementBounds.Fixed(leftSectionWidth + elementToDialogPadding,
            leftSectionWidth, buttonWidth, lineHeight);
        var resetButtonBounds = ElementBounds.Fixed(leftSectionWidth + elementToDialogPadding,
            leftSectionWidth + elementToDialogPadding * 2.0,
            buttonWidth, lineHeight);

        SingleComposer
            .AddButton(Helpers.EnvelopesLangString("stampdesigner-btn-create"), OnCreate, createButtonBounds,
                key: "createButton")
            .AddButton(Helpers.EnvelopesLangString("stampdesigner-btn-reset"), OnReset, resetButtonBounds,
                key: "resetButton")
            .EndChildElements()
            .Compose();

        SingleComposer.GetButton("createButton").Enabled = false;
        SingleComposer.Compose();

        return true;
    }

    private void OnToggleButton(bool on, int x, int y)
    {
        _designState[y, x] = on;
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
                Console.WriteLine(_designState);
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
}