namespace Cities.Presentation.UI
{
    /// <summary>USS-klassen uit <c>Assets/UI/Styles/Components.uss</c>, zodat code geen losse strings gebruikt.</summary>
    public static class UiClasses
    {
        public const string Root = "c-root";

        public const string TextTitle = "c-text--title";
        public const string TextHeading = "c-text--heading";
        public const string TextEmphasis = "c-text--emphasis";
        public const string TextCaption = "c-text--caption";
        public const string TextMuted = "c-text--muted";

        public const string Button = "c-button";
        public const string ButtonPrimary = "c-button--primary";
        public const string ButtonGhost = "c-button--ghost";
        public const string ButtonDanger = "c-button--danger";
        public const string ButtonSmall = "c-button--small";
        public const string ButtonIcon = "c-button--icon";
        public const string ButtonSelected = "c-button--selected";

        public const string Panel = "c-panel";
        public const string PanelHeader = "c-panel__header";
        public const string PanelTitle = "c-panel__title";
        public const string PanelClose = "c-panel__close";
        public const string PanelBody = "c-panel__body";
        public const string PanelFooter = "c-panel__footer";

        public const string Toolbar = "c-toolbar";
        public const string ToolbarSeparator = "c-toolbar__separator";

        public const string Badge = "c-badge";
        public const string Meter = "c-meter";
        public const string MeterFill = "c-meter__fill";
        public const string Tooltip = "c-tooltip";

        /// <summary>Klasse voor een knopvariant; secundair is de basisstijl en heeft geen extra klasse.</summary>
        public static string ForVariant(ButtonVariant variant)
        {
            switch (variant)
            {
                case ButtonVariant.Primary: return ButtonPrimary;
                case ButtonVariant.Ghost: return ButtonGhost;
                case ButtonVariant.Danger: return ButtonDanger;
                default: return null;
            }
        }

        /// <summary>Klasse voor een statusbadge, of null voor de neutrale stijl.</summary>
        public static string ForStatus(UiStatus status)
        {
            switch (status)
            {
                case UiStatus.Success: return "c-badge--success";
                case UiStatus.Warning: return "c-badge--warning";
                case UiStatus.Danger: return "c-badge--danger";
                case UiStatus.Info: return "c-badge--info";
                default: return null;
            }
        }

        /// <summary>Klasse voor de kleur van een meter, of null voor de accentkleur.</summary>
        public static string ForMeterColor(MeterColor color)
        {
            switch (color)
            {
                case MeterColor.Residential: return "c-meter--residential";
                case MeterColor.Commercial: return "c-meter--commercial";
                case MeterColor.Industrial: return "c-meter--industrial";
                case MeterColor.Office: return "c-meter--office";
                default: return null;
            }
        }
    }

    public enum ButtonVariant
    {
        Secondary,
        Primary,
        Ghost,
        Danger,
    }

    public enum UiStatus
    {
        Neutral,
        Success,
        Warning,
        Danger,
        Info,
    }

    public enum MeterColor
    {
        Accent,
        Residential,
        Commercial,
        Industrial,
        Office,
    }
}
