using System;
using Cairo;
using Envelopes.Util;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.GameContent;

namespace Envelopes.Gui;

public class GuiDialogEnvelopeHeadersEditor : GuiDialog
{
    private string _fromText = string.Empty;
    private readonly Action<string, string> _onSave;
    private string _toText = string.Empty;


    public GuiDialogEnvelopeHeadersEditor(ICoreClientAPI capi, Action<string, string> onSave) : base(capi)
    {
        _onSave = onSave;
        capi?.Input.RegisterHotKey(
            "redraw",
            "Redraw",
            GlKeys.N,
            HotkeyType.GUIOrOtherControls
        );
        capi?.Input.SetHotKeyHandler("redraw", _ => SetupDialog());
    }

    private ElementBounds BaseBounds => ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);

    public override string ToggleKeyCombinationCode => "envelopeheadereditorkeycombo";

    public override void OnGuiOpened()
    {
        SetupDialog();
    }

    private bool SetupDialog()
    {
        var dialogBounds = ElementStdBounds.AutosizedMainDialog
            .WithAlignment(EnumDialogArea.CenterMiddle);

        var canvasBounds = ElementBounds.Fixed(0, 0, 700, 350);


        var font = new CairoFont
        {
            Color = new[] { 0d, 0d, 0d, 1d },
            FontWeight = FontWeight.Bold,
            Orientation = EnumTextOrientation.Left,
            Fontname = GuiStyle.DecorativeFontName,
            Slant = FontSlant.Italic,
            UnscaledFontsize = 24.0
        };

        SingleComposer = capi.Gui.CreateCompo("envelopeHeadersEditor", dialogBounds)
            .BeginChildElements(canvasBounds)
            .AddImage(
                ElementBounds.Fixed(0, 0, 700, 350),
                new AssetLocation("envelopes:textures/envelope-back.png"))
            .AddStaticText(Helpers.EnvelopesLangString("headereditor-from"), font,
                ElementBounds.Fixed(40, 40, 480, 30))
            .AddTextInputNoBackground(
                ElementBounds.Fixed(40, 80, 480, 40),
                OnFromFieldChanged,
                font,
                "txtFrom"
            )
            .AddStaticText(Helpers.EnvelopesLangString("headereditor-to"),
                font,
                ElementBounds.Fixed(40, 140, 480, 30))
            .AddTextInputNoBackground(
                ElementBounds.Fixed(40, 180, 480, 40),
                OnToFieldChanged,
                font,
                "txtTo"
            )
            .EndChildElements()
            .AddSmallButton(
                Helpers.EnvelopesLangString("headereditor-close"),
                OnCloseButtonClicked,
                ElementBounds.Fixed(150, 300, 90, 30)
            )
            .AddSmallButton(
                Helpers.EnvelopesLangString("headereditor-save"),
                OnSaveButtonClicked,
                ElementBounds.Fixed(300, 300, 90, 30)
            )
            .Compose();

        return true;
    }


    private void OnTitleBarClose()
    {
        TryClose();
    }

    private void OnFromFieldChanged(string newText)
    {
        _fromText = newText;
    }

    private void OnToFieldChanged(string newText)
    {
        _toText = newText;
    }

    private bool OnCloseButtonClicked()
    {
        TryClose();
        return true;
    }


    private bool OnSaveButtonClicked()
    {
        _onSave(_fromText, _toText);
        TryClose();
        return true;
    }
}