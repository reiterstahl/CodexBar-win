using System.ComponentModel;
using System.Globalization;

namespace CodexBar.Windows.Tray;

/// <summary>
/// Spanish and English UI strings, side by side. XAML binds through <c>{local:Tr Key}</c> and code
/// calls <see cref="T"/> or <see cref="F"/>; changing the language refreshes both in place.
/// Framework-independent so the test runner can check that every key is translated.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public const string Automatic = "auto";
    public const string Spanish = "es";
    public const string English = "en";

    private static readonly CultureInfo SpanishCulture = CultureInfo.GetCultureInfo("es-CR");
    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");

    private bool _english;

    private Loc()
    {
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static Loc Instance { get; } = new();

    public bool IsEnglish => _english;

    public CultureInfo Culture => _english ? EnglishCulture : SpanishCulture;

    public string this[string key] => Lookup(key);

    public static string T(string key) => Instance.Lookup(key);

    public static string F(string key, params object?[] arguments) =>
        string.Format(Instance.Culture, Instance.Lookup(key), arguments);

    /// <summary>"auto" follows the Windows display language: Spanish for es-*, otherwise English.</summary>
    public static string Resolve(string? setting, CultureInfo systemCulture)
    {
        return setting switch
        {
            Spanish => Spanish,
            English => English,
            _ => systemCulture.TwoLetterISOLanguageName.Equals("es", StringComparison.OrdinalIgnoreCase)
                ? Spanish
                : English,
        };
    }

    public void SetLanguage(string? setting)
    {
        bool english = Resolve(setting, CultureInfo.CurrentUICulture) == English;
        if (english == _english)
        {
            return;
        }

        _english = english;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnglish)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Culture)));
    }

    private string Lookup(string key)
    {
        return Strings.TryGetValue(key, out (string Spanish, string English) text)
            ? _english ? text.English : text.Spanish
            : key;
    }

    public static IReadOnlyDictionary<string, (string Spanish, string English)> Strings { get; } =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            // Main window
            ["RenameAccountTip"] = ("Clic para renombrar la cuenta", "Click to rename the account"),
            ["CopyLogin"] = ("Copiar login", "Copy login"),
            ["CopyLoginTip"] = (
                "Copia el comando de PowerShell para este perfil. No incluye credenciales.",
                "Copies the PowerShell command for this profile. It contains no credentials."),
            ["ViewCards"] = ("Tarjetas", "Cards"),
            ["ViewCardsName"] = ("Vista de tarjetas", "Cards view"),
            ["ViewSummary"] = ("Resumen", "Summary"),
            ["ViewSummaryName"] = ("Vista resumen", "Summary view"),
            ["ViewMini"] = ("Mini", "Mini"),
            ["ViewMiniName"] = ("Vista mini", "Mini view"),
            ["AlwaysOnTop"] = ("Siempre visible", "Always on top"),
            ["AlwaysOnTopHint"] = ("La ventana queda sobre las demás", "Keeps the window above the others"),
            ["Customize"] = ("Personalizar", "Customize"),
            ["Minimize"] = ("Minimizar", "Minimize"),
            ["MinimizeName"] = ("Minimizar a la barra de tareas", "Minimize to the taskbar"),
            ["Hide"] = ("Ocultar", "Hide"),
            ["HideName"] = ("Ocultar en el área de notificación", "Hide to the notification area"),
            ["RefreshNow"] = ("Actualizar ahora", "Refresh now"),
            ["RefreshTip"] = (
                "Consulta las cuentas ahora. También se actualiza sola cada 5 minutos.",
                "Checks every account now. It also refreshes on its own every 5 minutes."),
            ["Refresh"] = ("Actualizar", "Refresh"),
            ["Refreshing"] = ("Actualizando…", "Refreshing…"),
            ["Restart"] = ("Reiniciar", "Restart"),
            ["RestartTip"] = (
                "Cierra CodexBar, instala la versión nueva y lo vuelve a abrir",
                "Closes CodexBar, installs the new version and opens it again"),
            ["LoginCopied"] = ("Login copiado para {0}. Pegalo en PowerShell.", "Login command copied for {0}. Paste it in PowerShell."),
            ["LoginCopyFailed"] = ("No se pudo copiar el login. Intentá de nuevo.", "Could not copy the login command. Try again."),

            // Status line and footer
            ["StatusStarting"] = ("Iniciando…", "Starting…"),
            ["AgeJustNow"] = ("hace un momento", "just now"),
            ["AgeMinutes"] = ("hace {0} min", "{0} min ago"),
            ["StatusUpdated"] = ("Actualizado {0} · {1} de {2} con cuota", "Updated {0} · {1} of {2} with quota"),
            ["FooterNext"] = ("Próxima cuota: {0} en {1}", "Next quota: {0} in {1}"),
            ["FooterAllAvailable"] = ("Todas las cuentas tienen cuota", "Every account has quota"),
            ["FooterAuto"] = ("Consulta automática cada 5 min", "Checks automatically every 5 min"),
            ["UpdateReady"] = ("CodexBar {0} está lista para instalar", "CodexBar {0} is ready to install"),

            // Cards and quota windows
            ["PillAvailableAgain"] = ("Disponible otra vez", "Available again"),
            ["BackIn"] = ("Vuelve en {0}", "Back in {0}"),
            ["LimitReached"] = ("Límite alcanzado", "Limit reached"),
            ["NoSession"] = ("Sin sesión", "Signed out"),
            ["NoData"] = ("Sin datos", "No data"),
            ["SessionExpired"] = (
                "La sesión de {0} expiró o no existe. Copiá el login y ejecutalo en PowerShell.",
                "The {0} session expired or does not exist. Copy the login command and run it in PowerShell."),
            ["NoProviderData"] = ("No hay datos del proveedor.", "No data from the provider."),
            ["NoUsageInfo"] = ("{0} no devolvió información de uso.", "{0} returned no usage information."),
            ["WindowSession"] = ("Sesión", "Session"),
            ["WindowWeekly"] = ("Semanal", "Weekly"),
            ["CaptionFree"] = ("libre", "free"),
            ["CaptionUsed"] = ("usado", "used"),
            ["ResetUnavailable"] = ("Renovación no disponible", "Reset unavailable"),
            ["ResettingNow"] = ("Renovando ahora", "Resetting now"),
            ["ResetsInLong"] = ("Se renueva en {0}", "Resets in {0}"),
            ["ResetsInShort"] = ("Renueva en {0}", "Resets in {0}"),
            ["Today"] = ("hoy", "Today"),
            ["Tomorrow"] = ("mañana", "Tomorrow"),
            ["ResetShort"] = ("Renovación: {0} · {1}", "{0} at {1}"),
            ["ResetTooltip"] = ("{0}: se renueva el {1}", "{0}: resets on {1}"),
            ["ResetTooltipUnavailable"] = ("{0}: renovación no disponible", "{0}: reset unavailable"),
            ["PaceEnough"] = ("Al ritmo actual te alcanza", "Lasts until the reset at this rate"),
            ["PaceRunsOut"] = ("Se agota en ~{0} a este ritmo", "Runs out in ~{0} at this rate"),
            ["DateLong"] = ("dddd d 'de' MMMM, h:mm tt", "dddd, MMMM d, h:mm tt"),
            ["DateShortDay"] = ("ddd d MMM", "ddd, MMM d"),

            // Notifications and tray
            ["RecoveredTitle"] = ("{0} ya tiene cuota", "{0} has quota again"),
            ["RecoveredSession"] = ("La sesión se renovó: {0}% libre.", "The session reset: {0}% free."),
            ["RecoveredGeneric"] = ("La cuota volvió a estar disponible.", "Quota is available again."),
            ["TrayOpen"] = ("Abrir", "Open"),
            ["TrayCustomize"] = ("Personalizar…", "Customize…"),
            ["TrayExit"] = ("Salir", "Exit"),
            ["TrayRestartToUpdate"] = ("Reiniciar para actualizar a {0}", "Restart to update to {0}"),
            ["TrayNoAccounts"] = ("CodexBar — cuentas no disponibles", "CodexBar — accounts unavailable"),
            ["TraySummary"] = ("CodexBar — {0} de {1} con cuota", "CodexBar — {0} of {1} with quota"),
            ["UpdateReadyTitle"] = ("CodexBar {0} está lista", "CodexBar {0} is ready"),
            ["UpdateReadyBody"] = (
                "Reiniciá CodexBar desde la ventana o el menú de la bandeja para instalarla.",
                "Restart CodexBar from the window or the tray menu to install it."),
            ["UpdateFailed"] = ("No se pudo instalar la actualización: {0}", "Could not install the update: {0}"),

            // Customization window
            ["SettingsWindowTitle"] = ("Personalizar CodexBar", "Customize CodexBar"),
            ["SettingsSubtitle"] = ("Los cambios se aplican al instante y se guardan solos", "Changes apply instantly and are saved automatically"),
            ["Close"] = ("Cerrar", "Close"),
            ["CloseCustomizationName"] = ("Cerrar personalización", "Close customization"),
            ["Reset"] = ("Restablecer", "Reset"),
            ["ResetTip"] = (
                "Vuelve a los valores de fábrica. No cambia nombres ni posición de la ventana.",
                "Restores the defaults. Account names and window position are kept."),
            ["SettingsFooter"] = ("CodexBar {0} · Se guarda en {1}", "CodexBar {0} · Saved in {1}"),
            ["SectionTheme"] = ("TEMA", "THEME"),
            ["SectionAccent"] = ("COLOR DE ACENTO", "ACCENT COLOR"),
            ["SectionChart"] = ("TIPO DE GRÁFICO", "CHART TYPE"),
            ["SectionChartColors"] = ("COLOR DE LOS GRÁFICOS", "CHART COLORS"),
            ["SectionPercent"] = ("PORCENTAJE", "PERCENTAGE"),
            ["SectionView"] = ("VISTA", "VIEW"),
            ["SectionDensity"] = ("DENSIDAD", "DENSITY"),
            ["SectionLanguage"] = ("IDIOMA", "LANGUAGE"),
            ["SectionBehavior"] = ("COMPORTAMIENTO", "BEHAVIOR"),
            ["PickColorName"] = ("Elegir un color personalizado", "Choose a custom color"),
            ["PickColorTip"] = ("Elegir cualquier color", "Choose any color"),
            ["CustomColor"] = ("Personalizado", "Custom"),
            ["AccentHexName"] = ("Código hexadecimal del acento", "Accent hex code"),
            ["AccentHexTip"] = ("Escribí un color #RRGGBB y presioná Enter", "Type a #RRGGBB color and press Enter"),
            ["CodexColorName"] = ("Color de Codex", "Codex color"),
            ["ClaudeColorName"] = ("Color de Claude Code", "Claude Code color"),
            ["InterfaceSize"] = ("Tamaño de la interfaz", "Interface size"),
            ["PaceMarker"] = ("Marcador de ritmo", "Pace marker"),
            ["PaceMarkerHint"] = ("Indica si el consumo actual alcanza hasta la renovación", "Shows whether your current usage lasts until the reset"),
            ["AvailableFirst"] = ("Cuentas con cuota primero", "Accounts with quota first"),
            ["AvailableFirstHint"] = ("Las cuentas agotadas bajan al final", "Exhausted accounts move to the bottom"),
            ["NotifyRecovery"] = ("Avisar cuando vuelva la cuota", "Notify when quota returns"),
            ["NotifyRecoveryHint"] = ("Notificación de Windows al renovarse una cuenta", "Windows notification when an account resets"),
            ["DynamicIcon"] = ("Ícono dinámico en la bandeja", "Dynamic tray icon"),
            ["DynamicIconHint"] = ("Muestra la sesión y la semana de la cuenta más limitada", "Shows the session and weekly quota of the most limited account"),
            ["StartWithWindows"] = ("Iniciar con Windows", "Start with Windows"),
            ["StartWithWindowsHint"] = ("Abre CodexBar en el área de notificación al iniciar sesión", "Opens CodexBar in the notification area when you sign in"),

            // Option labels
            ["ChartBar"] = ("Barras", "Bars"),
            ["ChartRing"] = ("Anillos", "Rings"),
            ["ChartGauge"] = ("Medidor", "Gauge"),
            ["ChartBlocks"] = ("Bloques", "Blocks"),
            ["ChartNumbers"] = ("Números", "Numbers"),
            ["ColorAccent"] = ("Acento", "Accent"),
            ["ColorLevel"] = ("Por nivel", "By level"),
            ["ColorProvider"] = ("Proveedor", "Provider"),
            ["PercentAvailable"] = ("Disponible", "Available"),
            ["PercentUsed"] = ("Usado", "Used"),
            ["DensityComfortable"] = ("Cómoda", "Comfortable"),
            ["DensityCompact"] = ("Compacta", "Compact"),
            ["LanguageAuto"] = ("Automático", "Automatic"),
            ["ThemeGrafito"] = ("Grafito", "Graphite"),
            ["ThemeMedianoche"] = ("Medianoche", "Midnight"),
            ["ThemeClaro"] = ("Claro", "Light"),
            ["ThemePapel"] = ("Papel", "Paper"),
            ["ThemeContraste"] = ("Contraste", "Contrast"),
            ["AccentYellow"] = ("Amarillo CodexBar", "CodexBar yellow"),
            ["AccentBlue"] = ("Azul", "Blue"),
            ["AccentTeal"] = ("Verde agua", "Teal"),
            ["AccentOrange"] = ("Naranja", "Orange"),
            ["AccentPurple"] = ("Lila", "Purple"),
            ["AccentPink"] = ("Rosa", "Pink"),
        };
}
