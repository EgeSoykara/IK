using MudBlazor;

namespace IK.Web.Components.Layout;

public static class StitchTheme
{
    private static readonly string[] FontStack = ["Inter", "Aptos", "Segoe UI", "Arial", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#c8102e",
            PrimaryContrastText = "#ffffff",
            Secondary = "#111111",
            SecondaryContrastText = "#ffffff",
            Tertiary = "#7a1c2b",
            TertiaryContrastText = "#ffffff",
            Background = "#f6f5f4",
            Surface = "#ffffff",
            AppbarBackground = "#ffffff",
            AppbarText = "#171717",
            DrawerBackground = "#111111",
            DrawerText = "#f5f3f2",
            DrawerIcon = "#f5f3f2",
            TextPrimary = "#171717",
            TextSecondary = "#66615f",
            ActionDefault = "#5f5a58",
            ActionDisabled = "#8c8784",
            ActionDisabledBackground = "#ebe8e6",
            LinesDefault = "#dedad7",
            LinesInputs = "#c9c3bf",
            TableLines = "#dedad7",
            TableStriped = "#f8f7f6",
            Divider = "#dedad7",
            DividerLight = "#ebe8e6",
            Error = "#b3261e",
            ErrorContrastText = "#ffffff",
            Success = "#19764a",
            SuccessContrastText = "#ffffff",
            Warning = "#a76400",
            WarningContrastText = "#ffffff",
            Info = "#1d4ed8",
            InfoContrastText = "#ffffff"
        },
        Typography = new Typography
        {
            Default = new DefaultTypography
            {
                FontFamily = FontStack,
                FontSize = "14px",
                LineHeight = "1.5",
                LetterSpacing = "0"
            },
            H1 = new H1Typography
            {
                FontFamily = FontStack,
                FontSize = "48px",
                FontWeight = "700",
                LineHeight = "1.2",
                LetterSpacing = "0"
            },
            H2 = new H2Typography
            {
                FontFamily = FontStack,
                FontSize = "32px",
                FontWeight = "600",
                LineHeight = "1.25",
                LetterSpacing = "0"
            },
            H3 = new H3Typography
            {
                FontFamily = FontStack,
                FontSize = "24px",
                FontWeight = "600",
                LineHeight = "1.3",
                LetterSpacing = "0"
            },
            H4 = new H4Typography
            {
                FontFamily = FontStack,
                FontSize = "20px",
                FontWeight = "600",
                LineHeight = "1.4",
                LetterSpacing = "0"
            },
            H5 = new H5Typography
            {
                FontFamily = FontStack,
                FontSize = "18px",
                FontWeight = "600",
                LineHeight = "1.4",
                LetterSpacing = "0"
            },
            H6 = new H6Typography
            {
                FontFamily = FontStack,
                FontSize = "16px",
                FontWeight = "600",
                LineHeight = "1.4",
                LetterSpacing = "0"
            },
            Body1 = new Body1Typography
            {
                FontFamily = FontStack,
                FontSize = "16px",
                LineHeight = "1.5",
                LetterSpacing = "0"
            },
            Body2 = new Body2Typography
            {
                FontFamily = FontStack,
                FontSize = "14px",
                LineHeight = "1.5",
                LetterSpacing = "0"
            },
            Button = new ButtonTypography
            {
                FontFamily = FontStack,
                FontSize = "12px",
                FontWeight = "600",
                LineHeight = "1",
                LetterSpacing = "0"
            },
            Caption = new CaptionTypography
            {
                FontFamily = FontStack,
                FontSize = "11px",
                FontWeight = "500",
                LineHeight = "1",
                LetterSpacing = "0"
            },
            Overline = new OverlineTypography
            {
                FontFamily = FontStack,
                FontSize = "12px",
                FontWeight = "600",
                LineHeight = "1",
                LetterSpacing = "0.05em",
                TextTransform = "uppercase"
            }
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
            DrawerWidthLeft = "272px",
            AppbarHeight = "68px"
        }
    };
}
