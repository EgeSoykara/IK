using MudBlazor;

namespace IK.Web.Components.Layout;

public static class StitchTheme
{
    private static readonly string[] FontStack = ["Inter", "Aptos", "Segoe UI", "Arial", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#3f4ad4",
            PrimaryContrastText = "#ffffff",
            Secondary = "#4d6077",
            SecondaryContrastText = "#ffffff",
            Tertiary = "#005daa",
            TertiaryContrastText = "#ffffff",
            Background = "#f7f9fc",
            Surface = "#ffffff",
            AppbarBackground = "#ffffff",
            AppbarText = "#191c1e",
            DrawerBackground = "#001529",
            DrawerText = "#e0e3e6",
            DrawerIcon = "#e0e3e6",
            TextPrimary = "#191c1e",
            TextSecondary = "#454654",
            ActionDefault = "#4d6077",
            ActionDisabled = "#767686",
            ActionDisabledBackground = "#e6e8eb",
            LinesDefault = "#c6c5d7",
            LinesInputs = "#c6c5d7",
            TableLines = "#c6c5d7",
            TableStriped = "#f2f4f7",
            Divider = "#c6c5d7",
            DividerLight = "#e0e3e6",
            Error = "#ba1a1a",
            ErrorContrastText = "#ffffff",
            Success = "#0075d5",
            SuccessContrastText = "#ffffff",
            Warning = "#8a5a00",
            WarningContrastText = "#ffffff",
            Info = "#005daa",
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
            DrawerWidthLeft = "256px",
            AppbarHeight = "64px"
        }
    };
}
