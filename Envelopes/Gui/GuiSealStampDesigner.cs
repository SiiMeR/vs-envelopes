using Vintagestory.API.Client;

namespace Envelopes.Gui;

public class GuiSealStampDesigner : GuiDialog
{
    public GuiSealStampDesigner(ICoreClientAPI? capi) : base(capi)
    {
        SetupDialog();
    }

    public override string ToggleKeyCombinationCode => "sealdesignerkeycombo";

    private void SetupDialog()
    {
        var lineHeight = 30;
        var elementToDialogPadding = GuiStyle.ElementToDialogPadding;
        var elementBounds = ElementStdBounds.AutosizedMainDialog.WithAlignment(EnumDialogArea.CenterMiddle);
        var num2 = 350;
        var elementBounds2 = ElementBounds.Fixed(5.0, elementToDialogPadding, num2, lineHeight);
        var elementBounds3 =
            ElementBounds.Fixed(5.0, elementToDialogPadding + lineHeight, num2, elementToDialogPadding);
        var elementBounds4 = ElementBounds.Fixed(5.0, elementToDialogPadding * 2.0 + lineHeight, num2, num2)
            .WithFixedPadding(2.0);
        var elementBounds5 = ElementBounds.Fixed(num2 + elementToDialogPadding,
            num2 / 2 - lineHeight / 2 + lineHeight + elementToDialogPadding, 120.0, lineHeight);
        var elementBounds6 = ElementBounds.Fixed(num2 + elementToDialogPadding, num2 + elementToDialogPadding * 2.0,
            120.0, lineHeight);
        var dialogBounds = ElementBounds.Fixed(0.0, 0.0, 500.0, num2 + 63).WithFixedPadding(14.0, 30.0);
        var cairoFont = CairoFont.WhiteDetailText();
        cairoFont.Color = new[] { 1.0, 0.2, 0.2, 1.0 };

        SingleComposer = capi.Gui.CreateCompo("stampdesigner", elementBounds)
            .AddShadedDialogBG(dialogBounds)
            .AddDialogTitleBar(Helpers.EnvelopesLangString("stampdesigner-title"));

        SingleComposer.Compose();
    }
}